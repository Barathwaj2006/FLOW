using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase85SchemaV3ForensicAuditTests : IDisposable
{
    private readonly string _tempDbPath;

    public Phase85SchemaV3ForensicAuditTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_forensic_v3_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task ForensicAudit_V2ToV3_Migration_PreservesPersonalizationRecords()
    {
        // 1. Manually create V2 database with all 4 tables and seed records
        using (var rawDb = new SqlitePersonalizationDatabase(_tempDbPath))
        {
            await rawDb.ExecuteNonQueryAsync("PRAGMA user_version = 2;");

            var dictRepo = new SqlitePersonalDictionaryRepository(rawDb);
            for (int i = 0; i < 5; i++)
            {
                await dictRepo.AddAsync(new DictionaryEntry
                {
                    Term = $"Term_{i}",
                    Replacement = $"Replacement_{i}",
                    Language = "en",
                    ApplicationScope = "global",
                    IsEnabled = true
                });
            }

            var snippetRepo = new SqliteSnippetRepository(rawDb);
            for (int i = 0; i < 5; i++)
            {
                await snippetRepo.AddAsync(new SnippetEntry
                {
                    TriggerPhrase = $"trig_{i}",
                    ExpansionText = $"Expansion text {i}",
                    Description = $"Snippet {i}",
                    IsEnabled = true
                });
            }

            var styleRepo = new SqliteStyleRepository(rawDb);
            await styleRepo.AddProfileAsync(new StyleProfile
            {
                Id = "p1",
                Name = "Engineering",
                Description = "Technical writing style",
                ContractionPolicy = ContractionPolicy.Expand,
                FormalityLevel = FormalityLevel.Formal,
                UseBulletPoints = true
            });
            await styleRepo.AddProfileAsync(new StyleProfile
            {
                Id = "p2",
                Name = "CasualChat",
                Description = "Informal chat style",
                ContractionPolicy = ContractionPolicy.Preserve,
                FormalityLevel = FormalityLevel.Casual,
                UseBulletPoints = false
            });

            await styleRepo.SetStyleForAppAsync("devenv.exe", "p1");
        }

        // 2. Open with SqlitePersonalizationDatabase which executes migration to v3
        using (var v3Db = new SqlitePersonalizationDatabase(_tempDbPath))
        {
            int version = v3Db.GetSchemaVersion();
            Assert.Equal(3, version);

            string? integrity = await v3Db.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
            Assert.Equal("ok", integrity);

            string? journal = await v3Db.ExecuteScalarAsync<string>("PRAGMA journal_mode;");
            Assert.Equal("wal", journal?.ToLowerInvariant());

            // 3. Verify ALL 5 dictionary entries survive
            var dictRepo = new SqlitePersonalDictionaryRepository(v3Db);
            for (int i = 0; i < 5; i++)
            {
                var entry = await dictRepo.GetByTermAsync($"Term_{i}");
                Assert.NotNull(entry);
                Assert.Equal($"Replacement_{i}", entry.Replacement);
            }

            // 4. Verify ALL 5 snippets survive
            var snippetRepo = new SqliteSnippetRepository(v3Db);
            for (int i = 0; i < 5; i++)
            {
                var snippet = await snippetRepo.GetByTriggerAsync($"trig_{i}");
                Assert.NotNull(snippet);
                Assert.Equal($"Expansion text {i}", snippet.ExpansionText);
            }

            // 5. Verify style profiles and mappings survive
            var styleRepo = new SqliteStyleRepository(v3Db);
            var p1 = await styleRepo.GetProfileByIdAsync("p1");
            Assert.NotNull(p1);
            Assert.Equal("Engineering", p1.Name);

            var mapping = await styleRepo.GetStyleIdForAppAsync("devenv.exe");
            Assert.NotNull(mapping);
            Assert.Equal("p1", mapping);

            // 6. Verify DictationHistory and HistorySettings tables exist and are functional
            var histRepo = new SqliteHistoryRepository(v3Db);
            var settings = await histRepo.GetSettingsAsync();
            Assert.NotNull(settings);
            Assert.True(settings.HistoryEnabled);
            Assert.Equal(RetentionPolicy.Unlimited, settings.Retention);
        }
    }

    [Fact]
    public async Task ForensicAudit_SchemaObjects_TablesIndexesTriggersExist()
    {
        using var db = new SqlitePersonalizationDatabase(_tempDbPath);
        await using var conn = db.CreateConnection();

        // 1. Audit Tables
        await using var cmdTbl = conn.CreateCommand();
        cmdTbl.CommandText = "SELECT name, type FROM sqlite_master WHERE type IN ('table', 'view');";
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var r = await cmdTbl.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                tables.Add(r.GetString(0));
            }
        }

        Assert.Contains("DictionaryEntries", tables);
        Assert.Contains("Snippets", tables);
        Assert.Contains("StyleProfiles", tables);
        Assert.Contains("AppStyleMappings", tables);
        Assert.Contains("DictationHistory", tables);
        Assert.Contains("DictationHistoryFts", tables);
        Assert.Contains("HistorySettings", tables);

        // 2. Audit Triggers
        await using var cmdTrg = conn.CreateCommand();
        cmdTrg.CommandText = "SELECT name FROM sqlite_master WHERE type='trigger';";
        var triggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var r = await cmdTrg.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                triggers.Add(r.GetString(0));
            }
        }

        Assert.Contains("trg_history_ai", triggers);
        Assert.Contains("trg_history_ad", triggers);
        Assert.Contains("trg_history_au", triggers);

        // 3. Audit Indices
        await using var cmdIdx = conn.CreateCommand();
        cmdIdx.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND tbl_name='DictationHistory';";
        var indices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var r = await cmdIdx.ExecuteReaderAsync())
        {
            while (await r.ReadAsync())
            {
                indices.Add(r.GetString(0));
            }
        }

        Assert.Contains("idx_history_created", indices);
        Assert.Contains("idx_history_app", indices);
        Assert.Contains("idx_history_lang", indices);
        Assert.Contains("idx_history_fav", indices);
        Assert.Contains("idx_history_mode", indices);
        Assert.Contains("idx_history_state", indices);
        Assert.Contains("idx_history_deleted", indices);
    }

    [Fact]
    public async Task ForensicAudit_FtsSynchronizationTriggers_InsertUpdateDelete()
    {
        using var db = new SqlitePersonalizationDatabase(_tempDbPath);
        var repo = new SqliteHistoryRepository(db);

        var entry = new DictationEntry(
            Id: "fts_sync_entry",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 30,
            WordCount: 5,
            Language: "en",
            Application: "notepad.exe",
            ApplicationCategory: "Document",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Original speech recognition transcript"
        );

        // 1. INSERT trigger test
        await repo.InsertAsync(entry);

        await using (var conn = db.CreateConnection())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM DictationHistoryFts WHERE DictationHistoryFts MATCH '\"Original\"';";
            long count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }

        // 2. UPDATE trigger test
        await using (var conn = db.CreateConnection())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE DictationHistory SET Text = 'Modified text content for trigger audit' WHERE Id = 'fts_sync_entry';";
            await cmd.ExecuteNonQueryAsync();

            // Old token returns 0
            cmd.CommandText = "SELECT COUNT(*) FROM DictationHistoryFts WHERE DictationHistoryFts MATCH '\"Original\"';";
            long oldCnt = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, oldCnt);

            // New token returns 1
            cmd.CommandText = "SELECT COUNT(*) FROM DictationHistoryFts WHERE DictationHistoryFts MATCH '\"Modified\"';";
            long newCnt = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(1, newCnt);
        }

        // 3. DELETE trigger test
        await repo.DeletePermanentlyAsync("fts_sync_entry");

        await using (var conn = db.CreateConnection())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM DictationHistoryFts WHERE DictationHistoryFts MATCH '\"Modified\"';";
            long afterDel = Convert.ToInt64(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, afterDel);
        }
    }
}
