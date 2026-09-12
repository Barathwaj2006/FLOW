using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8DeletionAndUndoTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;

    public Phase8DeletionAndUndoTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_del_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task SoftDelete_And_UndoRestore_RoundTrip()
    {
        var entry = new DictationEntry(
            Id: "del_test_1",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "notepad.exe",
            ApplicationCategory: "TextEditor",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Text to be deleted"
        );

        await _repository.InsertAsync(entry);

        // 1. Verify visible
        var p1 = await _repository.GetPagedAsync(new HistoryFilter(), 0, 10);
        Assert.Single(p1.Items);

        // 2. Soft delete
        bool deleted = await _repository.SoftDeleteAsync("del_test_1");
        Assert.True(deleted);

        // Verify hidden from default query
        var p2 = await _repository.GetPagedAsync(new HistoryFilter(IncludeDeleted: false), 0, 10);
        Assert.Empty(p2.Items);

        // Still retrievable directly or with IncludeDeleted
        var fetchedDeleted = await _repository.GetByIdAsync("del_test_1");
        Assert.NotNull(fetchedDeleted);
        Assert.True(fetchedDeleted.IsDeleted);
        Assert.NotNull(fetchedDeleted.DeletedAt);

        // 3. Undo / Restore
        bool restored = await _repository.RestoreAsync("del_test_1");
        Assert.True(restored);

        // Reappears in default queries
        var p3 = await _repository.GetPagedAsync(new HistoryFilter(IncludeDeleted: false), 0, 10);
        Assert.Single(p3.Items);
        Assert.False(p3.Items[0].IsDeleted);
        Assert.Null(p3.Items[0].DeletedAt);
    }

    [Fact]
    public async Task PermanentDelete_CompletelyRemovesRowAndFts()
    {
        var entry = new DictationEntry(
            Id: "perm_test_1",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 25,
            WordCount: 4,
            Language: "en",
            Application: "notepad.exe",
            ApplicationCategory: "TextEditor",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Ephemeral confidential text"
        );

        await _repository.InsertAsync(entry);

        bool removed = await _repository.DeletePermanentlyAsync("perm_test_1");
        Assert.True(removed);

        Assert.Null(await _repository.GetByIdAsync("perm_test_1"));

        // FTS search returns empty
        var search = await _repository.SearchAsync("confidential");
        Assert.Empty(search.Items);
    }

    [Fact]
    public async Task DeleteAll_WithoutConfirmation_ThrowsInvalidOperationException()
    {
        await _repository.InsertAsync(new DictationEntry(
            Id: "safe_row",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 10,
            WordCount: 2,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Should survive"
        ));

        // Attempt DeleteAll without confirmation
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.DeleteAllAsync(confirm: false));

        // Verify data preserved
        int count = await _repository.GetCountAsync();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task DeleteAll_WithConfirmation_DeletesAllNonFavoriteRecords()
    {
        await _repository.InsertAsync(new DictationEntry(
            Id: "normal_row",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 10,
            WordCount: 2,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Normal"
        ));

        await _repository.InsertAsync(new DictationEntry(
            Id: "fav_row",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 10,
            WordCount: 2,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Protected Favorite",
            IsFavorite: true
        ));

        int deleted = await _repository.DeleteAllAsync(confirm: true);
        Assert.Equal(1, deleted);

        Assert.Null(await _repository.GetByIdAsync("normal_row"));
        Assert.NotNull(await _repository.GetByIdAsync("fav_row"));
    }
}
