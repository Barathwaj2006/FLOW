using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Core.Scratchpad;

namespace Flow.Host.Windows.Scratchpad;

/// <summary>
/// Observable item representing an individual scratchpad in the navigation list.
/// </summary>
public sealed class ScratchpadItemViewModel : INotifyPropertyChanged
{
    private string _title;
    private string _content;
    private bool _isPinned;
    private DateTimeOffset _updatedAt;
    private int _wordCount;
    private int _characterCount;

    public string Id { get; }

    public string Title
    {
        get => _title;
        set { if (_title != value) { _title = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); } }
    }

    public string DisplayTitle => string.IsNullOrWhiteSpace(_title) ? "Untitled Scratchpad" : _title;

    public string Content
    {
        get => _content;
        set
        {
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ContentPreview));
            }
        }
    }

    public bool IsPinned
    {
        get => _isPinned;
        set { if (_isPinned != value) { _isPinned = value; OnPropertyChanged(); OnPropertyChanged(nameof(PinIndicator)); } }
    }

    public string PinIndicator => _isPinned ? "📌" : "";

    public DateTimeOffset UpdatedAt
    {
        get => _updatedAt;
        set { if (_updatedAt != value) { _updatedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedDate)); } }
    }

    public string FormattedDate => _updatedAt.ToLocalTime().ToString("g");

    public int WordCount
    {
        get => _wordCount;
        set { if (_wordCount != value) { _wordCount = value; OnPropertyChanged(); } }
    }

    public int CharacterCount
    {
        get => _characterCount;
        set { if (_characterCount != value) { _characterCount = value; OnPropertyChanged(); } }
    }

    public string ContentPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_content)) return "No content";
            using var reader = new StringReader(_content);
            string? line = reader.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(line)) line = "No content";
            return line.Length <= 80 ? line : line[..77] + "...";
        }
    }

    public ScratchpadItemViewModel(ScratchpadEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        Id = entry.Id;
        _title = entry.Title;
        _content = entry.Content;
        _isPinned = entry.IsPinned;
        _updatedAt = entry.UpdatedAt;
        _wordCount = entry.WordCount;
        _characterCount = entry.CharacterCount;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Primary ViewModel for FLOW Scratchpad &amp; Quick Capture.
/// Handles responsive local debounced autosave, zero data loss, list management,
/// search, pinning, reversible deletion, and export.
/// </summary>
public sealed class ScratchpadViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IScratchpadService _service;
    private readonly SynchronizationContext? _syncContext;
    private readonly object _saveLock = new();

    private ScratchpadItemViewModel? _selectedItem;
    private string _editorTitle = string.Empty;
    private string _editorContent = string.Empty;
    private int _wordCount;
    private int _characterCount;
    private string _statusMessage = "Ready";
    private string _searchText = string.Empty;
    private string _selectedFilter = "All"; // All, Pinned, Recent
    private bool _isUndoVisible;
    private string? _lastDeletedId;
    private string? _lastDeletedTitle;
    private bool _isDirty;
    private bool _isLoading;
    private bool _isDisposed;

    private CancellationTokenSource? _debounceCts;
    private readonly TimeSpan _autosaveDebounceDelay = TimeSpan.FromMilliseconds(400);

    public ObservableCollection<ScratchpadItemViewModel> Items { get; } = new();

    public ScratchpadItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!ReferenceEquals(_selectedItem, value))
            {
                // Flush pending changes before switching
                FlushPendingSave();
                _selectedItem = value;
                OnPropertyChanged();
                LoadSelectedItemIntoEditor();
            }
        }
    }

    public string EditorTitle
    {
        get => _editorTitle;
        set
        {
            if (_editorTitle != value)
            {
                _editorTitle = value;
                OnPropertyChanged();
                if (!_isLoading)
                {
                    MarkDirtyAndScheduleAutosave();
                }
            }
        }
    }

    public string EditorContent
    {
        get => _editorContent;
        set
        {
            if (_editorContent != value)
            {
                _editorContent = value;
                OnPropertyChanged();
                WordCount = ScratchpadEntry.CalculateWordCount(value);
                CharacterCount = value?.Length ?? 0;
                if (!_isLoading)
                {
                    MarkDirtyAndScheduleAutosave();
                }
            }
        }
    }

    public int WordCount
    {
        get => _wordCount;
        private set { if (_wordCount != value) { _wordCount = value; OnPropertyChanged(); } }
    }

    public int CharacterCount
    {
        get => _characterCount;
        private set { if (_characterCount != value) { _characterCount = value; OnPropertyChanged(); } }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { if (_statusMessage != value) { _statusMessage = value; OnPropertyChanged(); } }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                _ = RefreshListAsync();
            }
        }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set
        {
            if (_selectedFilter != value)
            {
                _selectedFilter = value;
                OnPropertyChanged();
                _ = RefreshListAsync();
            }
        }
    }

    public bool IsUndoVisible
    {
        get => _isUndoVisible;
        set { if (_isUndoVisible != value) { _isUndoVisible = value; OnPropertyChanged(); } }
    }

    public string? LastDeletedTitle
    {
        get => _lastDeletedTitle;
        set { if (_lastDeletedTitle != value) { _lastDeletedTitle = value; OnPropertyChanged(); } }
    }

    public bool IsDirty => _isDirty;

    public ScratchpadViewModel(IScratchpadService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _syncContext = SynchronizationContext.Current;
    }

    /// <summary>
    /// Loads initial scratchpads list.
    /// </summary>
    public async Task InitializeAsync()
    {
        await RefreshListAsync();
        if (Items.Count > 0 && SelectedItem == null)
        {
            SelectedItem = Items[0];
        }
        else if (Items.Count == 0)
        {
            await CreateNewScratchpadAsync();
        }
    }

    /// <summary>
    /// Creates a new scratchpad and selects it in the editor.
    /// </summary>
    public async Task<ScratchpadItemViewModel> CreateNewScratchpadAsync(string? defaultContent = null)
    {
        FlushPendingSave();

        string initialContent = defaultContent ?? string.Empty;
        var entry = await _service.CreateScratchpadAsync("Untitled Scratchpad", initialContent);
        var itemVm = new ScratchpadItemViewModel(entry);

        Items.Insert(0, itemVm);
        SelectedItem = itemVm;
        StatusMessage = "New scratchpad created";
        return itemVm;
    }

    /// <summary>
    /// Refreshes the list based on search and active filter.
    /// </summary>
    public async Task RefreshListAsync()
    {
        ScratchpadFilter filter;
        if (SelectedFilter == "Pinned")
        {
            filter = new ScratchpadFilter(SearchQuery: _searchText, IsPinned: true, SortBy: ScratchpadSortOrder.UpdatedAtDesc);
        }
        else if (SelectedFilter == "Recent")
        {
            filter = new ScratchpadFilter(SearchQuery: _searchText, SortBy: ScratchpadSortOrder.UpdatedAtDesc);
        }
        else
        {
            filter = new ScratchpadFilter(SearchQuery: _searchText, SortBy: ScratchpadSortOrder.PinnedFirstThenUpdated);
        }

        var page = await _service.ListScratchpadsAsync(filter, pageIndex: 0, pageSize: 200);

        string? currentSelectedId = SelectedItem?.Id;

        Items.Clear();
        foreach (var entry in page.Items)
        {
            Items.Add(new ScratchpadItemViewModel(entry));
        }

        if (currentSelectedId != null)
        {
            SelectedItem = Items.FirstOrDefault(x => x.Id == currentSelectedId);
        }
    }

    private void LoadSelectedItemIntoEditor()
    {
        _isLoading = true;
        try
        {
            if (_selectedItem != null)
            {
                _editorTitle = _selectedItem.Title;
                _editorContent = _selectedItem.Content;
                _wordCount = _selectedItem.WordCount;
                _characterCount = _selectedItem.CharacterCount;
                StatusMessage = "Saved";
            }
            else
            {
                _editorTitle = string.Empty;
                _editorContent = string.Empty;
                _wordCount = 0;
                _characterCount = 0;
                StatusMessage = "Ready";
            }

            OnPropertyChanged(nameof(EditorTitle));
            OnPropertyChanged(nameof(EditorContent));
            OnPropertyChanged(nameof(WordCount));
            OnPropertyChanged(nameof(CharacterCount));
            _isDirty = false;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void MarkDirtyAndScheduleAutosave()
    {
        if (_selectedItem == null) return;

        _isDirty = true;
        StatusMessage = "Unsaved changes...";

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_autosaveDebounceDelay, token);
                if (!token.IsCancellationRequested)
                {
                    await SaveCurrentAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when new typing cancels previous delay
            }
        }, token);
    }

    /// <summary>
    /// Explicitly saves the current editor content to the database immediately.
    /// </summary>
    public async Task SaveCurrentAsync(CancellationToken ct = default)
    {
        if (_selectedItem == null) return;

        lock (_saveLock)
        {
            if (!_isDirty) return;
        }

        UpdateStatusOnUI("Saving...");

        try
        {
            string id = _selectedItem.Id;
            string titleToSave = _editorTitle;
            string contentToSave = _editorContent;

            var updated = await _service.UpdateScratchpadAsync(id, titleToSave, contentToSave, ct);
            if (updated != null)
            {
                lock (_saveLock)
                {
                    _isDirty = false;
                }

                PostToUI(() =>
                {
                    if (_selectedItem != null && _selectedItem.Id == id)
                    {
                        _selectedItem.Title = updated.Title;
                        _selectedItem.Content = updated.Content;
                        _selectedItem.UpdatedAt = updated.UpdatedAt;
                        _selectedItem.WordCount = updated.WordCount;
                        _selectedItem.CharacterCount = updated.CharacterCount;
                    }
                    StatusMessage = "Saved";
                });
            }
        }
        catch (Exception)
        {
            UpdateStatusOnUI("Error saving changes");
        }
    }

    /// <summary>
    /// Flushes any pending autosave synchronously or before switching.
    /// </summary>
    public void FlushPendingSave()
    {
        _debounceCts?.Cancel();
        if (_isDirty && _selectedItem != null)
        {
            try
            {
                var task = SaveCurrentAsync(CancellationToken.None);
                task.GetAwaiter().GetResult();
            }
            catch
            {
                // Never crash on flush
            }
        }
    }

    /// <summary>
    /// Flushes any pending autosave asynchronously.
    /// </summary>
    public async Task FlushPendingSaveAsync()
    {
        _debounceCts?.Cancel();
        if (_isDirty && _selectedItem != null)
        {
            try
            {
                await SaveCurrentAsync(CancellationToken.None);
            }
            catch
            {
                // Never crash on flush
            }
        }
    }

    /// <summary>
    /// Toggles the pinned state of the currently selected scratchpad.
    /// </summary>
    public async Task TogglePinAsync()
    {
        if (_selectedItem == null) return;

        bool newPinned = !_selectedItem.IsPinned;
        if (newPinned)
        {
            await _service.PinScratchpadAsync(_selectedItem.Id);
        }
        else
        {
            await _service.UnpinScratchpadAsync(_selectedItem.Id);
        }

        _selectedItem.IsPinned = newPinned;
        StatusMessage = newPinned ? "Pinned" : "Unpinned";
        await RefreshListAsync();
    }

    /// <summary>
    /// Soft-deletes the currently selected scratchpad and provides an Undo prompt.
    /// </summary>
    public async Task DeleteCurrentAsync()
    {
        if (_selectedItem == null) return;

        string id = _selectedItem.Id;
        string title = _selectedItem.DisplayTitle;

        _debounceCts?.Cancel();
        _isDirty = false;

        bool success = await _service.DeleteScratchpadAsync(id);
        if (success)
        {
            _lastDeletedId = id;
            LastDeletedTitle = title;
            IsUndoVisible = true;

            int index = Items.IndexOf(_selectedItem);
            Items.Remove(_selectedItem);

            if (Items.Count > 0)
            {
                int nextIndex = Math.Clamp(index, 0, Items.Count - 1);
                SelectedItem = Items[nextIndex];
            }
            else
            {
                SelectedItem = null;
            }

            StatusMessage = $"Deleted '{title}'";
        }
    }

    /// <summary>
    /// Restores the last soft-deleted scratchpad.
    /// </summary>
    public async Task RestoreDeletedAsync()
    {
        if (string.IsNullOrWhiteSpace(_lastDeletedId)) return;

        bool success = await _service.RestoreScratchpadAsync(_lastDeletedId);
        if (success)
        {
            string restoredId = _lastDeletedId;
            _lastDeletedId = null;
            IsUndoVisible = false;

            await RefreshListAsync();
            SelectedItem = Items.FirstOrDefault(x => x.Id == restoredId);
            StatusMessage = "Scratchpad restored";
        }
    }

    /// <summary>
    /// Safely copies the full current editor content to the Windows clipboard.
    /// Does not simulate Enter or keystrokes.
    /// </summary>
    public void CopyToClipboard()
    {
        try
        {
            string textToCopy = _editorContent ?? string.Empty;
            Clipboard.SetDataObject(textToCopy, true);
            StatusMessage = "Copied to clipboard";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Exports the currently selected scratchpad to a local file.
    /// </summary>
    public async Task<string> ExportAsync(string destinationPath, ScratchpadExportFormat format, bool overwrite = false)
    {
        if (_selectedItem == null)
        {
            throw new InvalidOperationException("No scratchpad selected for export.");
        }

        await FlushPendingSaveAsync();
        string path = await _service.ExportScratchpadAsync(_selectedItem.Id, destinationPath, format, overwrite);
        StatusMessage = $"Exported to {Path.GetFileName(path)}";
        return path;
    }

    private void UpdateStatusOnUI(string status)
    {
        PostToUI(() => StatusMessage = status);
    }

    private void PostToUI(Action action)
    {
        if (_syncContext != null && SynchronizationContext.Current != _syncContext)
        {
            _syncContext.Post(_ => action(), null);
        }
        else if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }
}
