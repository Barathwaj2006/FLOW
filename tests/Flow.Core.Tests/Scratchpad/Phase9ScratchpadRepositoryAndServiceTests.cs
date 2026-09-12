using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadRepositoryAndServiceTests
{
    [Fact]
    public async Task Repository_InsertAndGetById_RoundTripsDurableData()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var entry = ScratchpadEntry.Create("Architecture Notes", "Core engine uses .NET 9 structured concurrency.");
        string id = await repo.InsertAsync(entry);
        Assert.Equal(entry.Id, id);

        var retrieved = await repo.GetByIdAsync(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Architecture Notes", retrieved.Title);
        Assert.Equal("Core engine uses .NET 9 structured concurrency.", retrieved.Content);
        Assert.False(retrieved.IsPinned);
        Assert.False(retrieved.IsDeleted);
        Assert.Equal(7, retrieved.WordCount);
        Assert.Equal(entry.Content.Length, retrieved.CharacterCount);
    }

    [Fact]
    public async Task Repository_Update_ModifiesContentAndTimestamp()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var entry = ScratchpadEntry.Create("Initial", "First line of thought.");
        await repo.InsertAsync(entry);

        var updated = entry.WithUpdatedContent("Revised Title", "First line of thought.\nSecond line added.");
        bool success = await repo.UpdateAsync(updated);
        Assert.True(success);

        var retrieved = await repo.GetByIdAsync(entry.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Revised Title", retrieved.Title);
        Assert.Equal("First line of thought.\nSecond line added.", retrieved.Content);
        Assert.Equal(7, retrieved.WordCount);
    }

    [Fact]
    public async Task Repository_SetPinned_TogglesPinningStatus()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var entry = ScratchpadEntry.Create("Unpinned", "Sample content");
        await repo.InsertAsync(entry);

        bool pinned = await repo.SetPinnedAsync(entry.Id, true);
        Assert.True(pinned);

        var afterPin = await repo.GetByIdAsync(entry.Id);
        Assert.NotNull(afterPin);
        Assert.True(afterPin.IsPinned);

        bool unpinned = await repo.SetPinnedAsync(entry.Id, false);
        Assert.True(unpinned);

        var afterUnpin = await repo.GetByIdAsync(entry.Id);
        Assert.NotNull(afterUnpin);
        Assert.False(afterUnpin.IsPinned);
    }

    [Fact]
    public async Task Repository_SoftDeleteAndRestore_MaintainsReversibility()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var entry = ScratchpadEntry.Create("To Delete", "Delete test");
        await repo.InsertAsync(entry);

        // Soft delete
        bool deleted = await repo.SoftDeleteAsync(entry.Id);
        Assert.True(deleted);

        // Active listing does not include deleted item
        var activePage = await repo.GetPagedAsync(new ScratchpadFilter(IsDeleted: false), 0, 50);
        Assert.DoesNotContain(activePage.Items, x => x.Id == entry.Id);

        // Deleted listing includes it
        var deletedPage = await repo.GetPagedAsync(new ScratchpadFilter(IsDeleted: true), 0, 50);
        Assert.Contains(deletedPage.Items, x => x.Id == entry.Id);
        var deletedItem = deletedPage.Items.First(x => x.Id == entry.Id);
        Assert.True(deletedItem.IsDeleted);
        Assert.NotNull(deletedItem.DeletedAt);

        // Restore
        bool restored = await repo.RestoreAsync(entry.Id);
        Assert.True(restored);

        activePage = await repo.GetPagedAsync(new ScratchpadFilter(IsDeleted: false), 0, 50);
        Assert.Contains(activePage.Items, x => x.Id == entry.Id);
    }

    [Fact]
    public async Task Repository_PermanentDelete_RemovesRowCompletely()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var entry = ScratchpadEntry.Create("Permanent", "Permanent deletion test");
        await repo.InsertAsync(entry);

        bool removed = await repo.DeletePermanentlyAsync(entry.Id);
        Assert.True(removed);

        var retrieved = await repo.GetByIdAsync(entry.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Repository_Sorting_PinsPrecedeUnpinnedItems()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var time = DateTimeOffset.UtcNow;
        var unpinnedNew = ScratchpadEntry.Create("Unpinned New", "content", isPinned: false, updatedAt: time.AddMinutes(10));
        var pinnedOld = ScratchpadEntry.Create("Pinned Old", "content", isPinned: true, updatedAt: time.AddMinutes(1));
        var unpinnedOld = ScratchpadEntry.Create("Unpinned Old", "content", isPinned: false, updatedAt: time);

        await repo.InsertAsync(unpinnedNew);
        await repo.InsertAsync(pinnedOld);
        await repo.InsertAsync(unpinnedOld);

        var page = await repo.GetPagedAsync(new ScratchpadFilter(SortBy: ScratchpadSortOrder.PinnedFirstThenUpdated), 0, 10);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal("Pinned Old", page.Items[0].Title);
        Assert.Equal("Unpinned New", page.Items[1].Title);
        Assert.Equal("Unpinned Old", page.Items[2].Title);
    }

    [Fact]
    public async Task ExportService_ExportsMarkdownPlainTextAndJsonSafely()
    {
        var exportService = new ScratchpadExportService();
        var entry = ScratchpadEntry.Create("Quick Ideas", "Line 1\nLine 2");

        string tempDir = Path.Combine(Path.GetTempPath(), $"flow_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. PlainText
            string txtPath = Path.Combine(tempDir, "note.txt");
            await exportService.ExportAsync(entry, txtPath, ScratchpadExportFormat.PlainText);
            string txtContent = await File.ReadAllTextAsync(txtPath);
            Assert.Contains("Quick Ideas", txtContent);
            Assert.Contains("Line 1\nLine 2", txtContent);

            // 2. Markdown
            string mdPath = Path.Combine(tempDir, "note.md");
            await exportService.ExportAsync(entry, mdPath, ScratchpadExportFormat.Markdown);
            string mdContent = await File.ReadAllTextAsync(mdPath);
            Assert.StartsWith("# Quick Ideas", mdContent);
            Assert.Contains("Line 1\nLine 2", mdContent);

            // 3. JSON
            string jsonPath = Path.Combine(tempDir, "note.json");
            await exportService.ExportAsync(entry, jsonPath, ScratchpadExportFormat.Json);
            string jsonContent = await File.ReadAllTextAsync(jsonPath);
            Assert.Contains("\"Title\": \"Quick Ideas\"", jsonContent);
            Assert.Contains("\"WordCount\": 4", jsonContent);

            // 4. ExportAll
            var entries = new[] { entry, ScratchpadEntry.Create("Second Entry", "Some more thoughts") };
            string exportSubdir = Path.Combine(tempDir, "all_notes");
            var exportedFiles = await exportService.ExportAllAsync(entries, exportSubdir, ScratchpadExportFormat.Markdown);
            Assert.Equal(2, exportedFiles.Count);
            Assert.All(exportedFiles, path => Assert.True(File.Exists(path)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ScratchpadService_HighLevelWorkflow_SucceedsEndToEnd()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);
        var service = new ScratchpadService(repo);

        // Create
        var created = await service.CreateScratchpadAsync("Project Alpha", "Initial roadmap");
        Assert.NotNull(created);
        Assert.Equal("Project Alpha", created.Title);

        // Update
        var updated = await service.UpdateScratchpadAsync(created.Id, "Project Alpha (Updated)", "Initial roadmap\nPhase 9 in progress");
        Assert.NotNull(updated);
        Assert.Equal("Project Alpha (Updated)", updated.Title);
        Assert.Equal(6, updated.WordCount);

        // Pin & List
        await service.PinScratchpadAsync(created.Id);
        var pinnedList = await service.GetPinnedScratchpadsAsync();
        Assert.Single(pinnedList);
        Assert.Equal(created.Id, pinnedList[0].Id);

        // Soft Delete
        await service.DeleteScratchpadAsync(created.Id);
        var recent = await service.GetRecentScratchpadsAsync();
        Assert.Empty(recent);

        // Restore
        await service.RestoreScratchpadAsync(created.Id);
        recent = await service.GetRecentScratchpadsAsync();
        Assert.Single(recent);
    }
}
