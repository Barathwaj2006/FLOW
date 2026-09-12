using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.History;

namespace Flow.Host.Windows.History;

/// <summary>
/// ViewModel for History list, search, pagination, and deletion with undo (WF-039, WF-040).
/// Native Windows desktop UX (Segoe UI, zero glassmorphism, zero fake numbers).
/// </summary>
public sealed class HistoryViewModel : INotifyPropertyChanged
{
    private readonly IHistoryService _historyService;
    private string _searchQuery = "";
    private string _selectedApplication = "";
    private string _selectedLanguage = "";
    private bool? _onlyFavorites;
    private int _currentPage = 0;
    private int _totalPages = 0;
    private int _totalItems = 0;
    private bool _isLoading;
    private DictationEntry? _lastDeletedEntry;
    private bool _canUndoDelete;

    public ObservableCollection<DictationEntry> Entries { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery != value)
            {
                _searchQuery = value;
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
        private set { _currentPage = value; OnPropertyChanged(); }
    }

    public int TotalPages
    {
        get => _totalPages;
        private set { _totalPages = value; OnPropertyChanged(); }
    }

    public int TotalItems
    {
        get => _totalItems;
        private set { _totalItems = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    public bool CanUndoDelete
    {
        get => _canUndoDelete;
        private set { _canUndoDelete = value; OnPropertyChanged(); }
    }

    public HistoryViewModel(IHistoryService historyService)
    {
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
    }

    public async Task RefreshAsync(int pageIndex = 0)
    {
        IsLoading = true;
        try
        {
            var filter = new HistoryFilter(
                SearchQuery: string.IsNullOrWhiteSpace(_searchQuery) ? null : _searchQuery,
                Application: string.IsNullOrWhiteSpace(_selectedApplication) ? null : _selectedApplication,
                Language: string.IsNullOrWhiteSpace(_selectedLanguage) ? null : _selectedLanguage,
                IsFavorite: _onlyFavorites
            );

            var page = await _historyService.Repository.GetPagedAsync(filter, pageIndex, 50);
            Entries.Clear();
            foreach (var item in page.Items)
            {
                Entries.Add(item);
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
        }
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
        private set { _metrics = value; OnPropertyChanged(); }
    }

    public DailyStreakInfo? Streak
    {
        get => _streak;
        private set { _streak = value; OnPropertyChanged(); }
    }

    public ProductivityInsights? Insights
    {
        get => _insights;
        private set { _insights = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

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
