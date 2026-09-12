using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Core.History;
using Microsoft.Win32;

namespace Flow.Host.Windows.History;

/// <summary>
/// Native Windows desktop window for Dictation History, Productivity Statistics, and Export (WF-039 through WF-045).
/// Built with WPF on .NET 9, strictly adhering to Offline Sovereignty, Zero-Enter invariant, and Content Lock.
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly HistoryViewModel _historyVm;
    private readonly StatisticsViewModel _statsVm;
    private bool _isInitializing = true;

    public HistoryViewModel HistoryViewModel => _historyVm;
    public StatisticsViewModel StatisticsViewModel => _statsVm;

    public HistoryWindow(HistoryViewModel historyVm, StatisticsViewModel statsVm)
    {
        InitializeComponent();

        _historyVm = historyVm ?? throw new ArgumentNullException(nameof(historyVm));
        _statsVm = statsVm ?? throw new ArgumentNullException(nameof(statsVm));

        DataContext = _historyVm;
        HistoryItemsControl.ItemsSource = _historyVm.Entries;

        _historyVm.PropertyChanged += OnHistoryVmPropertyChanged;
        _statsVm.PropertyChanged += OnStatsVmPropertyChanged;

        Loaded += HistoryWindow_Loaded;
    }

    private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _historyVm.PopulateFilterOptionsAsync();

            AppFilterComboBox.ItemsSource = _historyVm.AvailableApplications;
            AppFilterComboBox.SelectedIndex = 0;

            LangFilterComboBox.ItemsSource = _historyVm.AvailableLanguages;
            LangFilterComboBox.SelectedIndex = 0;

            _isInitializing = false;

            await _historyVm.RefreshAsync(0);
            await _statsVm.LoadAsync();

            UpdatePaginationUi();
            UpdateStatsUi();
        }
        catch (Exception ex)
        {
            GlobalStatusBarText.Text = $"Initialization error: {ex.Message}";
        }
    }

    private void OnHistoryVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(HistoryViewModel.IsEmpty):
                case nameof(HistoryViewModel.TotalItems):
                case nameof(HistoryViewModel.CurrentPage):
                    UpdatePaginationUi();
                    break;

                case nameof(HistoryViewModel.StatusBannerMessage):
                    if (!string.IsNullOrEmpty(_historyVm.StatusBannerMessage))
                    {
                        GlobalStatusBarText.Text = _historyVm.StatusBannerMessage;
                    }
                    break;
            }
        });
    }

    private void OnStatsVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateStatsUi);
    }

    private void UpdatePaginationUi()
    {
        PageSummaryText.Text = _historyVm.PageInfo;
        PrevPageButton.IsEnabled = _historyVm.CanPreviousPage;
        NextPageButton.IsEnabled = _historyVm.CanNextPage;

        if (_historyVm.IsEmpty)
        {
            EmptyStateBorder.Visibility = Visibility.Visible;
            EmptyStateSubtitle.Text = _historyVm.EmptyMessage;
        }
        else
        {
            EmptyStateBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateStatsUi()
    {
        KpiTotalWords.Text = _statsVm.FormattedTotalWords;
        KpiTotalSessions.Text = _statsVm.FormattedTotalSessions;
        KpiAverageWpm.Text = _statsVm.FormattedAverageWpm;
        KpiActiveDuration.Text = _statsVm.FormattedActiveDuration;
        KpiTimeSaved.Text = _statsVm.FormattedTimeSaved;
        KpiStreak.Text = _statsVm.FormattedCurrentStreak;
        KpiLongestStreak.Text = $"Longest streak: {_statsVm.FormattedLongestStreak}";

        TopAppsItemsControl.ItemsSource = _statsVm.TopApplicationsList;
        TopLangsItemsControl.ItemsSource = _statsVm.TopLanguagesList;

        InsightMostActiveDay.Text = _statsVm.FormattedMostActiveDay;
        InsightPrimaryApp.Text = _statsVm.FormattedMostUsedApp;
        InsightPrimaryLang.Text = _statsVm.FormattedMostUsedLang;
        InsightLongestSession.Text = _statsVm.FormattedLongestSession;
    }

    #region Search & Filtering Handlers

    private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (e.Key == Key.Enter)
        {
            _historyVm.SearchQuery = SearchTextBox.Text;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchTextBox.Text = "";
            SearchPlaceholder.Visibility = Visibility.Visible;
            _historyVm.SearchQuery = "";
            e.Handled = true;
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        SearchTextBox.Text = "";
        SearchPlaceholder.Visibility = Visibility.Visible;
        _historyVm.SearchQuery = "";
    }

    private void AppFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        string selected = AppFilterComboBox.SelectedItem as string ?? "All Applications";
        _historyVm.SelectedApplication = selected;
    }

    private void LangFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;
        string selected = LangFilterComboBox.SelectedItem as string ?? "All Languages";
        _historyVm.SelectedLanguage = selected;
    }

    private void FavoritesOnlyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        _historyVm.OnlyFavorites = FavoritesOnlyCheckBox.IsChecked == true ? true : null;
    }

    private async void RefreshHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        await _historyVm.RefreshAsync(_historyVm.CurrentPage);
    }

    #endregion

    #region Item Action Handlers

    private async void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DictationEntry entry)
        {
            await _historyVm.ToggleFavoriteAsync(entry);
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DictationEntry entry)
        {
            _historyVm.CopyToClipboard(entry);
            GlobalStatusBarText.Text = $"Copied {entry.WordCount} words to clipboard. [Zero-Enter: No keystrokes sent]";
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DictationEntry entry)
        {
            await _historyVm.DeleteEntryAsync(entry);
            UndoBannerText.Text = $"Dictation entry from {entry.CreatedAt:HH:mm:ss} deleted.";
            UndoBanner.Visibility = Visibility.Visible;
        }
    }

    private async void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        await _historyVm.UndoLastDeleteAsync();
        UndoBanner.Visibility = Visibility.Collapsed;
        GlobalStatusBarText.Text = "Deleted entry restored.";
    }

    private void DismissBannerButton_Click(object sender, RoutedEventArgs e)
    {
        UndoBanner.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Pagination Handlers

    private async void PrevPageButton_Click(object sender, RoutedEventArgs e)
    {
        await _historyVm.PreviousPageAsync();
    }

    private async void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
        await _historyVm.NextPageAsync();
    }

    #endregion

    #region Statistics Handlers

    private void TimeRangeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _statsVm == null) return;

        if (RangeTodayRadio.IsChecked == true)
            _statsVm.SelectedWindow = TimeRangeWindow.Today;
        else if (Range7DaysRadio.IsChecked == true)
            _statsVm.SelectedWindow = TimeRangeWindow.SevenDays;
        else if (Range30DaysRadio.IsChecked == true)
            _statsVm.SelectedWindow = TimeRangeWindow.ThirtyDays;
        else if (RangeAllTimeRadio.IsChecked == true)
            _statsVm.SelectedWindow = TimeRangeWindow.AllTime;
    }

    private async void RefreshStatsButton_Click(object sender, RoutedEventArgs e)
    {
        await _statsVm.LoadAsync();
    }

    #endregion

    #region Export Handlers

    private async void ExportExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            HistoryExportFormat format = HistoryExportFormat.Json;
            string filter = "JSON Files (*.json)|*.json";
            string defaultExt = ".json";

            if (FormatCsvRadio.IsChecked == true)
            {
                format = HistoryExportFormat.Csv;
                filter = "CSV Files (*.csv)|*.csv";
                defaultExt = ".csv";
            }
            else if (FormatTxtRadio.IsChecked == true)
            {
                format = HistoryExportFormat.PlainText;
                filter = "Plain Text Files (*.txt)|*.txt";
                defaultExt = ".txt";
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export FLOW Dictation History",
                Filter = filter,
                DefaultExt = defaultExt,
                FileName = $"flow_history_{DateTime.Now:yyyyMMdd_HHmmss}{defaultExt}"
            };

            if (dialog.ShowDialog(this) == true)
            {
                bool filteredOnly = ScopeFilteredRadio.IsChecked == true;
                string exportedPath = await _historyVm.ExportAsync(dialog.FileName, format, filteredOnly);

                ExportResultText.Text = $"Export successful: {Path.GetFileName(exportedPath)} ({format})\nFile saved to: {exportedPath}";
                ExportResultBanner.Visibility = Visibility.Visible;
                GlobalStatusBarText.Text = $"Exported to {exportedPath}";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Export failed: {ex.Message}", "FLOW Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
