using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.History;

namespace Flow.Host.Windows.History;

/// <summary>
/// ViewModel for History list, search, pagination, filtering, and deletion with undo (WF-039, WF-040).
/// Native Windows desktop UX (Segoe UI, zero glassmorphism, zero fake numbers).
/// </summary>
public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private readonly IHistoryService _historyService;
    private string _searchQuery = "";
    private string _selectedApplication = "All Applications";
    private string _selectedLanguage = "All Languages";
    private bool? _onlyFavorites;
    private int _currentPage = 0;
    private int _totalPages = 0;
    private int _totalItems = 0;
    private bool _isLoading;
    private DictationEntry? _lastDeletedEntry;
    private bool _canUndoDelete;
    private string? _statusBannerMessage;

    public ObservableCollection<DictationEntry> Entries { get; } = new();
    public ObservableCollection<string> AvailableApplications { get; } = new() { "All Applications" };
    public ObservableCollection<string> AvailableLanguages { get; } = new() { "All Languages" };

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EmptyMessage));
                _ = RefreshAsync(0);
            }
        }
    }

    public string SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            if (_selectedApplication != value)
            {
                _selectedApplication = value;
                OnPropertyChanged();
                _ = RefreshAsync(0);
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                _ = RefreshAsync(0);
            }
        }
    }

    public bool? OnlyFavorites
    {
        get => _onlyFavorites;
        set
        {
            if (_onlyFavorites != value)
            {
                _onlyFavorites = value;
                OnPropertyChanged();
                _ = RefreshAsync(0);
            }
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            _currentPage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanNextPage));
            OnPropertyChanged(nameof(CanPreviousPage));
            OnPropertyChanged(nameof(PageInfo));
        }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            _totalPages = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanNextPage));
            OnPropertyChanged(nameof(CanPreviousPage));
            OnPropertyChanged(nameof(PageInfo));
        }
    }

    public int TotalItems
    {
        get => _totalItems;
        private set
        {
            _totalItems = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(EmptyMessage));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    public bool CanUndoDelete
    {
        get => _canUndoDelete;
        private set { _canUndoDelete = value; OnPropertyChanged(); }
    }

    public bool CanNextPage => _currentPage + 1 < _totalPages;
    public bool CanPreviousPage => _currentPage > 0;

    public string PageInfo => _totalItems == 0
        ? "0 entries"
        : $"Page {_currentPage + 1} of {Math.Max(1, _totalPages)} ({_totalItems} entries)";

    public bool IsEmpty => Entries.Count == 0 && !IsLoading;

    public string EmptyMessage => !string.IsNullOrWhiteSpace(_searchQuery)
        ? "No dictations match your search filter."
        : "No dictation history found. Start dictating with [Right Alt] to see your transcripts here.";

    public string? StatusBannerMessage
    {
        get => _statusBannerMessage;
        set
        {
            _statusBannerMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStatusBanner));
        }
    }

    public bool HasStatusBanner => !string.IsNullOrEmpty(_statusBannerMessage);

    public void DismissStatusBanner() => StatusBannerMessage = null;

    public HistoryViewModel(IHistoryService historyService)
    {
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
    }

    public async Task PopulateFilterOptionsAsync()
    {
        try
        {
            var stats = await _historyService.Statistics.GetStatisticsAsync(TimeRangeWindow.AllTime);
            foreach (var app in stats.TopApplications.Keys)
            {
                if (!string.IsNullOrWhiteSpace(app) && !AvailableApplications.Contains(app))
                {
                    AvailableApplications.Add(app);
                }
            }
            foreach (var lang in stats.TopLanguages.Keys)
            {
                if (!string.IsNullOrWhiteSpace(lang) && !AvailableLanguages.Contains(lang))
                {
                    AvailableLanguages.Add(lang);
                }
            }
        }
        catch
        {
            // Non-critical: filter options will retain defaults
        }
    }

    public async Task RefreshAsync(int pageIndex = 0)
    {
        IsLoading = true;
        try
        {
            string? appFilter = _selectedApplication is "All Applications" or "" ? null : _selectedApplication;
            string? langFilter = _selectedLanguage is "All Languages" or "" ? null : _selectedLanguage;

            var filter = new HistoryFilter(
                SearchQuery: string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery,
                Application: appFilter,
                Language: langFilter,
                IsFavorite: _onlyFavorites
            );

            var page = await _historyService.Repository.GetPagedAsync(filter, pageIndex, 50);
            Entries.Clear();
            foreach (var item in page.Items)
            {
                Entries.Add(item);
                if (!string.IsNullOrWhiteSpace(item.Application) && !AvailableApplications.Contains(item.Application))
                {
                    AvailableApplications.Add(item.Application);
                }
                if (!string.IsNullOrWhiteSpace(item.Language) && !AvailableLanguages.Contains(item.Language))
                {
                    AvailableLanguages.Add(item.Language);
                }
            }

            CurrentPage = page.PageIndex;
            TotalPages = page.TotalPages;
            TotalItems = page.TotalCount;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task NextPageAsync()
    {
        if (CanNextPage)
        {
            await RefreshAsync(_currentPage + 1);
        }
    }

    public async Task PreviousPageAsync()
    {
        if (CanPreviousPage)
        {
            await RefreshAsync(_currentPage - 1);
        }
    }

    public async Task ToggleFavoriteAsync(DictationEntry entry)
    {
        if (entry == null) return;
        bool newState = !entry.IsFavorite;
        await _historyService.Repository.SetFavoriteAsync(entry.Id, newState);

        int idx = Entries.IndexOf(entry);
        if (idx >= 0)
        {
            Entries[idx] = entry with { IsFavorite = newState };
        }
    }

    public async Task DeleteEntryAsync(DictationEntry entry)
    {
        if (entry == null) return;
        await _historyService.Repository.SoftDeleteAsync(entry.Id);
        _lastDeletedEntry = entry;
        CanUndoDelete = true;
        Entries.Remove(entry);
        TotalItems = Math.Max(0, TotalItems - 1);
        StatusBannerMessage = "Dictation entry deleted.";
    }

    public async Task UndoLastDeleteAsync()
    {
        if (_lastDeletedEntry != null)
        {
            await _historyService.Repository.RestoreAsync(_lastDeletedEntry.Id);
            Entries.Insert(0, _lastDeletedEntry);
            _lastDeletedEntry = null;
            CanUndoDelete = false;
            TotalItems++;
            StatusBannerMessage = "Entry restored.";
        }
    }

    public void CopyToClipboard(DictationEntry entry)
    {
        if (entry?.Text != null)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetDataObject(entry.Text, true);
                    StatusBannerMessage = "Transcript copied to clipboard.";
                    return;
                }
                catch (System.Runtime.InteropServices.COMException) when (i < 4)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }
        }
    }

    public async Task<string> ExportAsync(string destinationPath, HistoryExportFormat format, bool filteredOnly = false)
    {
        string? appFilter = _selectedApplication is "All Applications" or "" ? null : _selectedApplication;
        string? langFilter = _selectedLanguage is "All Languages" or "" ? null : _selectedLanguage;

        var filter = filteredOnly
            ? new HistoryFilter(
                SearchQuery: string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery,
                Application: appFilter,
                Language: langFilter,
                IsFavorite: _onlyFavorites)
            : null;

        string result = await _historyService.Export.ExportAsync(destinationPath, format, filter, overwrite: true);
        StatusBannerMessage = $"Successfully exported {format} to {System.IO.Path.GetFileName(destinationPath)}";
        return result;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// ViewModel for Productivity Statistics & Insights (WF-042, WF-044).
/// Shows strictly real, non-fabricated metrics and deterministic insights.
/// </summary>
public sealed class StatisticsViewModel : INotifyPropertyChanged
{
    private readonly IHistoryService _historyService;
    private TimeRangeWindow _selectedWindow = TimeRangeWindow.SevenDays;
    private ProductivityMetrics? _metrics;
    private DailyStreakInfo? _streak;
    private ProductivityInsights? _insights;
    private bool _isLoading;

    public TimeRangeWindow SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (_selectedWindow != value)
            {
                _selectedWindow = value;
                OnPropertyChanged();
                _ = LoadAsync();
            }
        }
    }

    public ProductivityMetrics? Metrics
    {
        get => _metrics;
        private set
        {
            _metrics = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedTotalWords));
            OnPropertyChanged(nameof(FormattedTotalSessions));
            OnPropertyChanged(nameof(FormattedTotalCharacters));
            OnPropertyChanged(nameof(FormattedAverageWpm));
            OnPropertyChanged(nameof(FormattedActiveDuration));
            OnPropertyChanged(nameof(FormattedTimeSaved));
            OnPropertyChanged(nameof(TopApplicationsList));
            OnPropertyChanged(nameof(TopLanguagesList));
        }
    }

    public DailyStreakInfo? Streak
    {
        get => _streak;
        private set
        {
            _streak = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedCurrentStreak));
            OnPropertyChanged(nameof(FormattedLongestStreak));
        }
    }

    public ProductivityInsights? Insights
    {
        get => _insights;
        private set
        {
            _insights = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedMostActiveDay));
            OnPropertyChanged(nameof(FormattedMostUsedApp));
            OnPropertyChanged(nameof(FormattedMostUsedLang));
            OnPropertyChanged(nameof(FormattedLongestSession));
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    // Formatted KPI presentation properties
    public string FormattedTotalWords => _metrics?.TotalWords.ToString("N0") ?? "0";
    public string FormattedTotalSessions => _metrics?.TotalSessions.ToString("N0") ?? "0";
    public string FormattedTotalCharacters => _metrics?.TotalCharacters.ToString("N0") ?? "0";
    public string FormattedAverageWpm => $"{_metrics?.AverageWpm ?? 0:F1} WPM";

    public string FormattedActiveDuration
    {
        get
        {
            long ms = _metrics?.TotalActiveDurationMs ?? 0;
            var ts = TimeSpan.FromMilliseconds(ms);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m" : $"{ts.Minutes}m {ts.Seconds}s";
        }
    }

    /// <summary>
    /// Deterministic time-saved calculation benchmarked against 40 WPM average typing speed.
    /// Derived strictly from actual words spoken and active audio capture duration.
    /// </summary>
    public string FormattedTimeSaved
    {
        get
        {
            int words = _metrics?.TotalWords ?? 0;
            long ms = _metrics?.TotalActiveDurationMs ?? 0;
            double typingMinutes = words / 40.0;
            double speakingMinutes = ms / 60000.0;
            double savedMinutes = Math.Max(0.0, typingMinutes - speakingMinutes);
            var ts = TimeSpan.FromMinutes(savedMinutes);
            return ts.TotalHours >= 1 ? $"{(int)ts.TotalHours}h {ts.Minutes}m saved" : $"{ts.Minutes}m {ts.Seconds}s saved";
        }
    }

    public string FormattedCurrentStreak => $"{_streak?.CurrentStreak ?? 0} days";
    public string FormattedLongestStreak => $"{_streak?.LongestStreak ?? 0} days";

    public IReadOnlyList<KeyValuePair<string, int>> TopApplicationsList =>
        _metrics?.TopApplications.OrderByDescending(kv => kv.Value).ToList() ?? (IReadOnlyList<KeyValuePair<string, int>>)Array.Empty<KeyValuePair<string, int>>();

    public IReadOnlyList<KeyValuePair<string, int>> TopLanguagesList =>
        _metrics?.TopLanguages.OrderByDescending(kv => kv.Value).ToList() ?? (IReadOnlyList<KeyValuePair<string, int>>)Array.Empty<KeyValuePair<string, int>>();

    public string FormattedMostActiveDay => _insights?.MostActiveDay ?? "None recorded";
    public string FormattedMostUsedApp => _insights?.MostUsedApplication ?? "None recorded";
    public string FormattedMostUsedLang => _insights?.MostUsedLanguage ?? "None recorded";
    public string FormattedLongestSession => _insights != null && _insights.LongestSessionDuration > TimeSpan.Zero
        ? $"{(int)_insights.LongestSessionDuration.TotalMinutes}m {_insights.LongestSessionDuration.Seconds}s"
        : "0s";

    public StatisticsViewModel(IHistoryService historyService)
    {
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Metrics = await _historyService.Statistics.GetStatisticsAsync(_selectedWindow);
            Streak = await _historyService.Statistics.GetDailyStreakAsync();
            Insights = await _historyService.Statistics.GetProductivityInsightsAsync(_selectedWindow);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
