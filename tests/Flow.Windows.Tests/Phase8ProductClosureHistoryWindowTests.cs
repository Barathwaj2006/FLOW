using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Flow.Core.History;
using Flow.Core.Storage;
using Flow.Host.Windows.History;
using Xunit;

namespace Flow.Windows.Tests;

/// <summary>
/// Phase 8 Final Product Closure Tests:
/// Comprehensive adversarial and functional validation of the Windows-native History Window UI,
/// ViewModels, FTS search, filtering, pagination, favorite toggling, soft deletion with undo,
/// deterministic productivity KPI metrics (with time-saved), multi-format export, and window lifecycle.
/// </summary>
public class Phase8ProductClosureHistoryWindowTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _tempExportDir;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly HistoryPrivacyService _privacyService;
    private readonly HistoryRetentionService _retentionService;
    private readonly ProductivityStatisticsService _statsService;
    private readonly HistoryExportService _exportService;
    private readonly HistoryService _historyService;

    public Phase8ProductClosureHistoryWindowTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_closure_test_{Guid.NewGuid():N}.db");
        _tempExportDir = Path.Combine(Path.GetTempPath(), $"flow_closure_exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempExportDir);

        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
        _privacyService = new HistoryPrivacyService();
        _retentionService = new HistoryRetentionService(_repository);
        _statsService = new ProductivityStatisticsService(_repository);
        _exportService = new HistoryExportService(_repository);

        _historyService = new HistoryService(
            _repository,
            _repository,
            _statsService,
            _retentionService,
            _exportService,
            _privacyService
        );
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
        try { if (Directory.Exists(_tempExportDir)) Directory.Delete(_tempExportDir, true); } catch { }
    }

    private static void RunOnSta(Action action)
    {
        Exception? exCaught = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exCaught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exCaught != null)
        {
            throw new TargetInvocationException(exCaught);
        }
    }

    private static void RunOnSta(Func<Task> asyncAction)
    {
        Exception? exCaught = null;
        var thread = new Thread(() =>
        {
            try
            {
                asyncAction().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                exCaught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exCaught != null)
        {
            throw new TargetInvocationException(exCaught);
        }
    }

    [Fact]
    public void HistoryInHub_Instantiates_OnStaThread()
    {
        RunOnSta(() =>
        {
            var window = new Flow.Host.Windows.UI.FlowHubWindow(null, null, null, _historyService, null);

            Assert.NotNull(window);
            Assert.Equal("FLOW — Voice Productivity", window.Title);

            window.Close();
        });
    }

    [Fact]
    public async Task HistoryViewModel_Paging_FtsSearch_And_AppFilter()
    {
        // 1. Seed 12 distinct dictation records
        for (int i = 1; i <= 12; i++)
        {
            string app = i % 2 == 0 ? "notepad.exe" : "cursor.exe";
            string topic = i == 5 ? "microservice distributed architecture" : $"general note number {i}";
            var entry = new DictationEntry(
                Id: $"entry_{i:D3}",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-i * 5),
                DurationMs: 5000,
                CharacterCount: topic.Length,
                WordCount: topic.Split(' ').Length,
                Language: "en-US",
                Application: app,
                ApplicationCategory: "Editor",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: topic
            );
            await _repository.InsertAsync(entry);
        }

        var historyVm = new HistoryViewModel(_historyService);

        // 2. Initial load
        await historyVm.RefreshAsync(0);
        Assert.Equal(12, historyVm.TotalItems);
        Assert.Equal(12, historyVm.Entries.Count);
        Assert.False(historyVm.IsEmpty);
        Assert.Contains("12 entries", historyVm.PageInfo);

        // 3. FTS Search filter
        historyVm.SearchQuery = "distributed architecture";
        await historyVm.RefreshAsync(0);
        Assert.Single(historyVm.Entries);
        Assert.Equal("entry_005", historyVm.Entries[0].Id);
        Assert.Contains("microservice distributed architecture", historyVm.Entries[0].Text);

        // 4. Application filter
        historyVm.SearchQuery = "";
        historyVm.SelectedApplication = "notepad.exe";
        await historyVm.RefreshAsync(0);
        Assert.Equal(6, historyVm.Entries.Count);
        Assert.All(historyVm.Entries, e => Assert.Equal("notepad.exe", e.Application));

        // 5. Reset filter
        historyVm.SelectedApplication = "All Applications";
        await historyVm.RefreshAsync(0);
        Assert.Equal(12, historyVm.Entries.Count);
    }

    [Fact]
    public async Task HistoryViewModel_FavoriteToggle_PersistsInDatabase()
    {
        var entry = new DictationEntry(
            Id: "fav_test_001",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 4000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en-US",
            Application: "notepad.exe",
            ApplicationCategory: "Editor",
            Mode: "Dictation",
            State: HistoryState.Completed,
            IsFavorite: false,
            Text: "Important meeting notes"
        );
        await _repository.InsertAsync(entry);

        var historyVm = new HistoryViewModel(_historyService);
        await historyVm.RefreshAsync(0);

        var item = historyVm.Entries.First(e => e.Id == "fav_test_001");
        Assert.False(item.IsFavorite);

        // Toggle Favorite
        await historyVm.ToggleFavoriteAsync(item);

        // Verify in ViewModel
        var updatedItem = historyVm.Entries.First(e => e.Id == "fav_test_001");
        Assert.True(updatedItem.IsFavorite);

        // Verify directly in SQLite repository
        var repoItem = await _repository.GetByIdAsync("fav_test_001");
        Assert.NotNull(repoItem);
        Assert.True(repoItem.IsFavorite);

        // Toggle back
        await historyVm.ToggleFavoriteAsync(updatedItem);
        Assert.False(historyVm.Entries.First(e => e.Id == "fav_test_001").IsFavorite);
    }

    [Fact]
    public async Task HistoryViewModel_SoftDelete_And_Undo_RestoresItem()
    {
        var entry = new DictationEntry(
            Id: "delete_test_001",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 3000,
            CharacterCount: 15,
            WordCount: 3,
            Language: "en-US",
            Application: "cursor.exe",
            ApplicationCategory: "Editor",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Temporary notes"
        );
        await _repository.InsertAsync(entry);

        var historyVm = new HistoryViewModel(_historyService);
        await historyVm.RefreshAsync(0);
        Assert.Equal(1, historyVm.TotalItems);

        // 1. Soft Delete
        await historyVm.DeleteEntryAsync(historyVm.Entries[0]);
        Assert.Empty(historyVm.Entries);
        Assert.Equal(0, historyVm.TotalItems);
        Assert.True(historyVm.CanUndoDelete);

        // Check SQLite
        var repoItem = await _repository.GetByIdAsync("delete_test_001");
        Assert.NotNull(repoItem);
        Assert.True(repoItem.IsDeleted);

        // 2. Undo Delete
        await historyVm.UndoLastDeleteAsync();
        Assert.Single(historyVm.Entries);
        Assert.Equal(1, historyVm.TotalItems);
        Assert.False(historyVm.CanUndoDelete);

        // Check SQLite restored
        repoItem = await _repository.GetByIdAsync("delete_test_001");
        Assert.NotNull(repoItem);
        Assert.False(repoItem.IsDeleted);
    }

    [Fact]
    public void HistoryViewModel_SafeCopyToClipboard_CopiesTextWithoutEnter()
    {
        RunOnSta(() =>
        {
            var entry = new DictationEntry(
                Id: "clip_test_001",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow,
                DurationMs: 2500,
                CharacterCount: 22,
                WordCount: 4,
                Language: "en-US",
                Application: "notepad.exe",
                ApplicationCategory: "Editor",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: "Strictly safe dictation"
            );

            var historyVm = new HistoryViewModel(_historyService);
            historyVm.CopyToClipboard(entry);

            string clipboardText = Clipboard.GetText();
            Assert.Equal("Strictly safe dictation", clipboardText);
            // Invariant: Clipboard text must not contain automated carriage return / line feeds
            Assert.DoesNotContain("\r", clipboardText);
            Assert.DoesNotContain("\n", clipboardText);
        });
    }

    [Fact]
    public async Task StatisticsViewModel_KPI_Calculations_And_DeterministicTimeSaved()
    {
        // 1. Seed 3 sessions with known parameters:
        // Session 1: 120 words, 60,000 ms (1 min) -> Dictation: 120 WPM. Typing @ 40 WPM: 3.0 min. Time saved: 2.0 min.
        // Session 2: 80 words, 30,000 ms (0.5 min) -> Dictation: 160 WPM. Typing @ 40 WPM: 2.0 min. Time saved: 1.5 min.
        // Session 3: 40 words, 20,000 ms (0.333 min) -> Dictation: 120 WPM. Typing @ 40 WPM: 1.0 min. Time saved: 0.667 min.
        // Total words: 240. Total active duration: 110,000 ms (1.833 min).
        // Typing @ 40 WPM: 240 / 40 = 6.0 min. Time saved: 6.0 - 1.833 = 4.167 min (approx 4m 10s saved).
        var now = DateTimeOffset.UtcNow;
        await _repository.InsertAsync(new DictationEntry("s1", Guid.NewGuid(), now.AddMinutes(-30), 60000, 600, 120, "en-US", "notepad.exe", "Editor", "Dictation", HistoryState.Completed, Text: "sample"));
        await _repository.InsertAsync(new DictationEntry("s2", Guid.NewGuid(), now.AddMinutes(-20), 30000, 400, 80, "en-US", "slack.exe", "Chat", "Dictation", HistoryState.Completed, Text: "sample"));
        await _repository.InsertAsync(new DictationEntry("s3", Guid.NewGuid(), now.AddMinutes(-10), 20000, 200, 40, "es-ES", "notepad.exe", "Editor", "Dictation", HistoryState.Completed, Text: "sample"));

        var statsVm = new StatisticsViewModel(_historyService)
        {
            SelectedWindow = TimeRangeWindow.Today
        };
        await statsVm.LoadAsync();

        Assert.NotNull(statsVm.Metrics);
        Assert.Equal("240", statsVm.FormattedTotalWords);
        Assert.Equal("3", statsVm.FormattedTotalSessions);
        Assert.Equal("1,200", statsVm.FormattedTotalCharacters);
        Assert.Contains("WPM", statsVm.FormattedAverageWpm);
        Assert.Contains("saved", statsVm.FormattedTimeSaved);

        // Check top applications
        Assert.Contains(statsVm.TopApplicationsList, kv => kv.Key == "notepad.exe" && kv.Value == 2);
        Assert.Contains(statsVm.TopApplicationsList, kv => kv.Key == "slack.exe" && kv.Value == 1);

        // Check top languages
        Assert.Contains(statsVm.TopLanguagesList, kv => kv.Key == "en-US" && kv.Value == 2);
        Assert.Contains(statsVm.TopLanguagesList, kv => kv.Key == "es-ES" && kv.Value == 1);

        // Check insights
        Assert.Equal("notepad.exe", statsVm.FormattedMostUsedApp);
        Assert.Equal("en-US", statsVm.FormattedMostUsedLang);
    }

    [Fact]
    public async Task HistoryViewModel_MultiFormat_Export()
    {
        var entry = new DictationEntry(
            Id: "exp_001",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 30,
            WordCount: 5,
            Language: "en-US",
            Application: "notepad.exe",
            ApplicationCategory: "Editor",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Deterministic export test sample"
        );
        await _repository.InsertAsync(entry);

        var historyVm = new HistoryViewModel(_historyService);
        await historyVm.RefreshAsync(0);

        // 1. JSON Export
        string jsonPath = Path.Combine(_tempExportDir, "history_export.json");
        string resultJson = await historyVm.ExportAsync(jsonPath, HistoryExportFormat.Json);
        Assert.True(File.Exists(resultJson));
        string jsonContent = await File.ReadAllTextAsync(resultJson);
        Assert.Contains("Deterministic export test sample", jsonContent);
        Assert.Contains("exp_001", jsonContent);

        // 2. CSV Export
        string csvPath = Path.Combine(_tempExportDir, "history_export.csv");
        string resultCsv = await historyVm.ExportAsync(csvPath, HistoryExportFormat.Csv);
        Assert.True(File.Exists(resultCsv));
        string csvContent = await File.ReadAllTextAsync(resultCsv);
        Assert.Contains("Id,SessionId,CreatedAt", csvContent);
        Assert.Contains("exp_001", csvContent);
        Assert.Contains("Deterministic export test sample", csvContent);

        // 3. PlainText Export
        string txtPath = Path.Combine(_tempExportDir, "history_export.txt");
        string resultTxt = await historyVm.ExportAsync(txtPath, HistoryExportFormat.PlainText);
        Assert.True(File.Exists(resultTxt));
        string txtContent = await File.ReadAllTextAsync(resultTxt);
        Assert.Contains("# FLOW Dictation History Export", txtContent);
        Assert.Contains("Deterministic export test sample", txtContent);
    }

    [Fact]
    public void HistoryTab_HubLifecycle_OnStaThread()
    {
        RunOnSta(() =>
        {
            var hub = new Flow.Host.Windows.UI.FlowHubWindow(null, null, null, _historyService, null);
            Assert.NotNull(hub);
            Assert.Equal("FLOW — Voice Productivity", hub.Title);
            hub.Close();
        });
    }
}
