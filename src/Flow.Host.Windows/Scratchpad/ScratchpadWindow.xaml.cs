using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Flow.Core.Scratchpad;
using Microsoft.Win32;

namespace Flow.Host.Windows.Scratchpad;

/// <summary>
/// Code-behind for ScratchpadWindow.xaml.
/// Ensures clean decoupling through ViewModel data binding and safe lifecycle flushing.
/// </summary>
public partial class ScratchpadWindow : Window
{
    private readonly ScratchpadViewModel _viewModel;

    public ScratchpadViewModel ViewModel => _viewModel;

    public ScratchpadWindow(ScratchpadViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
            EditorTextBox.Focus();
        };

        Closing += OnWindowClosing;
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Flush any pending debounced autosave synchronously before window teardown
        _viewModel.FlushPendingSave();
    }

    private async void OnNewScratchpadClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.CreateNewScratchpadAsync();
        EditorTextBox.Focus();
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = string.Empty;
    }

    private void OnFilterAllClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedFilter = "All";
    }

    private void OnFilterPinnedClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedFilter = "Pinned";
    }

    private void OnFilterRecentClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedFilter = "Recent";
    }

    private async void OnPinClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.TogglePinAsync();
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        _viewModel.CopyToClipboard();
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItem == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export FLOW Scratchpad",
            FileName = $"{_viewModel.SelectedItem.DisplayTitle}.md",
            Filter = "Markdown Document (*.md)|*.md|Plain Text Document (*.txt)|*.txt|JSON Document (*.json)|*.json",
            DefaultExt = ".md"
        };

        if (dialog.ShowDialog(this) == true)
        {
            string ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            var format = ext switch
            {
                ".txt" => ScratchpadExportFormat.PlainText,
                ".json" => ScratchpadExportFormat.Json,
                _ => ScratchpadExportFormat.Markdown
            };

            await _viewModel.ExportAsync(dialog.FileName, format, overwrite: true);
        }
    }

    private async void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedItem == null) return;

        var result = MessageBox.Show(
            this,
            $"Are you sure you want to delete '{_viewModel.SelectedItem.DisplayTitle}'?\n\nYou can undo this action immediately after deleting.",
            "Delete Scratchpad",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteCurrentAsync();
        }
    }

    private async void OnUndoClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.RestoreDeletedAsync();
    }
}
