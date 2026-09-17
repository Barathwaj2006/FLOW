using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Flow.Host.Windows.Scratchpad;
using Xunit;

namespace Flow.Windows.Tests;

public class Phase9ScratchpadWindowAndViewModelTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteScratchpadRepository _repository;
    private readonly ScratchpadExportService _exportService;
    private readonly ScratchpadService _service;

    public Phase9ScratchpadWindowAndViewModelTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_scratchpad_ui_test_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteScratchpadRepository(_database);
        _exportService = new ScratchpadExportService();
        _service = new ScratchpadService(_repository, _repository, _exportService);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
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
    public void ScratchpadInHub_Instantiates_OnStaThread()
    {
        RunOnSta(() =>
        {
            var window = new Flow.Host.Windows.UI.FlowHubWindow(null, null, null, null, _service);

            Assert.NotNull(window);
            Assert.Equal("FLOW — Voice Productivity", window.Title);

            window.Close();
        });
    }

    [Fact]
    public void ScratchpadViewModel_Initialize_LoadsItemsAndSelectsFirst()
    {
        RunOnSta(async () =>
        {
            await _service.CreateScratchpadAsync("First Note", "Initial content");
            await _service.CreateScratchpadAsync("Second Note", "Second content");

            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();

            Assert.Equal(2, viewModel.Items.Count);
            Assert.NotNull(viewModel.SelectedItem);
            Assert.Equal("Second Note", viewModel.SelectedItem.Title);
            Assert.Equal("Second content", viewModel.EditorContent);
            Assert.Equal(2, viewModel.WordCount);
        });
    }

    [Fact]
    public void ScratchpadViewModel_CreateNew_InsertsAndSelects()
    {
        RunOnSta(async () =>
        {
            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();

            int initialCount = viewModel.Items.Count;
            var newItem = await viewModel.CreateNewScratchpadAsync("Fresh Idea");

            Assert.Equal(initialCount + 1, viewModel.Items.Count);
            Assert.Equal(newItem, viewModel.SelectedItem);
            Assert.Equal("Fresh Idea", viewModel.EditorContent);
            Assert.Equal(2, viewModel.WordCount);
        });
    }

    [Fact]
    public void ScratchpadViewModel_Autosave_PersistsChangesDurably()
    {
        RunOnSta(async () =>
        {
            var item = await _service.CreateScratchpadAsync("Original Title", "Original Content");

            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();
            viewModel.SelectedItem = viewModel.Items[0];

            viewModel.EditorTitle = "Updated Title Via Editor";
            viewModel.EditorContent = "Updated Content with five words.";

            Assert.True(viewModel.IsDirty);

            // Execute explicit save
            await viewModel.SaveCurrentAsync();

            Assert.False(viewModel.IsDirty);
            Assert.Equal("Saved", viewModel.StatusMessage);

            // Verify in repository
            var fromDb = await _service.GetScratchpadAsync(item.Id);
            Assert.NotNull(fromDb);
            Assert.Equal("Updated Title Via Editor", fromDb.Title);
            Assert.Equal("Updated Content with five words.", fromDb.Content);
            Assert.Equal(5, fromDb.WordCount);
        });
    }

    [Fact]
    public void ScratchpadViewModel_SwitchSelection_FlushesDirtyChangesWithoutDataLoss()
    {
        RunOnSta(async () =>
        {
            var note1 = await _service.CreateScratchpadAsync("Note 1", "Content 1");
            var note2 = await _service.CreateScratchpadAsync("Note 2", "Content 2");

            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();

            // Select Note 1
            viewModel.SelectedItem = viewModel.Items.First(x => x.Id == note1.Id);
            viewModel.EditorContent = "Modified content 1 that must not be lost.";
            Assert.True(viewModel.IsDirty);

            // Switch to Note 2
            viewModel.SelectedItem = viewModel.Items.First(x => x.Id == note2.Id);

            // Note 1 changes should have been automatically flushed
            var reloadedNote1 = await _service.GetScratchpadAsync(note1.Id);
            Assert.NotNull(reloadedNote1);
            Assert.Equal("Modified content 1 that must not be lost.", reloadedNote1.Content);
        });
    }

    [Fact]
    public void ScratchpadViewModel_TogglePin_UpdatesStateAndList()
    {
        RunOnSta(async () =>
        {
            var note = await _service.CreateScratchpadAsync("To Pin", "Pinning test note");

            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();
            viewModel.SelectedItem = viewModel.Items[0];

            Assert.False(viewModel.SelectedItem.IsPinned);

            await viewModel.TogglePinAsync();
            Assert.True(viewModel.SelectedItem.IsPinned);

            var dbNote = await _service.GetScratchpadAsync(note.Id);
            Assert.NotNull(dbNote);
            Assert.True(dbNote.IsPinned);
        });
    }

    [Fact]
    public void ScratchpadViewModel_SoftDeleteAndUndo_RestoresState()
    {
        RunOnSta(async () =>
        {
            var note1 = await _service.CreateScratchpadAsync("Keep Note", "Keep content");
            var note2 = await _service.CreateScratchpadAsync("Delete Note", "Delete content");

            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();

            viewModel.SelectedItem = viewModel.Items.First(x => x.Id == note2.Id);
            await viewModel.DeleteCurrentAsync();

            Assert.True(viewModel.IsUndoVisible);
            Assert.DoesNotContain(viewModel.Items, x => x.Id == note2.Id);

            // Undo restore
            await viewModel.RestoreDeletedAsync();

            Assert.False(viewModel.IsUndoVisible);
            Assert.Contains(viewModel.Items, x => x.Id == note2.Id);
            Assert.NotNull(viewModel.SelectedItem);
            Assert.Equal(note2.Id, viewModel.SelectedItem.Id);
        });
    }

    [Fact]
    public void ScratchpadViewModel_SearchAndFilter_UpdatesDisplayedList()
    {
        RunOnSta(async () =>
        {
            await _service.CreateScratchpadAsync("Whisper Model", "Inference details");
            await _service.CreateScratchpadAsync("Meeting Minutes", "Action items");

            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();
            Assert.Equal(2, viewModel.Items.Count);

            viewModel.SearchText = "Whisper";
            await viewModel.RefreshListAsync();

            Assert.Single(viewModel.Items);
            Assert.Equal("Whisper Model", viewModel.Items[0].Title);

            viewModel.SearchText = string.Empty;
            await viewModel.RefreshListAsync();
            Assert.Equal(2, viewModel.Items.Count);
        });
    }

    [Fact]
    public void ScratchpadViewModel_CopyToClipboard_SetsClipboardWithoutEnter()
    {
        RunOnSta(() =>
        {
            var viewModel = new ScratchpadViewModel(_service)
            {
                EditorContent = "Directly copied text into Windows clipboard."
            };

            viewModel.CopyToClipboard();

            string clipboardText = string.Empty;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    clipboardText = Clipboard.GetText();
                    break;
                }
                catch (System.Runtime.InteropServices.COMException) when (attempt < 4)
                {
                    System.Threading.Thread.Sleep(50);
                }
            }

            Assert.Equal("Directly copied text into Windows clipboard.", clipboardText);
            Assert.Equal("Copied to clipboard", viewModel.StatusMessage);
            Assert.DoesNotContain("\r\n", clipboardText);
        });
    }

    [Fact]
    public void ScratchpadTab_HubLifecycle_OnStaThread()
    {
        RunOnSta(() =>
        {
            var hub = new Flow.Host.Windows.UI.FlowHubWindow(null, null, null, null, _service);
            Assert.NotNull(hub);
            Assert.Equal("FLOW — Voice Productivity", hub.Title);
            hub.Close();
        });
    }

    [Fact]
    public void Physical_ScratchpadViewModel_FullUserFlow_OnStaThread()
    {
        RunOnSta(async () =>
        {
            var viewModel = new ScratchpadViewModel(_service);
            await viewModel.InitializeAsync();

            // 1. Create note
            var note = await viewModel.CreateNewScratchpadAsync("Interactive Note");
            Assert.NotNull(viewModel.SelectedItem);
            Assert.Equal("Interactive Note", viewModel.EditorContent);

            // 2. Edit note and test debounced autosave
            viewModel.EditorTitle = "Interactive Note Title";
            viewModel.EditorContent = "Full user workflow test content with multiple lines.\nSecond line of text.";
            await viewModel.SaveCurrentAsync();
            Assert.Equal("Saved", viewModel.StatusMessage);

            // 3. Pin note
            await viewModel.TogglePinAsync();
            Assert.True(viewModel.SelectedItem.IsPinned);

            // 4. Search note
            viewModel.SearchText = "Interactive";
            await viewModel.RefreshListAsync();
            Assert.Single(viewModel.Items);

            // 5. Copy content
            viewModel.CopyToClipboard();
            Assert.Equal("Full user workflow test content with multiple lines.\nSecond line of text.", Clipboard.GetText());

            // 6. Delete & Undo
            await viewModel.DeleteCurrentAsync();
            Assert.True(viewModel.IsUndoVisible);
            await viewModel.RestoreDeletedAsync();
            Assert.False(viewModel.IsUndoVisible);
        });
    }
}
