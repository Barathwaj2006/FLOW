using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Personalization;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Flow.Host.Windows.Personalization;
using Flow.Host.Windows.UI;
using Xunit;

namespace Flow.Windows.Tests;

public sealed class ConsumerHubPersonalizationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqlitePersonalizationDatabase _db;
    private readonly SqlitePersonalDictionaryRepository _dictRepo;
    private readonly PersonalDictionaryEngine _dictEngine;
    private readonly SqliteSnippetRepository _snippetRepo;
    private readonly SnippetExpansionEngine _snippetEngine;
    private readonly SqliteStyleRepository _styleRepo;
    private readonly StyleFormattingEngine _styleEngine;

    public ConsumerHubPersonalizationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"flow_test_hub_{Guid.NewGuid():N}.db");
        _db = new SqlitePersonalizationDatabase(_testDbPath);
        _dictRepo = new SqlitePersonalDictionaryRepository(_db);
        _dictEngine = new PersonalDictionaryEngine(_dictRepo);
        _snippetRepo = new SqliteSnippetRepository(_db);
        _snippetEngine = new SnippetExpansionEngine(_snippetRepo);
        _styleRepo = new SqliteStyleRepository(_db);
        _styleEngine = new StyleFormattingEngine(_styleRepo);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [Fact]
    public async Task DictionaryViewModel_Add_Search_Star_Delete_Lifecycle()
    {
        var vm = new DictionaryViewModel(_dictRepo, _dictEngine);
        await vm.RefreshAsync();
        Assert.True(vm.IsEmpty);

        // 1. Add word
        bool added = await vm.AddOrUpdateEntryAsync("Kubernetes", "K8s", isStarred: true);
        Assert.True(added);
        Assert.Single(vm.DisplayEntries);
        Assert.Equal("Kubernetes", vm.DisplayEntries[0].Term);
        Assert.Equal("K8s", vm.DisplayEntries[0].Replacement);
        Assert.True(vm.DisplayEntries[0].IsStarred);

        // 2. Add second word
        await vm.AddOrUpdateEntryAsync("Antigravity", null, isStarred: false);
        Assert.Equal(2, vm.DisplayEntries.Count);

        // 3. Search filter
        vm.SearchQuery = "Kuber";
        Assert.Single(vm.DisplayEntries);
        Assert.Equal("Kubernetes", vm.DisplayEntries[0].Term);

        vm.SearchQuery = "";
        Assert.Equal(2, vm.DisplayEntries.Count);

        // 4. Toggle star
        string k8sId = vm.DisplayEntries.First(e => e.Term == "Kubernetes").Id;
        await vm.ToggleStarAsync(k8sId);
        var refreshedK8s = vm.DisplayEntries.First(e => e.Term == "Kubernetes");
        Assert.False(refreshedK8s.IsStarred);

        // 5. Delete word
        await vm.DeleteEntryAsync(k8sId);
        Assert.Single(vm.DisplayEntries);
        Assert.Equal("Antigravity", vm.DisplayEntries[0].Term);
    }

    [Fact]
    public async Task SnippetsViewModel_Add_Search_Delete_Lifecycle()
    {
        var vm = new SnippetsViewModel(_snippetRepo, _snippetEngine);
        await vm.RefreshAsync();
        Assert.True(vm.IsEmpty);

        // 1. Add snippet
        bool added = await vm.AddOrUpdateSnippetAsync("my calendly", "https://calendly.com/user/30min", "Meeting scheduler");
        Assert.True(added);
        Assert.Single(vm.DisplaySnippets);
        Assert.Equal("my calendly", vm.DisplaySnippets[0].TriggerPhrase);
        Assert.Equal("https://calendly.com/user/30min", vm.DisplaySnippets[0].ExpansionText);

        // 2. Add second snippet
        await vm.AddOrUpdateSnippetAsync("standard intro", "Hi, nice to meet you. Thanks for reaching out!", "Email intro");
        Assert.Equal(2, vm.DisplaySnippets.Count);

        // 3. Search filter
        vm.SearchQuery = "calendly";
        Assert.Single(vm.DisplaySnippets);
        Assert.Equal("my calendly", vm.DisplaySnippets[0].TriggerPhrase);

        vm.SearchQuery = "";
        Assert.Equal(2, vm.DisplaySnippets.Count);

        // 4. Delete snippet
        string id = vm.DisplaySnippets[0].Id;
        await vm.DeleteSnippetAsync(id);
        Assert.Single(vm.DisplaySnippets);
    }

    [Fact]
    public async Task StylesViewModel_InitializesDefaultProfiles_AndAllowsSelection()
    {
        var vm = new StylesViewModel(_styleRepo, _styleEngine);
        await vm.RefreshAsync();

        // Must contain the seeded writing styles
        Assert.True(vm.Profiles.Count >= 4);
        Assert.Contains(vm.Profiles, p => p.Name == "Default");
        Assert.Contains(vm.Profiles, p => p.Name == "Personal");
        Assert.Contains(vm.Profiles, p => p.Name == "Work");
        Assert.Contains(vm.Profiles, p => p.Name == "Casual");

        Assert.NotNull(vm.ActiveProfile);

        // Select Work style
        var work = vm.Profiles.First(p => p.Name == "Work");
        await vm.SelectProfileAsync(work.Id);

        Assert.Equal(work.Id, vm.ActiveProfile.Id);
        Assert.Equal("Work", vm.ActiveProfileName);
    }

    [Fact]
    public void FlowHubWindow_WithAllPersonalizationServices_InitializesCleanlyOnSta()
    {
        RunOnSta(() =>
        {
            var window = new FlowHubWindow(
                coordinator: null,
                capture: null,
                deviceManager: null,
                historyService: null,
                scratchpadService: null,
                dictRepo: _dictRepo,
                dictEngine: _dictEngine,
                snippetRepo: _snippetRepo,
                snippetEngine: _snippetEngine,
                styleRepo: _styleRepo,
                styleEngine: _styleEngine
            );

            Assert.NotNull(window.DictionaryVm);
            Assert.NotNull(window.SnippetsVm);
            Assert.NotNull(window.StylesVm);

            Assert.Equal(0, window.NavListBox.SelectedIndex);
            Assert.Equal(0, window.MainTabControl.SelectedIndex);
            Assert.Equal("FLOW Active & Ready", window.StatusBadgeText.Text);
        });
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
            throw exCaught;
        }
    }
}
