using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8RetentionAndStorageLimitTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly HistoryRetentionService _retentionService;

    public Phase8RetentionAndStorageLimitTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_retention_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
        _retentionService = new HistoryRetentionService(_repository);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task Retention_ThirtyDaysPolicy_PrunesOlderEntries()
    {
        // 1. Insert entries: 10 days old, 40 days old, 100 days old
        var e1 = new DictationEntry(
            Id: "fresh",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-10),
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Fresh entry 10 days old"
        );

        var e2 = new DictationEntry(
            Id: "expired1",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-40),
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Expired entry 40 days old"
        );

        var e3 = new DictationEntry(
            Id: "expired2",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-100),
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Expired entry 100 days old"
        );

        await _repository.InsertAsync(e1);
        await _repository.InsertAsync(e2);
        await _repository.InsertAsync(e3);

        // Configure retention to 30 days
        var settings = await _repository.GetSettingsAsync();
        await _repository.SaveSettingsAsync(settings with { Retention = RetentionPolicy.ThirtyDays });

        int pruned = await _retentionService.EnforceRetentionAsync();
        Assert.Equal(2, pruned);

        Assert.NotNull(await _repository.GetByIdAsync("fresh"));
        Assert.Null(await _repository.GetByIdAsync("expired1"));
        Assert.Null(await _repository.GetByIdAsync("expired2"));
    }

    [Fact]
    public async Task Retention_StarredEntries_AreNeverPurgedByAge()
    {
        // Insert 200 days old entry that is starred
        var favoriteOld = new DictationEntry(
            Id: "starred_old",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-200),
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Important starred entry",
            IsFavorite: true // INVIOLABLE: Starred entries are protected from auto-purge
        );

        await _repository.InsertAsync(favoriteOld);

        var settings = await _repository.GetSettingsAsync();
        await _repository.SaveSettingsAsync(settings with { Retention = RetentionPolicy.ThirtyDays });

        int pruned = await _retentionService.EnforceRetentionAsync();
        Assert.Equal(0, pruned);

        var fetched = await _repository.GetByIdAsync("starred_old");
        Assert.NotNull(fetched);
        Assert.True(fetched.IsFavorite);
    }

    [Fact]
    public async Task Retention_MaxCount_PrunesOldestEntriesOverCeiling()
    {
        // Configure ceiling to 5 entries
        var settings = await _repository.GetSettingsAsync();
        await _repository.SaveSettingsAsync(settings with
        {
            Retention = RetentionPolicy.Unlimited,
            MaxHistoryEntries = 5
        });

        // Insert 10 entries
        for (int i = 0; i < 10; i++)
        {
            await _repository.InsertAsync(new DictationEntry(
                Id: $"entry_{i:D2}",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-10 + i), // entry_00 is oldest
                DurationMs: 5000,
                CharacterCount: 20,
                WordCount: 4,
                Language: "en",
                Application: "app.exe",
                ApplicationCategory: "General",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: $"Entry {i}"
            ));
        }

        int pruned = await _retentionService.EnforceRetentionAsync();
        Assert.Equal(5, pruned);

        int remaining = await _repository.GetCountAsync();
        Assert.Equal(5, remaining);

        // Oldest (entry_00 to entry_04) should be pruned; newer (entry_05 to entry_09) remain
        Assert.Null(await _repository.GetByIdAsync("entry_00"));
        Assert.NotNull(await _repository.GetByIdAsync("entry_09"));
    }

    [Fact]
    public async Task Retention_StarredEntries_AreNeverPurgedByCountCeiling()
    {
        var settings = await _repository.GetSettingsAsync();
        await _repository.SaveSettingsAsync(settings with { MaxHistoryEntries = 2 });

        // Insert 1 favorite and 3 normal entries
        await _repository.InsertAsync(new DictationEntry(
            Id: "fav_old",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow.AddDays(-10),
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Old Starred",
            IsFavorite: true
        ));

        for (int i = 1; i <= 3; i++)
        {
            await _repository.InsertAsync(new DictationEntry(
                Id: $"norm_{i}",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-i),
                DurationMs: 5000,
                CharacterCount: 20,
                WordCount: 4,
                Language: "en",
                Application: "app.exe",
                ApplicationCategory: "General",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: $"Normal {i}"
            ));
        }

        await _retentionService.EnforceRetentionAsync();

        // Favorite entry must STILL exist
        var fav = await _repository.GetByIdAsync("fav_old");
        Assert.NotNull(fav);
        Assert.True(fav.IsFavorite);
    }
}
