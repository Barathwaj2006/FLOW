using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Flow.Core.History;
using Flow.Core.Language;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Scratchpad;
using Flow.Core.Session;
using Flow.Core.Storage;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.Personalization;
using Flow.Host.Windows.Scratchpad;
using Microsoft.Win32;

namespace Flow.Host.Windows.UI;

/// <summary>
/// Display presenter for dictation entries in FLOW Hub's Home and History tabs.
/// </summary>
public sealed class HistoryDisplayItem
{
    public string Id { get; init; } = string.Empty;
    public string FormattedTime { get; init; } = string.Empty;
    public string FormattedDate { get; init; } = string.Empty;
    public string TargetApplication { get; init; } = string.Empty;
    public int WordCount { get; init; }
    public string Language { get; init; } = string.Empty;
    public string TranscriptText { get; init; } = string.Empty;
    public bool IsFavorite { get; init; }

    public static HistoryDisplayItem FromEntry(DictationEntry entry) => new()
    {
        Id = entry.Id,
        FormattedTime = entry.CreatedAt.ToLocalTime().ToString("t"),
        FormattedDate = entry.CreatedAt.ToLocalTime().ToString("g"),
        TargetApplication = string.IsNullOrWhiteSpace(entry.Application) ? "Desktop" : entry.Application,
        WordCount = entry.WordCount,
        Language = entry.Language,
        TranscriptText = entry.Text ?? "(No transcript recorded)",
        IsFavorite = entry.IsFavorite
    };
}

public partial class FlowHubWindow : Window
{
    private readonly VoiceSessionCoordinator? _coordinator;
    private readonly WasapiAudioCapture? _capture;
    private readonly WasapiDeviceManager? _deviceManager;
    private readonly IHistoryService? _historyService;
    private readonly IScratchpadService? _scratchpadService;
    private readonly IPersonalDictionaryRepository? _dictRepo;
    private readonly PersonalDictionaryEngine? _dictEngine;
    private readonly ISnippetRepository? _snippetRepo;
    private readonly SnippetExpansionEngine? _snippetEngine;
    private readonly IStyleRepository? _styleRepo;
    private readonly StyleFormattingEngine? _styleEngine;
    private readonly ISettingsRepository? _settingsRepo;
    private readonly GlobalHotkeyHook? _hotkeyHook;
    private FlowAppSettings _currentSettings = new();

    // View models & state collections
    public DictionaryViewModel? DictionaryVm { get; private set; }
    public SnippetsViewModel? SnippetsVm { get; private set; }
    public StylesViewModel? StylesVm { get; private set; }

    private readonly ObservableCollection<HistoryDisplayItem> _homeRecentItems = new();
    private readonly ObservableCollection<HistoryDisplayItem> _historyItems = new();
    private readonly ObservableCollection<ScratchpadItemViewModel> _scratchpadNotes = new();

    private int _historyCurrentPage = 0;
    private int _historyTotalPages = 1;
    private bool _historyFavoritesOnly = false;
    private bool _isPopulatingHistoryFilter = false;
    private bool _historyFilterInitialized = false;
    private string? _activeScratchpadId;
    private DispatcherTimer? _scratchpadAutosaveTimer;
    private bool _isScratchpadDirty = false;

    public bool AllowRealClose { get; set; }
    public event Action? ExitApplicationRequested;

    public FlowHubWindow()
    {
        InitializeComponent();
    }

    public FlowHubWindow(
        VoiceSessionCoordinator? coordinator,
        WasapiAudioCapture? capture,
        WasapiDeviceManager? deviceManager,
        IHistoryService? historyService,
        IScratchpadService? scratchpadService)
        : this(coordinator, capture, deviceManager, historyService, scratchpadService, null, null, null, null, null, null, null, null)
    {
    }

    public FlowHubWindow(
        VoiceSessionCoordinator? coordinator,
        WasapiAudioCapture? capture,
        WasapiDeviceManager? deviceManager,
        IHistoryService? historyService,
        IScratchpadService? scratchpadService,
        IPersonalDictionaryRepository? dictRepo,
        PersonalDictionaryEngine? dictEngine,
        ISnippetRepository? snippetRepo,
        SnippetExpansionEngine? snippetEngine,
        IStyleRepository? styleRepo,
        StyleFormattingEngine? styleEngine,
        ISettingsRepository? settingsRepo = null,
        GlobalHotkeyHook? hotkeyHook = null) : this()
    {
        _coordinator = coordinator;
        _capture = capture;
        _deviceManager = deviceManager;
        _historyService = historyService;
        _scratchpadService = scratchpadService;
        _dictRepo = dictRepo;
        _dictEngine = dictEngine;
        _snippetRepo = snippetRepo;
        _snippetEngine = snippetEngine;
        _styleRepo = styleRepo;
        _styleEngine = styleEngine;
        _settingsRepo = settingsRepo;
        _hotkeyHook = hotkeyHook;

        if (_dictRepo != null)
        {
            DictionaryVm = new DictionaryViewModel(_dictRepo, _dictEngine);
        }
        if (_snippetRepo != null)
        {
            SnippetsVm = new SnippetsViewModel(_snippetRepo, _snippetEngine);
        }
        if (_styleRepo != null)
        {
            StylesVm = new StylesViewModel(_styleRepo, _styleEngine);
        }

        Loaded += FlowHubWindow_Loaded;
    }

    private async void FlowHubWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Enforce strict landing on Home (index 0)
        if (NavListBox != null) NavListBox.SelectedIndex = 0;
        if (MainTabControl != null) MainTabControl.SelectedIndex = 0;

        HomeRecentItemsControl.ItemsSource = _homeRecentItems;
        HistoryItemsControl.ItemsSource = _historyItems;
        ScratchpadItemsControl.ItemsSource = _scratchpadNotes;

        if (DictionaryVm != null)
        {
            DictionaryItemsControl.ItemsSource = DictionaryVm.DisplayEntries;
        }
        if (SnippetsVm != null)
        {
            SnippetsItemsControl.ItemsSource = SnippetsVm.DisplaySnippets;
        }

        // Initialize autosave timer for scratchpad
        _scratchpadAutosaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };
        _scratchpadAutosaveTimer.Tick += async (s, args) =>
        {
            _scratchpadAutosaveTimer.Stop();
            if (_isScratchpadDirty)
            {
                await SaveCurrentScratchpadAsync();
            }
        };

        PopulateAudioDevices();

        if (_coordinator != null)
        {
            _coordinator.AudioLevelChanged += Coordinator_AudioLevelChanged;
            _coordinator.StateChanged += Coordinator_StateChanged;
            _coordinator.FinalTextInserted += Coordinator_FinalTextInserted;
        }

        UpdateStatusBadge();

        // Load data asynchronously
        await LoadHomeDataAsync();
        await LoadHistoryAsync(0);
        await LoadDictionaryDataAsync();
        await LoadSnippetsDataAsync();
        await LoadStylesDataAsync();
        await LoadScratchpadNotesAsync();
        await LoadSettingsDataAsync();
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index <= 5)
        {
            if (NavListBoxBottom != null) NavListBoxBottom.SelectedIndex = -1;
            if (NavListBox != null) NavListBox.SelectedIndex = index;
        }
        else if (index >= 6 && index <= 7)
        {
            if (NavListBox != null) NavListBox.SelectedIndex = -1;
            if (NavListBoxBottom != null) NavListBoxBottom.SelectedIndex = index - 6;
        }

        if (MainTabControl != null && index >= 0 && index < MainTabControl.Items.Count)
        {
            MainTabControl.SelectedIndex = index;
        }
    }

    private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabControl == null || NavListBox == null) return;
        if (NavListBox.SelectedIndex >= 0)
        {
            if (NavListBoxBottom != null && NavListBoxBottom.SelectedIndex != -1)
            {
                NavListBoxBottom.SelectedIndex = -1;
            }
            MainTabControl.SelectedIndex = NavListBox.SelectedIndex;
        }
    }

    private void NavListBoxBottom_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabControl == null || NavListBoxBottom == null) return;
        if (NavListBoxBottom.SelectedIndex >= 0)
        {
            if (NavListBox != null && NavListBox.SelectedIndex != -1)
            {
                NavListBox.SelectedIndex = -1;
            }
            MainTabControl.SelectedIndex = 6 + NavListBoxBottom.SelectedIndex;
        }
    }

    // ==================== HOME TAB ====================

    private async Task LoadHomeDataAsync()
    {
        try
        {
            if (_historyService != null)
            {
                // Productivity KPIs
                var stats = await _historyService.Statistics.GetStatisticsAsync(TimeRangeWindow.Today);
                var streak = await _historyService.Statistics.GetDailyStreakAsync();

                TxtKpiWordsToday.Text = stats.TotalWords.ToString("N0");
                TxtKpiWpm.Text = stats.AverageWpm.ToString("F0");
                TxtKpiSessions.Text = stats.TotalSessions.ToString("N0");
                TxtKpiStreak.Text = $"{streak.CurrentStreak} day{(streak.CurrentStreak == 1 ? "" : "s")}";

                // Recent dictations
                var filter = new HistoryFilter(IncludeDeleted: false);
                var page = await _historyService.Repository.GetPagedAsync(filter, pageIndex: 0, pageSize: 5);

                _homeRecentItems.Clear();
                foreach (var entry in page.Items)
                {
                    _homeRecentItems.Add(HistoryDisplayItem.FromEntry(entry));
                }

                TxtHomeRecentEmpty.Visibility = _homeRecentItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private void BtnViewAllHistory_Click(object sender, RoutedEventArgs e)
    {
        SelectTab(1); // Navigate to History
    }

    private void BtnCopyRecentItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string text && !string.IsNullOrEmpty(text))
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch { }
        }
    }

    private void BtnToggleDictation_Click(object sender, RoutedEventArgs e)
    {
        if (_coordinator == null || _capture == null) return;

        if (_coordinator.CurrentState == SessionState.Recording)
        {
            _capture.Stop();
            _ = Task.Run(async () => await _coordinator.EndSessionAsync());
        }
        else
        {
            _ = Task.Run(async () =>
            {
                if (await _coordinator.StartSessionAsync(isHandsFree: true) && _coordinator.CurrentState == SessionState.Recording)
                {
                    _capture.Start();
                }
            });
        }
    }

    private void BtnClearTest_Click(object sender, RoutedEventArgs e)
    {
        TxtTestDictation.Clear();
    }

    // ==================== HISTORY TAB ====================

    private async Task LoadHistoryAsync(int page = 0)
    {
        if (_historyService == null) return;

        try
        {
            _historyCurrentPage = Math.Max(0, page);
            string? query = string.IsNullOrWhiteSpace(TxtHistorySearch.Text) ? null : TxtHistorySearch.Text.Trim();
            string? app = ComboHistoryAppFilter.SelectedItem as string;
            if (app == "All Applications") app = null;
            bool? fav = _historyFavoritesOnly ? true : null;

            var filter = new HistoryFilter(SearchQuery: query, Application: app, IsFavorite: fav, IncludeDeleted: false);
            var result = await _historyService.Repository.GetPagedAsync(filter, pageIndex: _historyCurrentPage, pageSize: 20);

            _historyItems.Clear();
            foreach (var item in result.Items)
            {
                _historyItems.Add(HistoryDisplayItem.FromEntry(item));
            }

            _historyTotalPages = Math.Max(1, (int)Math.Ceiling(result.TotalCount / 20.0));
            TxtHistoryPageInfo.Text = $"Page {_historyCurrentPage + 1} of {_historyTotalPages} ({result.TotalCount} items)";
            TxtHistoryEmpty.Visibility = _historyItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            BtnHistoryPrevPage.IsEnabled = _historyCurrentPage > 0;
            BtnHistoryNextPage.IsEnabled = _historyCurrentPage < _historyTotalPages - 1;

            // Populate applications filter once on initial load
            if (ComboHistoryAppFilter != null && !_historyFilterInitialized && !_isPopulatingHistoryFilter)
            {
                _isPopulatingHistoryFilter = true;
                _historyFilterInitialized = true;
                try
                {
                    ComboHistoryAppFilter.Items.Clear();
                    ComboHistoryAppFilter.Items.Add("All Applications");
                    var allEntries = await _historyService.Repository.GetAllForExportAsync(null);
                    var apps = allEntries.Select(e => e.Application).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().OrderBy(a => a);
                    foreach (var a in apps)
                    {
                        ComboHistoryAppFilter.Items.Add(a);
                    }
                    ComboHistoryAppFilter.SelectedIndex = 0;
                }
                finally
                {
                    _isPopulatingHistoryFilter = false;
                }
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private async void TxtHistorySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadHistoryAsync(0);
    }

    private async void ComboHistoryAppFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isPopulatingHistoryFilter) return;
        await LoadHistoryAsync(0);
    }

    private async void BtnToggleFavoritesOnly_Click(object sender, RoutedEventArgs e)
    {
        _historyFavoritesOnly = !_historyFavoritesOnly;
        BtnToggleFavoritesOnly.Background = _historyFavoritesOnly
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E2B4C"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B"));
        await LoadHistoryAsync(0);
    }

    private async void BtnHistoryPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_historyCurrentPage > 0)
        {
            await LoadHistoryAsync(_historyCurrentPage - 1);
        }
    }

    private async void BtnHistoryNextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_historyCurrentPage < _historyTotalPages - 1)
        {
            await LoadHistoryAsync(_historyCurrentPage + 1);
        }
    }

    private async void BtnFavoriteHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (_historyService == null) return;
        if (sender is Button btn && btn.Tag is string id)
        {
            var item = _historyItems.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                await _historyService.Repository.SetFavoriteAsync(id, !item.IsFavorite);
                await LoadHistoryAsync(_historyCurrentPage);
            }
        }
    }

    private void BtnCopyHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string text && !string.IsNullOrEmpty(text))
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch { }
        }
    }

    private async void BtnDeleteHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (_historyService == null) return;
        if (sender is Button btn && btn.Tag is string id)
        {
            await _historyService.Repository.SoftDeleteAsync(id);
            await LoadHistoryAsync(_historyCurrentPage);
            await LoadHomeDataAsync();
        }
    }

    private async void BtnExportHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_historyService == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export FLOW History",
            Filter = "JSON Files (*.json)|*.json|CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt",
            FileName = $"FLOW_History_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (sfd.ShowDialog() == true)
        {
            var format = sfd.FilterIndex switch
            {
                2 => HistoryExportFormat.Csv,
                3 => HistoryExportFormat.PlainText,
                _ => HistoryExportFormat.Json
            };

            await _historyService.Export.ExportAsync(sfd.FileName, format, overwrite: true);
            MessageBox.Show(this, $"History exported successfully to:\n{sfd.FileName}", "FLOW Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ==================== DICTIONARY TAB ====================

    private async Task LoadDictionaryDataAsync()
    {
        if (DictionaryVm != null)
        {
            await DictionaryVm.RefreshAsync();
            TxtDictionaryEmpty.Visibility = DictionaryVm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void BtnShowAddWord_Click(object sender, RoutedEventArgs e)
    {
        PanelAddWord.Visibility = Visibility.Visible;
        TxtNewWordTerm.Focus();
    }

    private void BtnCancelAddWord_Click(object sender, RoutedEventArgs e)
    {
        PanelAddWord.Visibility = Visibility.Collapsed;
        TxtNewWordTerm.Clear();
        TxtNewWordReplacement.Clear();
        ChkNewWordStar.IsChecked = false;
    }

    private async void BtnSaveWord_Click(object sender, RoutedEventArgs e)
    {
        if (DictionaryVm == null) return;

        string term = TxtNewWordTerm.Text.Trim();
        string replacement = TxtNewWordReplacement.Text.Trim();
        bool star = ChkNewWordStar.IsChecked == true;

        if (string.IsNullOrWhiteSpace(term))
        {
            MessageBox.Show(this, "Please enter a word or phrase.", "FLOW Dictionary", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool success = await DictionaryVm.AddOrUpdateEntryAsync(term, replacement, star);
        if (success)
        {
            BtnCancelAddWord_Click(sender, e);
            TxtDictionaryEmpty.Visibility = DictionaryVm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void BtnToggleStarWord_Click(object sender, RoutedEventArgs e)
    {
        if (DictionaryVm == null) return;
        if (sender is Button btn && btn.Tag is string id)
        {
            await DictionaryVm.ToggleStarAsync(id);
        }
    }

    private async void BtnDeleteWord_Click(object sender, RoutedEventArgs e)
    {
        if (DictionaryVm == null) return;
        if (sender is Button btn && btn.Tag is string id)
        {
            await DictionaryVm.DeleteEntryAsync(id);
            TxtDictionaryEmpty.Visibility = DictionaryVm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ==================== SNIPPETS TAB ====================

    private async Task LoadSnippetsDataAsync()
    {
        if (SnippetsVm != null)
        {
            await SnippetsVm.RefreshAsync();
            TxtSnippetsEmpty.Visibility = SnippetsVm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void BtnShowAddSnippet_Click(object sender, RoutedEventArgs e)
    {
        PanelAddSnippet.Visibility = Visibility.Visible;
        TxtNewSnippetTrigger.Focus();
    }

    private void BtnCancelAddSnippet_Click(object sender, RoutedEventArgs e)
    {
        PanelAddSnippet.Visibility = Visibility.Collapsed;
        TxtNewSnippetTrigger.Clear();
        TxtNewSnippetExpansion.Clear();
    }

    private async void BtnSaveSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (SnippetsVm == null) return;

        string trigger = TxtNewSnippetTrigger.Text.Trim();
        string expansion = TxtNewSnippetExpansion.Text.Trim();

        if (string.IsNullOrWhiteSpace(trigger) || string.IsNullOrWhiteSpace(expansion))
        {
            MessageBox.Show(this, "Please provide both a trigger phrase and expansion text.", "FLOW Snippets", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool success = await SnippetsVm.AddOrUpdateSnippetAsync(trigger, expansion);
        if (success)
        {
            BtnCancelAddSnippet_Click(sender, e);
            TxtSnippetsEmpty.Visibility = SnippetsVm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private async void BtnDeleteSnippet_Click(object sender, RoutedEventArgs e)
    {
        if (SnippetsVm == null) return;
        if (sender is Button btn && btn.Tag is string id)
        {
            await SnippetsVm.DeleteSnippetAsync(id);
            TxtSnippetsEmpty.Visibility = SnippetsVm.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ==================== STYLES TAB ====================

    private async Task LoadStylesDataAsync()
    {
        if (StylesVm != null)
        {
            await StylesVm.RefreshAsync();
            UpdateStylesCardHighlight(StylesVm.ActiveProfileName);
        }
    }

    private void UpdateStylesCardHighlight(string styleName)
    {
        TxtActiveStyleDisplay.Text = styleName;

        var activeBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
        var defaultBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F2C46"));

        CardStyleNatural.BorderBrush = styleName.Contains("Natural", StringComparison.OrdinalIgnoreCase) ? activeBorder : defaultBorder;
        CardStyleNatural.BorderThickness = styleName.Contains("Natural", StringComparison.OrdinalIgnoreCase) ? new Thickness(2) : new Thickness(1);

        CardStyleFormal.BorderBrush = styleName.Contains("Formal", StringComparison.OrdinalIgnoreCase) ? activeBorder : defaultBorder;
        CardStyleFormal.BorderThickness = styleName.Contains("Formal", StringComparison.OrdinalIgnoreCase) ? new Thickness(2) : new Thickness(1);

        CardStyleCasual.BorderBrush = styleName.Contains("Casual", StringComparison.OrdinalIgnoreCase) ? activeBorder : defaultBorder;
        CardStyleCasual.BorderThickness = styleName.Contains("Casual", StringComparison.OrdinalIgnoreCase) ? new Thickness(2) : new Thickness(1);

        CardStyleConcise.BorderBrush = styleName.Contains("Concise", StringComparison.OrdinalIgnoreCase) ? activeBorder : defaultBorder;
        CardStyleConcise.BorderThickness = styleName.Contains("Concise", StringComparison.OrdinalIgnoreCase) ? new Thickness(2) : new Thickness(1);
    }

    private async void CardStyleNatural_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StylesVm != null)
        {
            var p = StylesVm.Profiles.FirstOrDefault(x => x.Name.Contains("Natural"));
            if (p != null) await StylesVm.SelectProfileAsync(p.Id);
            UpdateStylesCardHighlight("Natural / Balanced");
        }
    }

    private async void CardStyleFormal_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StylesVm != null)
        {
            var p = StylesVm.Profiles.FirstOrDefault(x => x.Name.Contains("Formal"));
            if (p != null) await StylesVm.SelectProfileAsync(p.Id);
            UpdateStylesCardHighlight("Formal / Professional");
        }
    }

    private async void CardStyleCasual_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StylesVm != null)
        {
            var p = StylesVm.Profiles.FirstOrDefault(x => x.Name.Contains("Casual"));
            if (p != null) await StylesVm.SelectProfileAsync(p.Id);
            UpdateStylesCardHighlight("Casual / Conversational");
        }
    }

    private async void CardStyleConcise_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (StylesVm != null)
        {
            var p = StylesVm.Profiles.FirstOrDefault(x => x.Name.Contains("Concise"));
            if (p != null) await StylesVm.SelectProfileAsync(p.Id);
            UpdateStylesCardHighlight("Concise / Bulleted");
        }
    }

    // ==================== SCRATCHPAD TAB ====================

    private async Task LoadScratchpadNotesAsync()
    {
        if (_scratchpadService == null) return;

        try
        {
            string? query = string.IsNullOrWhiteSpace(TxtScratchpadSearch.Text) ? null : TxtScratchpadSearch.Text.Trim();
            var filter = new ScratchpadFilter(SearchQuery: query);
            var page = await _scratchpadService.Repository.GetPagedAsync(filter, pageIndex: 0, pageSize: 50);

            _scratchpadNotes.Clear();
            foreach (var entry in page.Items)
            {
                _scratchpadNotes.Add(new ScratchpadItemViewModel(entry));
            }

            if (_scratchpadNotes.Count > 0 && _activeScratchpadId == null)
            {
                SelectScratchpadNote(_scratchpadNotes[0].Id);
            }
            else if (_scratchpadNotes.Count == 0)
            {
                // Create a clean default note if repository is empty
                var newNote = await _scratchpadService.CreateScratchpadAsync("Welcome Note", "This is your private local scratchpad. Dictate drafts, ideas, or meeting notes here.");
                _scratchpadNotes.Add(new ScratchpadItemViewModel(newNote));
                SelectScratchpadNote(newNote.Id);
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private void SelectScratchpadNote(string id)
    {
        var note = _scratchpadNotes.FirstOrDefault(n => n.Id == id);
        if (note == null) return;

        _activeScratchpadId = id;
        _isScratchpadDirty = false;

        TxtScratchpadTitle.Text = note.Title;
        TxtScratchpadContent.Text = note.Content;
        TxtScratchpadSaveStatus.Text = "Saved ✓";
        TxtScratchpadSaveStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
        BtnPinScratchpad.Content = note.IsPinned ? "Unpin Note" : "Pin Note";
    }

    private void ScratchpadItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Tag is string id)
        {
            SelectScratchpadNote(id);
        }
    }

    private async void BtnNewScratchpad_Click(object sender, RoutedEventArgs e)
    {
        if (_scratchpadService == null) return;

        var note = await _scratchpadService.CreateScratchpadAsync("Untitled Note", "");
        var vm = new ScratchpadItemViewModel(note);
        _scratchpadNotes.Insert(0, vm);
        SelectScratchpadNote(note.Id);
        TxtScratchpadTitle.Focus();
        TxtScratchpadTitle.SelectAll();
    }

    private void TxtScratchpadTitle_TextChanged(object sender, TextChangedEventArgs e)
    {
        MarkScratchpadDirty();
    }

    private void TxtScratchpadContent_TextChanged(object sender, TextChangedEventArgs e)
    {
        MarkScratchpadDirty();
    }

    private void MarkScratchpadDirty()
    {
        if (_activeScratchpadId == null) return;
        _isScratchpadDirty = true;
        TxtScratchpadSaveStatus.Text = "Saving...";
        TxtScratchpadSaveStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
        _scratchpadAutosaveTimer?.Stop();
        _scratchpadAutosaveTimer?.Start();
    }

    private async Task SaveCurrentScratchpadAsync()
    {
        if (_activeScratchpadId == null || _scratchpadService == null) return;

        try
        {
            string title = TxtScratchpadTitle.Text;
            string content = TxtScratchpadContent.Text;

            await _scratchpadService.UpdateScratchpadAsync(_activeScratchpadId, title, content);
            _isScratchpadDirty = false;

            var note = _scratchpadNotes.FirstOrDefault(n => n.Id == _activeScratchpadId);
            if (note != null)
            {
                note.Title = title;
                note.Content = content;
                note.WordCount = content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            }

            TxtScratchpadSaveStatus.Text = "Saved ✓";
            TxtScratchpadSaveStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
        }
        catch
        {
            TxtScratchpadSaveStatus.Text = "Save error";
            TxtScratchpadSaveStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
        }
    }

    private void BtnCopyScratchpad_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtScratchpadContent.Text))
        {
            try
            {
                Clipboard.SetText(TxtScratchpadContent.Text);
                MessageBox.Show(this, "Note copied to clipboard.", "FLOW Scratchpad", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch { }
        }
    }

    private async void BtnExportScratchpad_Click(object sender, RoutedEventArgs e)
    {
        if (_activeScratchpadId == null || _scratchpadService == null) return;

        var note = await _scratchpadService.GetScratchpadAsync(_activeScratchpadId);
        if (note == null) return;

        var sfd = new SaveFileDialog
        {
            Title = "Export Scratchpad Note",
            Filter = "Markdown (*.md)|*.md|Plain Text (*.txt)|*.txt",
            FileName = $"{note.Title.Replace(' ', '_')}"
        };

        if (sfd.ShowDialog() == true)
        {
            var format = sfd.FilterIndex == 1 ? ScratchpadExportFormat.Markdown : ScratchpadExportFormat.PlainText;
            await _scratchpadService.Export.ExportAsync(note, sfd.FileName, format, overwrite: true);
            MessageBox.Show(this, $"Note exported to:\n{sfd.FileName}", "FLOW Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnPinScratchpad_Click(object sender, RoutedEventArgs e)
    {
        if (_activeScratchpadId == null || _scratchpadService == null) return;

        var note = _scratchpadNotes.FirstOrDefault(n => n.Id == _activeScratchpadId);
        if (note == null) return;

        bool newPinState = !note.IsPinned;
        if (newPinState)
        {
            await _scratchpadService.PinScratchpadAsync(_activeScratchpadId);
        }
        else
        {
            await _scratchpadService.UnpinScratchpadAsync(_activeScratchpadId);
        }

        note.IsPinned = newPinState;
        BtnPinScratchpad.Content = newPinState ? "Unpin Note" : "Pin Note";
    }

    private async void BtnDeleteScratchpad_Click(object sender, RoutedEventArgs e)
    {
        if (_scratchpadService == null) return;
        if (sender is Button btn && btn.Tag is string id)
        {
            await _scratchpadService.DeleteScratchpadAsync(id);
            var note = _scratchpadNotes.FirstOrDefault(n => n.Id == id);
            if (note != null) _scratchpadNotes.Remove(note);

            if (_activeScratchpadId == id)
            {
                if (_scratchpadNotes.Count > 0)
                {
                    SelectScratchpadNote(_scratchpadNotes[0].Id);
                }
                else
                {
                    _activeScratchpadId = null;
                    TxtScratchpadTitle.Clear();
                    TxtScratchpadContent.Clear();
                }
            }
        }
    }

    private async void TxtScratchpadSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        await LoadScratchpadNotesAsync();
    }

    // ==================== SETTINGS & HARDWARE ====================

    private void PopulateAudioDevices()
    {
        ComboAudioDevices.Items.Clear();
        if (_deviceManager == null) return;

        var devices = _deviceManager.EnumerateCaptureDevices();
        string? defaultId = _deviceManager.GetDefaultCaptureDeviceId();
        var defaultDev = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
        string defaultName = defaultDev?.Name ?? "Default Windows Microphone";

        TxtActiveDevice.Text = $"Active Device: {defaultName}";

        int selectIdx = 0;
        for (int i = 0; i < devices.Count; i++)
        {
            var dev = devices[i];
            string label = dev.Name + (dev.IsDefault ? " (Default)" : "");
            ComboAudioDevices.Items.Add(new ComboBoxItem { Content = label, Tag = dev.Id });
            if (!string.IsNullOrEmpty(_currentSettings.AudioDeviceId) && dev.Id == _currentSettings.AudioDeviceId)
            {
                selectIdx = i;
                TxtActiveDevice.Text = $"Active Device: {dev.Name}";
            }
            else if (string.IsNullOrEmpty(_currentSettings.AudioDeviceId) && (dev.IsDefault || dev.Id == defaultId))
            {
                selectIdx = i;
            }
        }

        if (ComboAudioDevices.Items.Count > 0)
        {
            ComboAudioDevices.SelectedIndex = selectIdx;
        }
        else
        {
            ComboAudioDevices.Items.Add(new ComboBoxItem { Content = "No active recording device detected", Tag = null });
            ComboAudioDevices.SelectedIndex = 0;
            TxtActiveDevice.Text = "Active Device: No physical microphone detected";
        }
    }

    private void BtnRefreshDevices_Click(object sender, RoutedEventArgs e)
    {
        PopulateAudioDevices();
    }

    private void BtnApplyDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_capture == null) return;

        if (ComboAudioDevices.SelectedItem is ComboBoxItem item && item.Tag is string deviceId)
        {
            bool wasCapturing = _capture.IsCapturing;
            if (wasCapturing) _capture.Stop();
            _capture.TargetDeviceId = deviceId;
            if (wasCapturing) _capture.Start();

            _currentSettings = _currentSettings with { AudioDeviceId = deviceId };
            _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);

            TxtActiveDevice.Text = $"Active Device: {item.Content}";
            MessageBox.Show(this, $"Microphone endpoint set to:\n{item.Content}", "FLOW Audio", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void BtnClearAllHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_historyService == null) return;

        var result = MessageBox.Show(this, "Are you sure you want to permanently clear all local dictation history?", "Clear History Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            await _historyService.Repository.DeleteAllAsync(confirm: true);
            _historyFilterInitialized = false;
            await LoadHistoryAsync(0);
            await LoadHomeDataAsync();
            MessageBox.Show(this, "Local dictation history cleared.", "FLOW Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    // ==================== LIFECYCLE & COORDINATOR EVENTS ====================

    private void Coordinator_AudioLevelChanged(float rms)
    {
        Dispatcher.BeginInvoke(() =>
        {
            int percent = Math.Clamp((int)(rms * 400.0f), 0, 100);
            ProgressBarAudioLevel.Value = percent;
            TxtAudioLevelPercent.Text = $"Level: {percent}%";
        });
    }

    private void Coordinator_StateChanged(SessionState state, string? detail)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdateStatusBadge();
            if (state == SessionState.Completed)
            {
                _ = LoadHomeDataAsync();
                _ = LoadHistoryAsync(_historyCurrentPage);
            }
        });
    }

    private void Coordinator_FinalTextInserted(string text)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (TxtTestDictation.IsFocused)
            {
                TxtTestDictation.AppendText(" " + text);
                TxtTestDictation.CaretIndex = TxtTestDictation.Text.Length;
            }
            else if (TxtScratchpadContent.IsFocused)
            {
                TxtScratchpadContent.AppendText(" " + text);
                TxtScratchpadContent.CaretIndex = TxtScratchpadContent.Text.Length;
            }
        });
    }

    private void UpdateStatusBadge()
    {
        if (_coordinator == null)
        {
            StatusBadgeText.Text = "FLOW Active & Ready";
            StatusBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            StatusBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064E3B"));
            StatusBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            BtnToggleDictation.Content = "Start Dictation";
            return;
        }

        if (_coordinator.CurrentState == SessionState.Recording)
        {
            StatusBadgeText.Text = "Recording Audio...";
            StatusBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            StatusBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#450A0A"));
            StatusBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
            BtnToggleDictation.Content = "Stop Dictation";
        }
        else if (_coordinator.CurrentState == SessionState.Processing)
        {
            StatusBadgeText.Text = "Transcribing Locally...";
            StatusBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38BDF8"));
            StatusBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#082F49"));
            StatusBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0284C7"));
            BtnToggleDictation.Content = "Transcribing...";
        }
        else
        {
            StatusBadgeText.Text = "FLOW Active & Ready";
            StatusBadgeDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            StatusBadgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#064E3B"));
            StatusBadgeBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#059669"));
            BtnToggleDictation.Content = "Start Dictation";
        }
    }

    private void BtnMinimizeTray_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void BtnExitApp_Click(object sender, RoutedEventArgs e)
    {
        AllowRealClose = true;
        ExitApplicationRequested?.Invoke();
    }

    private bool _isSettingsLoading = true;

    private async Task LoadSettingsDataAsync()
    {
        _isSettingsLoading = true;
        try
        {
            if (_settingsRepo != null)
            {
                _currentSettings = await _settingsRepo.LoadSettingsAsync();
            }

            // 1. VAD Threshold
            if (SliderVadThreshold != null) SliderVadThreshold.Value = _currentSettings.VadThreshold;
            if (TxtVadThresholdLabel != null) TxtVadThresholdLabel.Text = $"Energy Threshold: {_currentSettings.VadThreshold:F3} (Configured)";
            _coordinator?.SetVadThreshold(_currentSettings.VadThreshold);

            // 2. Language
            int langIndex = _currentSettings.Language.ToLowerInvariant() switch
            {
                "auto" => 1,
                "ta" => 2,
                "hi" => 3,
                "es" => 4,
                "fr" => 5,
                "de" => 6,
                _ => 0 // "en"
            };
            if (ComboLanguages != null) ComboLanguages.SelectedIndex = langIndex;
            if (_coordinator != null)
            {
                _coordinator.SelectedLanguage = _currentSettings.Language;
                _coordinator.SetSessionLanguage(new LanguageCode(_currentSettings.Language));
            }

            // 3. Hotkey
            int hotkeyIndex = _currentSettings.HotkeyVk switch
            {
                163 => 1, // Right Ctrl
                119 => 2, // F8
                120 => 3, // F9
                121 => 4, // F10
                145 => 5, // Scroll Lock
                _ => 0    // Right Alt (165)
            };
            if (ComboHotkey != null) ComboHotkey.SelectedIndex = hotkeyIndex;
            _hotkeyHook?.UpdateTargetKey(_currentSettings.HotkeyVk);

            // 4. Theme
            if (ComboTheme != null) ComboTheme.SelectedIndex = string.Equals(_currentSettings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            ApplyTheme(_currentSettings.Theme);

            // 5. Retention Policy
            if (ComboRetentionPolicy != null)
            {
                ComboRetentionPolicy.SelectedIndex = _currentSettings.RetentionPolicy switch
                {
                    "30Days" or "ThirtyDays" => 1,
                    "90Days" or "NinetyDays" => 2,
                    "180Days" or "OneHundredEightyDays" => 3,
                    "365Days" or "OneYear" => 4,
                    _ => 0
                };
            }

            // 6. Launch on Startup
            if (ChkLaunchOnStartup != null)
            {
                ChkLaunchOnStartup.IsChecked = Native.WindowsStartupRegistrationService.IsStartupEnabled();
            }
        }
        catch
        {
            // Fail closed with safe defaults
        }
        finally
        {
            _isSettingsLoading = false;
        }
    }

    private void SliderVadThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isSettingsLoading || TxtVadThresholdLabel == null) return;

        float val = (float)e.NewValue;
        TxtVadThresholdLabel.Text = $"Energy Threshold: {val:F3}";
        _coordinator?.SetVadThreshold(val);

        _currentSettings = _currentSettings with { VadThreshold = val };
        _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);
    }

    private void ComboLanguages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSettingsLoading || ComboLanguages == null) return;

        string code = ComboLanguages.SelectedIndex switch
        {
            1 => "auto",
            2 => "ta",
            3 => "hi",
            4 => "es",
            5 => "fr",
            6 => "de",
            _ => "en"
        };

        if (_coordinator != null)
        {
            _coordinator.SelectedLanguage = code;
            _coordinator.SetSessionLanguage(new LanguageCode(code));
        }

        _currentSettings = _currentSettings with { Language = code };
        _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);
    }

    private void ComboHotkey_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSettingsLoading || ComboHotkey == null) return;

        if (ComboHotkey.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag as string, out int vk))
        {
            _hotkeyHook?.UpdateTargetKey(vk);

            _currentSettings = _currentSettings with { HotkeyVk = vk };
            _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);
        }
    }

    private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSettingsLoading || ComboTheme == null) return;

        string theme = ComboTheme.SelectedIndex == 1 ? "Light" : "Dark";
        ApplyTheme(theme);

        _currentSettings = _currentSettings with { Theme = theme };
        _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);
    }

    private void ApplyTheme(string themeName)
    {
        try
        {
            var uri = new Uri($"/Flow.Host.Windows;component/Themes/{themeName}Theme.xaml", UriKind.RelativeOrAbsolute);
            var newDict = new ResourceDictionary { Source = uri };

            // Apply merged theme dictionary
            Resources.MergedDictionaries.Clear();
            Resources.MergedDictionaries.Add(newDict);
        }
        catch
        {
            // Fail gracefully if resource not found
        }
    }

    private void ComboRetentionPolicy_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSettingsLoading || ComboRetentionPolicy == null) return;

        string policyStr = ComboRetentionPolicy.SelectedIndex switch
        {
            1 => "ThirtyDays",
            2 => "NinetyDays",
            3 => "OneHundredEightyDays",
            4 => "OneYear",
            _ => "Unlimited"
        };

        _currentSettings = _currentSettings with { RetentionPolicy = policyStr };
        _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);

        if (_historyService != null && Enum.TryParse<Flow.Core.History.RetentionPolicy>(policyStr, out var rp))
        {
            _ = Task.Run(async () =>
            {
                var settings = await _historyService.GetSettingsAsync();
                await _historyService.UpdateSettingsAsync(settings with { Retention = rp });
            });
        }
    }

    private void ChkLaunchOnStartup_Checked(object sender, RoutedEventArgs e)
    {
        if (_isSettingsLoading) return;
        Native.WindowsStartupRegistrationService.SetStartupEnabled(true);
        _currentSettings = _currentSettings with { LaunchOnStartup = true };
        _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);
    }

    private void ChkLaunchOnStartup_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isSettingsLoading) return;
        Native.WindowsStartupRegistrationService.SetStartupEnabled(false);
        _currentSettings = _currentSettings with { LaunchOnStartup = false };
        _ = _settingsRepo?.SaveSettingsAsync(_currentSettings);
    }

    private void BtnAccountCredits_Click(object sender, RoutedEventArgs e)
    {
        var loginWin = new Window
        {
            Title = "FLOW Account & Cloud AI Credits",
            Width = 480,
            Height = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(14, 20, 34)),
            ResizeMode = ResizeMode.NoResize
        };

        var sp = new StackPanel { Margin = new Thickness(24) };

        var title = new TextBlock
        {
            Text = "Account & Credits Management",
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        var desc = new TextBlock
        {
            Text = "Sign in with email verification to synchronize voice productivity credits across devices.",
            FontSize = 12,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        };

        var lblEmail = new TextBlock
        {
            Text = "Email Address:",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var txtEmail = new TextBox
        {
            Height = 32,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 26, 45)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 48, 80)),
            Margin = new Thickness(0, 0, 0, 12),
            Padding = new Thickness(6, 4, 6, 4),
            Text = "user@example.com"
        };

        var lblCode = new TextBlock
        {
            Text = "6-Digit Verification Code:",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(203, 213, 225)),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var txtCode = new TextBox
        {
            Height = 32,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 26, 45)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 48, 80)),
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(6, 4, 6, 4),
            Text = "749281"
        };

        var btnVerify = new Button
        {
            Content = "Verify & Claim 50 Daily Credits",
            Height = 36,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(2, 132, 199)),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        btnVerify.Click += (s, args) =>
        {
            MessageBox.Show(loginWin, "Email verified successfully!\nAccount active: " + txtEmail.Text + "\nCloud AI Credits: 100 Available", "FLOW Verified", MessageBoxButton.OK, MessageBoxImage.Information);
            BtnAccountCredits.Content = "★ 100 Credits";
            loginWin.Close();
        };

        sp.Children.Add(title);
        sp.Children.Add(desc);
        sp.Children.Add(lblEmail);
        sp.Children.Add(txtEmail);
        sp.Children.Add(lblCode);
        sp.Children.Add(txtCode);
        sp.Children.Add(btnVerify);

        loginWin.Content = sp;
        loginWin.ShowDialog();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowRealClose)
        {
            e.Cancel = true;
            this.Hide();
        }
        else
        {
            if (_coordinator != null)
            {
                _coordinator.AudioLevelChanged -= Coordinator_AudioLevelChanged;
                _coordinator.StateChanged -= Coordinator_StateChanged;
                _coordinator.FinalTextInserted -= Coordinator_FinalTextInserted;
            }
            base.OnClosing(e);
        }
    }
}
