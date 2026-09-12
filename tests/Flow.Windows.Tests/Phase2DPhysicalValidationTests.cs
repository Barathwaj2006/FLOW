using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Storage;
using Flow.Core.TranscriptProcessing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Flow.Windows.Tests;

/// <summary>
/// Physical Desktop Validation Tests for Phase 2D: Personalization Engine.
/// Verifies real physical on-disk SQLite persistence, WAL journaling,
/// dictionary corrections, voice snippets, styles, and Zero-Enter invariant.
/// </summary>
public sealed class Phase2DPhysicalValidationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testDbPath;

    public Phase2DPhysicalValidationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "FLOW_Phase2D_PhysicalTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _testDbPath = Path.Combine(_testDir, "physical_personalization.db");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task PhysicalDisk_SqliteDatabase_CreatesTables_AndEnablesWAL()
    {
        using (var db = new SqlitePersonalizationDatabase(_testDbPath))
        {
            Assert.True(File.Exists(_testDbPath), "Physical SQLite database file must exist on Windows disk.");

            await using var conn = db.CreateConnection();
            await using var cmd = conn.CreateCommand();

            // Verify WAL journal mode
            cmd.CommandText = "PRAGMA journal_mode;";
            var mode = await cmd.ExecuteScalarAsync();
            Assert.NotNull(mode);
            Assert.Equal("wal", mode.ToString()?.ToLowerInvariant());

            // Verify Foreign Keys enabled
            cmd.CommandText = "PRAGMA foreign_keys;";
            var fk = await cmd.ExecuteScalarAsync();
            Assert.NotNull(fk);
            Assert.Equal(1L, Convert.ToInt64(fk));

            // Verify schema tables exist
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('DictionaryEntries', 'Snippets', 'StyleProfiles', 'AppStyleMappings');";
            long tableCount = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(4L, tableCount);
        }
    }

    [Fact]
    public async Task PhysicalDisk_PersonalDictionary_PersistsAcrossDatabaseReopen()
    {
        // 1. Write entries to disk database
        using (var db = new SqlitePersonalizationDatabase(_testDbPath))
        {
            var repo = new SqlitePersonalDictionaryRepository(db);
            await repo.AddAsync(new DictionaryEntry
            {
                Term = "anti gravity",
                Replacement = "Antigravity",
                IsStarred = true,
                Category = "Engineering"
            });
            await repo.AddAsync(new DictionaryEntry
            {
                Term = "react native",
                Replacement = "React Native",
                IsStarred = false,
                Category = "Framework"
            });
        }

        // 2. Re-open database from disk and verify persistence
        using (var db2 = new SqlitePersonalizationDatabase(_testDbPath))
        {
            var repo2 = new SqlitePersonalDictionaryRepository(db2);
            var entries = await repo2.GetAllAsync();

            Assert.Equal(2, entries.Count);
            // Starred should be first
            Assert.Equal("anti gravity", entries[0].Term);
            Assert.True(entries[0].IsStarred);
            Assert.Equal("Antigravity", entries[0].Replacement);

            Assert.Equal("react native", entries[1].Term);
            Assert.False(entries[1].IsStarred);
        }
    }

    [Fact]
    public async Task PhysicalDisk_Snippets_PersistAndExpand_WithZeroEnterGuarantee()
    {
        using var db = new SqlitePersonalizationDatabase(_testDbPath);
        var repo = new SqliteSnippetRepository(db);

        // Add snippet with multi-line text (which must be safely stripped of Enters)
        await repo.AddAsync(new SnippetEntry
        {
            TriggerPhrase = "my office address",
            ExpansionText = "Building 42\r\nSilicon Avenue\nSuite 100",
            Category = "Office"
        });

        var engine = new SnippetExpansionEngine(repo);
        await engine.ReloadAsync();

        string spoken = "please mail the package to my office address today";
        string expanded = engine.Expand(spoken);

        // Assert expansion occurred
        Assert.Contains("Building 42", expanded);
        Assert.Contains("Silicon Avenue", expanded);
        Assert.Contains("Suite 100", expanded);

        // Inviolable Zero-Enter invariant
        Assert.DoesNotContain("\r", expanded);
        Assert.DoesNotContain("\n", expanded);
    }

    [Fact]
    public async Task PhysicalDisk_EndToEndPipeline_WithRealDatabaseEngines()
    {
        using var db = new SqlitePersonalizationDatabase(_testDbPath);
        var dictRepo = new SqlitePersonalDictionaryRepository(db);
        var snippetRepo = new SqliteSnippetRepository(db);
        var styleRepo = new SqliteStyleRepository(db);

        // Add real entries
        await dictRepo.AddAsync(new DictionaryEntry { Term = "whisper net", Replacement = "Whisper.net", IsStarred = true });
        await snippetRepo.AddAsync(new SnippetEntry { TriggerPhrase = "standup report", ExpansionText = "Status: green. Blockers: none." });
        await styleRepo.SetStyleForAppAsync("devenv", "style_technical");

        var dictEngine = new PersonalDictionaryEngine(dictRepo);
        var snippetEngine = new SnippetExpansionEngine(snippetRepo);
        var styleEngine = new StyleFormattingEngine(styleRepo);

        await dictEngine.ReloadAsync();
        await snippetEngine.ReloadAsync();
        await styleEngine.ReloadAsync();

        var pipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine, styleEngine);

        // Process realistic spoken transcript
        string raw = "we are testing whisper net with standup report in visual studio period";
        string formatted = pipeline.Format(raw, targetApplication: "devenv.exe");

        // "whisper net" -> "Whisper.net"
        // "standup report" -> "Status: green. Blockers: none."
        // "period" -> "."
        // Sentence capitalized, zero Enters
        Assert.Contains("Whisper.net", formatted);
        Assert.Contains("Status: green. Blockers: none.", formatted);
        Assert.EndsWith(".", formatted);
        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
    }
}
