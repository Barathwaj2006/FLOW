using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadDataModelAndMigrationTests
{
    [Fact]
    public void ScratchpadEntry_Create_DerivesTitleAndCalculatesCountsCorrectly()
    {
        string content = "# Meeting Notes\nDiscussed Q3 objectives with team.\nAction items:\n- Refactor pipeline";
        var entry = ScratchpadEntry.Create("", content);

        Assert.Equal("Meeting Notes", entry.Title);
        Assert.Equal(content, entry.Content);
        Assert.False(entry.IsPinned);
        Assert.False(entry.IsDeleted);
        Assert.Null(entry.DeletedAt);
        Assert.Equal(content.Length, entry.CharacterCount);
        Assert.Equal(13, entry.WordCount);
        Assert.True(entry.CreatedAt <= DateTimeOffset.UtcNow);
        Assert.True(entry.UpdatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ScratchpadEntry_Create_WithEmptyContent_HasSafeFallbacks()
    {
        var entry = ScratchpadEntry.Create("", "");

        Assert.Equal("Untitled Scratchpad", entry.Title);
        Assert.Equal(string.Empty, entry.Content);
        Assert.Equal(0, entry.CharacterCount);
        Assert.Equal(0, entry.WordCount);
    }

    [Fact]
    public void ScratchpadEntry_WithUpdatedContent_UpdatesTimestampAndCounts()
    {
        var entry = ScratchpadEntry.Create("Original", "One two three");
        var initialUpdated = entry.UpdatedAt;

        var futureTime = initialUpdated.AddMinutes(5);
        var updated = entry.WithUpdatedContent("New Title", "One two three four five six", futureTime);

        Assert.Equal("New Title", updated.Title);
        Assert.Equal("One two three four five six", updated.Content);
        Assert.Equal(6, updated.WordCount);
        Assert.Equal(27, updated.CharacterCount);
        Assert.Equal(futureTime, updated.UpdatedAt);
        Assert.Equal(entry.CreatedAt, updated.CreatedAt);
        Assert.Equal(entry.Id, updated.Id);
    }

    [Fact]
    public void ScratchpadEntry_DeriveTitleFromContent_TruncatesLongTitlesSafely()
    {
        string longLine = new string('A', 100);
        string title = ScratchpadEntry.DeriveTitleFromContent(longLine);

        Assert.Equal(60, title.Length);
        Assert.EndsWith("...", title);
    }

    [Fact]
    public void FreshDatabase_InitializesDirectlyToSchemaVersion4()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        int version = db.GetSchemaVersion();
        Assert.Equal(4, version);

        using var conn = db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var result = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("ok", result);
    }

    [Fact]
    public void Migration_FromVersion3ToVersion4_PreservesExistingDataAndAddsScratchpads()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"flow_mig_{Guid.NewGuid():N}.db");
        try
        {
            // 1. Manually setup v3 database
            using (var conn = new SqliteConnection($"Data Source={tempDb}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE DictionaryEntries (
                        Id TEXT PRIMARY KEY, Term TEXT NOT NULL, Replacement TEXT,
                        IsStarred INTEGER NOT NULL DEFAULT 0, Category TEXT, CaseSensitive INTEGER NOT NULL DEFAULT 0,
                        IsEnabled INTEGER NOT NULL DEFAULT 1, Language TEXT, ApplicationScope TEXT,
                        CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL
                    );
                    CREATE TABLE Snippets (
                        Id TEXT PRIMARY KEY, TriggerPhrase TEXT NOT NULL, ExpansionText TEXT NOT NULL,
                        IsEnabled INTEGER NOT NULL DEFAULT 1, Description TEXT, Category TEXT, Language TEXT,
                        ApplicationScope TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL
                    );
                    CREATE TABLE StyleProfiles (
                        Id TEXT PRIMARY KEY, Name TEXT NOT NULL UNIQUE, Description TEXT,
                        ContractionPolicy INTEGER NOT NULL DEFAULT 0, FormalityLevel INTEGER NOT NULL DEFAULT 1,
                        UseBulletPoints INTEGER NOT NULL DEFAULT 0, IsEnabled INTEGER NOT NULL DEFAULT 1,
                        LanguageScope TEXT, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL
                    );
                    CREATE TABLE AppStyleMappings (
                        ProcessName TEXT PRIMARY KEY, StyleProfileId TEXT NOT NULL, CreatedAt TEXT NOT NULL
                    );
                    CREATE TABLE DictationHistory (
                        Id TEXT PRIMARY KEY, SessionId TEXT NOT NULL, CreatedAt TEXT NOT NULL, DurationMs INTEGER NOT NULL,
                        CharacterCount INTEGER NOT NULL, WordCount INTEGER NOT NULL, Language TEXT NOT NULL,
                        Application TEXT NOT NULL, ApplicationCategory TEXT NOT NULL, Mode TEXT NOT NULL, State TEXT NOT NULL,
                        WasEdited INTEGER NOT NULL DEFAULT 0, IsFavorite INTEGER NOT NULL DEFAULT 0, Text TEXT,
                        TextHash TEXT, MetadataJson TEXT, IsDeleted INTEGER NOT NULL DEFAULT 0, DeletedAt TEXT
                    );
                    CREATE TABLE HistorySettings (
                        Key TEXT PRIMARY KEY, Value TEXT NOT NULL, UpdatedAt TEXT NOT NULL
                    );

                    INSERT INTO DictationHistory (Id, SessionId, CreatedAt, DurationMs, CharacterCount, WordCount, Language, Application, ApplicationCategory, Mode, State, Text)
                    VALUES ('hist-1', '00000000-0000-0000-0000-000000000001', '2026-09-12T10:00:00Z', 1200, 20, 4, 'en-US', 'notepad.exe', 'Editor', 'Dictation', 'Completed', 'Hello world historical test');

                    PRAGMA user_version = 3;
                ";
                cmd.ExecuteNonQuery();
            }

            // 2. Open with SqlitePersonalizationDatabase which should trigger migration to v4
            using (var db = new SqlitePersonalizationDatabase(tempDb))
            {
                int version = db.GetSchemaVersion();
                Assert.Equal(4, version);

                using var conn = db.CreateConnection();

                // Verify historical entry is completely preserved
                using (var checkHistCmd = conn.CreateCommand())
                {
                    checkHistCmd.CommandText = "SELECT Text FROM DictationHistory WHERE Id = 'hist-1';";
                    var text = checkHistCmd.ExecuteScalar()?.ToString();
                    Assert.Equal("Hello world historical test", text);
                }

                // Verify Scratchpads table and indexes exist
                using (var checkScratchCmd = conn.CreateCommand())
                {
                    checkScratchCmd.CommandText = "SELECT COUNT(*) FROM Scratchpads;";
                    int count = Convert.ToInt32(checkScratchCmd.ExecuteScalar());
                    Assert.Equal(0, count);
                }

                // Verify PRAGMA integrity_check
                using (var intCmd = conn.CreateCommand())
                {
                    intCmd.CommandText = "PRAGMA integrity_check;";
                    var status = intCmd.ExecuteScalar()?.ToString();
                    Assert.Equal("ok", status);
                }
            }
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
