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
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Scratchpad;
using Flow.Core.Session;
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
        : this(coordinator, capture, deviceManager, historyService, scratchpadService, null, null, null, null, null, null)
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
        StyleFormattingEngine? styleEngine) : this()
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
                await _coordinator.StartSessionAsync(isHandsFree: true);
                _capture.Start();
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

            // Populate applications filter if empty
            if (ComboHistoryAppFilter.Items.Count <= 1)
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
            if (dev.IsDefault || dev.Id == defaultId)
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
