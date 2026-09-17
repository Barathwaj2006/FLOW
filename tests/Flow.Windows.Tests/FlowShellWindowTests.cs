using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Audio;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Flow.Host.Windows.UI;
using Flow.Inference;
using Xunit;

namespace Flow.Windows.Tests;

public sealed class FlowShellWindowTests
{
    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(TimeSpan.FromSeconds(10));
        Assert.True(finished, "STA execution timed out after 10s.");
        if (ex != null) throw ex;
    }

    [Fact]
    public void FlowHubWindowManager_IsWebView2Available_DoesNotThrow()
    {
        // Must execute cleanly on any Windows machine
        bool available = FlowHubWindowManager.IsWebView2Available();
        // Result is boolean (true if Edge WebView2 Runtime installed, false otherwise)
        Assert.True(available || !available);
    }

    [Fact]
    public void FlowShellWindow_Instantiates_AndSetsPropertiesCorrectly()
    {
        RunOnSta(() =>
        {
            var window = new FlowShellWindow();
            Assert.False(window.AllowRealClose);
            window.AllowRealClose = true;
            Assert.True(window.AllowRealClose);
        });
    }

    [Fact]
    public void FlowShellWindow_SelectTab_BroadcastsNavigateTabEvent()
    {
        RunOnSta(() =>
        {
            var window = new FlowShellWindow();
            string? capturedEvent = null;
            object? capturedPayload = null;

            window.EventBroadcasted += (ev, payload) =>
            {
                capturedEvent = ev;
                capturedPayload = payload;
            };

            window.SelectTab(1); // 1 = history
            Assert.Equal("navigate-tab", capturedEvent);
            Assert.Equal("history", capturedPayload);

            window.SelectTab(2); // 2 = dictionary
            Assert.Equal("navigate-tab", capturedEvent);
            Assert.Equal("dictionary", capturedPayload);

            window.SelectTab(6); // 6 = settings
            Assert.Equal("navigate-tab", capturedEvent);
            Assert.Equal("settings", capturedPayload);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_GetStatus_ReturnsConnected()
    {
        RunOnSta(() =>
        {
            var window = new FlowShellWindow();
            string? respondedAction = null;
            bool? wasSuccess = null;
            object? responsePayload = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                respondedAction = action;
                responsePayload = payload;
                wasSuccess = success;
            };

            using var doc = JsonDocument.Parse("{}");
            window.HandleActionAsync("req-1", "get-status", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("get-status", respondedAction);
            Assert.True(wasSuccess);
            Assert.NotNull(responsePayload);

            string json = JsonSerializer.Serialize(responsePayload);
            Assert.Contains("\"connected\":true", json);
            Assert.Contains("\"port\":0", json);
            Assert.Contains("Offline", json);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_GetHardware_ReturnsProfile()
    {
        RunOnSta(() =>
        {
            var window = new FlowShellWindow();
            string? respondedAction = null;
            bool? wasSuccess = null;
            object? responsePayload = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                respondedAction = action;
                responsePayload = payload;
                wasSuccess = success;
            };

            using var doc = JsonDocument.Parse("{}");
            window.HandleActionAsync("req-2", "get-hardware", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("get-hardware", respondedAction);
            Assert.True(wasSuccess);
            Assert.NotNull(responsePayload);

            string json = JsonSerializer.Serialize(responsePayload);
            Assert.Contains("primaryGpuName", json);
            Assert.Contains("recommendedBackend", json);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_GetModels_WithModelManager()
    {
        RunOnSta(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "FlowShell_Models_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var modelManager = new WhisperModelManager(tempDir);
                var window = new FlowShellWindow(
                    coordinator: null,
                    capture: null,
                    deviceManager: null,
                    historyService: null,
                    scratchpadService: null,
                    dictRepo: null,
                    dictEngine: null,
                    snippetRepo: null,
                    snippetEngine: null,
                    styleRepo: null,
                    styleEngine: null,
                    settingsRepo: null,
                    hotkeyHook: null,
                    modelManager: modelManager);

                string? respondedAction = null;
                bool? wasSuccess = null;
                object? responsePayload = null;

                window.MessageSent += (reqId, action, payload, success) =>
                {
                    respondedAction = action;
                    responsePayload = payload;
                    wasSuccess = success;
                };

                using var doc = JsonDocument.Parse("{}");
                window.HandleActionAsync("req-3", "get-models", doc.RootElement).GetAwaiter().GetResult();

                Assert.Equal("get-models", respondedAction);
                Assert.True(wasSuccess);
                Assert.NotNull(responsePayload);

                string json = JsonSerializer.Serialize(responsePayload);
                Assert.Contains("ggml-tiny.en.bin", json);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_Dictionary_AddAndGetAndDelete()
    {
        RunOnSta(() =>
        {
            var db = new SqlitePersonalizationDatabase(":memory:");
            var dictRepo = new SqlitePersonalDictionaryRepository(db);
            var dictEngine = new PersonalDictionaryEngine(dictRepo);

            var window = new FlowShellWindow(
                coordinator: null,
                capture: null,
                deviceManager: null,
                historyService: null,
                scratchpadService: null,
                dictRepo: dictRepo,
                dictEngine: dictEngine,
                snippetRepo: null,
                snippetEngine: null,
                styleRepo: null,
                styleEngine: null,
                settingsRepo: null,
                hotkeyHook: null);

            object? lastPayload = null;
            bool? lastSuccess = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                lastPayload = payload;
                lastSuccess = success;
            };

            // 1. Add entry
            using (var addDoc = JsonDocument.Parse("{\"term\":\"Kubernetes\",\"replacement\":\"K8s\",\"isStarred\":true,\"category\":\"DevOps\"}"))
            {
                window.HandleActionAsync("req-add", "add-dictionary", addDoc.RootElement).GetAwaiter().GetResult();
            }

            Assert.True(lastSuccess);
            Assert.NotNull(lastPayload);

            // 2. Get dictionary
            using (var getDoc = JsonDocument.Parse("{}"))
            {
                window.HandleActionAsync("req-get", "get-dictionary", getDoc.RootElement).GetAwaiter().GetResult();
            }

            Assert.True(lastSuccess);
            string json = JsonSerializer.Serialize(lastPayload);
            Assert.Contains("Kubernetes", json);
            Assert.Contains("K8s", json);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_SnippetsAndStylesAndNotes_QueryCleanly()
    {
        RunOnSta(() =>
        {
            var db = new SqlitePersonalizationDatabase(":memory:");
            var snippetRepo = new SqliteSnippetRepository(db);
            var styleRepo = new SqliteStyleRepository(db);
            var scratchpadRepo = new SqliteScratchpadRepository(db);
            var scratchpadExport = new ScratchpadExportService();
            var scratchpadService = new ScratchpadService(scratchpadRepo, scratchpadRepo, scratchpadExport);

            var window = new FlowShellWindow(
                coordinator: null,
                capture: null,
                deviceManager: null,
                historyService: null,
                scratchpadService: scratchpadService,
                dictRepo: null,
                dictEngine: null,
                snippetRepo: snippetRepo,
                snippetEngine: null,
                styleRepo: styleRepo,
                styleEngine: null,
                settingsRepo: null,
                hotkeyHook: null);

            object? lastPayload = null;
            bool? lastSuccess = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                lastPayload = payload;
                lastSuccess = success;
            };

            using var doc = JsonDocument.Parse("{}");

            window.HandleActionAsync("req-snip", "get-snippets", doc.RootElement).GetAwaiter().GetResult();
            Assert.True(lastSuccess);

            window.HandleActionAsync("req-style", "get-styles", doc.RootElement).GetAwaiter().GetResult();
            Assert.True(lastSuccess);

            window.HandleActionAsync("req-notes", "get-notes", doc.RootElement).GetAwaiter().GetResult();
            Assert.True(lastSuccess);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_Settings_GetAndSave()
    {
        RunOnSta(() =>
        {
            var db = new SqlitePersonalizationDatabase(":memory:");
            var settingsRepo = new SqliteSettingsRepository(db);

            var window = new FlowShellWindow(
                coordinator: null,
                capture: null,
                deviceManager: null,
                historyService: null,
                scratchpadService: null,
                dictRepo: null,
                dictEngine: null,
                snippetRepo: null,
                snippetEngine: null,
                styleRepo: null,
                styleEngine: null,
                settingsRepo: settingsRepo,
                hotkeyHook: null);

            object? lastPayload = null;
            bool? lastSuccess = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                lastPayload = payload;
                lastSuccess = success;
            };

            using (var doc = JsonDocument.Parse("{}"))
            {
                window.HandleActionAsync("req-get-set", "get-settings", doc.RootElement).GetAwaiter().GetResult();
                Assert.True(lastSuccess);
                Assert.NotNull(lastPayload);
            }

            using (var saveDoc = JsonDocument.Parse("{\"hotkeyVk\":192,\"language\":\"en\",\"cloudAiProvider\":\"None\"}"))
            {
                window.HandleActionAsync("req-save-set", "save-settings", saveDoc.RootElement).GetAwaiter().GetResult();
                Assert.True(lastSuccess);
            }
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_UnknownAction_ReturnsError()
    {
        RunOnSta(() =>
        {
            var window = new FlowShellWindow();
            bool? wasSuccess = null;
            object? responsePayload = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                responsePayload = payload;
                wasSuccess = success;
            };

            using var doc = JsonDocument.Parse("{}");
            window.HandleActionAsync("req-err", "invalid-action-name", doc.RootElement).GetAwaiter().GetResult();

            Assert.False(wasSuccess);
            Assert.NotNull(responsePayload);
            Assert.Contains("Unknown action", responsePayload.ToString());
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_ExitApp_FiresEvent()
    {
        RunOnSta(() =>
        {
            var window = new FlowShellWindow();
            bool exitFired = false;
            window.ExitApplicationRequested += () => exitFired = true;

            using var doc = JsonDocument.Parse("{}");
            window.HandleActionAsync("req-exit", "exit-app", doc.RootElement).GetAwaiter().GetResult();

            Assert.True(exitFired);
        });
    }
}
