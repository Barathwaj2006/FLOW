using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Flow.Core.History;
using Flow.Core.Scratchpad;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Commands;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.Storage;
using Flow.Core.TextInsertion;
using Flow.Host.Windows.Lifecycle;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.Tray;
using Flow.Host.Windows.UI;
using Xunit;

namespace Flow.Windows.Tests;

public class ProductCompletionLifecycleTests
{
    [Fact]
    public void SingleInstanceCoordinator_RegisteredWindowMessage_IsValid()
    {
        Assert.True(SingleInstanceCoordinator.ActivationMessage > 0, "Registered window message must be non-zero.");
    }

    [Fact]
    public void SingleInstanceCoordinator_FirstInstance_AcquiresSuccessfully()
    {
        string testMutex = $"Local\\FLOW_Test_Mutex_{Guid.NewGuid():N}";
        using var instance1 = new SingleInstanceCoordinator(testMutex);
        bool acquired = instance1.TryAcquireSingleInstance();
        Assert.True(acquired, "First instance must successfully claim the single-instance mutex.");
    }

    [Fact]
    public void SingleInstanceCoordinator_SecondInstance_IsRejected()
    {
        string testMutex = $"Local\\FLOW_Test_Mutex_{Guid.NewGuid():N}";
        using var instance1 = new SingleInstanceCoordinator(testMutex);
        bool acquired1 = instance1.TryAcquireSingleInstance();
        Assert.True(acquired1);

        using var instance2 = new SingleInstanceCoordinator(testMutex);
        bool acquired2 = instance2.TryAcquireSingleInstance();
        Assert.False(acquired2, "Second instance must be rejected when another instance is active.");
    }

    [Fact]
    public void SingleInstanceCoordinator_AfterDisposal_NextInstanceCanAcquire()
    {
        string testMutex = $"Local\\FLOW_Test_Mutex_{Guid.NewGuid():N}";
        var instance1 = new SingleInstanceCoordinator(testMutex);
        bool acquired1 = instance1.TryAcquireSingleInstance();
        Assert.True(acquired1);
        instance1.Dispose();

        using var instance2 = new SingleInstanceCoordinator(testMutex);
        bool acquired2 = instance2.TryAcquireSingleInstance();
        Assert.True(acquired2, "Subsequent instance must be allowed after the previous instance is cleanly disposed.");
    }

    [Fact]
    public void TrayIconManager_ProcessMessage_DispatchesTrayClickAndFlowHub()
    {
        using var tray = new TrayIconManager(IntPtr.Zero, callbackMessage: 0x8001);

        bool trayClicked = false;
        bool flowHubRequested = false;

        tray.TrayClicked += () => trayClicked = true;
        tray.FlowHubRequested += () => flowHubRequested = true;

        // Simulate WM_LBUTTONUP (0x0202)
        tray.ProcessMessage(0x8001, (IntPtr)0x0202);

        Assert.True(trayClicked, "TrayClicked event must fire on WM_LBUTTONUP.");
        Assert.True(flowHubRequested, "FlowHubRequested event must fire on WM_LBUTTONUP.");
    }

    [Fact]
    public void TrayIconManager_TooltipAndBalloon_DoNotThrowExceptions()
    {
        using var tray = new TrayIconManager(IntPtr.Zero, callbackMessage: 0x8001);
        tray.Install("FLOW — Test Tooltip");
        tray.UpdateTooltip("FLOW — Updated Tooltip");
        tray.ShowBalloon("FLOW", "Dictation active");
        tray.Remove();
    }

    [Fact]
    public void TrayIconManager_ProcessMessage_IgnoresBalloonNotificationEvents()
    {
        using var tray = new TrayIconManager(IntPtr.Zero, callbackMessage: 0x8001);
        bool dictationToggled = false;
        bool hubRequested = false;

        tray.ToggleDictationRequested += () => dictationToggled = true;
        tray.FlowHubRequested += () => hubRequested = true;

        // Simulate NIN_BALLOONSHOW (0x0402), NIN_BALLOONHIDE (0x0403), NIN_BALLOONTIMEOUT (0x0404), NIN_BALLOONUSERCLICK (0x0405)
        tray.ProcessMessage(0x8001, (IntPtr)0x0402);
        tray.ProcessMessage(0x8001, (IntPtr)0x0403);
        tray.ProcessMessage(0x8001, (IntPtr)0x0404);
        tray.ProcessMessage(0x8001, (IntPtr)0x0405);

        Assert.False(dictationToggled, "Balloon notifications must never toggle dictation.");
        Assert.False(hubRequested, "Balloon notifications must not open hub unexpectedly.");
    }

    [Fact]
    public void GlobalHotkeyHook_InitialState_IsDisarmedAndNotHooked()
    {
        using var hook = new GlobalHotkeyHook();
        Assert.False(hook.IsHooked, "Hook must not be installed prior to Start().");
        Assert.False(hook.IsArmed, "Hook must start in disarmed state.");
        Assert.False(hook.IsHandsFreeActive, "Hands-free mode must start false.");
    }

    [Fact]
    public void GlobalHotkeyHook_ArmAndDisarm_TogglesStateCleanly()
    {
        using var hook = new GlobalHotkeyHook();
        hook.Arm();
        Assert.True(hook.IsArmed, "Hook must report armed after Arm().");
        hook.Disarm();
        Assert.False(hook.IsArmed, "Hook must report disarmed after Disarm().");
        Assert.False(hook.IsHandsFreeActive, "Hands-free must be false when disarmed.");
    }

    [Fact]
    public void VoiceSessionCoordinator_StartupState_IsStrictlyIdle_AndHandsFreeFalse()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
        var vad = new EnergyVAD(sampleRate: 16000.0);
        var asrRegistry = new ASREngineRegistry();
        var lang = new DeterministicTextSanitizer();
        var ins = new WindowsTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            asrRegistry,
            lang,
            ins
        );

        Assert.Equal(SessionState.Idle, coordinator.CurrentState);
        Assert.False(coordinator.IsHandsFree);
        Assert.Equal(SessionMode.Dictation, coordinator.CurrentMode);
        Assert.Null(coordinator.ActiveContext);
        Assert.Null(coordinator.ActiveTarget);
        Assert.Null(coordinator.ActiveSelectedText);
    }

    [Fact]
    public void WasapiAudioCapture_StartupState_IsNotCapturing()
    {
        using var capture = new WasapiAudioCapture();
        Assert.False(capture.IsCapturing, "Microphone capture must remain inactive upon creation.");
    }

    [Fact]
    public void FloatingHudController_InitialState_IsHiddenAndIdle()
    {
        using var hud = new FloatingHudController();
        Assert.False(hud.IsVisible, "Floating HUD must remain hidden at startup.");
        Assert.Equal("Ready", hud.StatusText);
        Assert.False(hud.IsCommandMode);
        Assert.Equal(0.0f, hud.AudioLevel);
    }

    [Fact]
    public void FlowHubWindow_InitialLandingTab_IsDashboardIndexZero()
    {
        RunOnSta(() =>
        {
            var window = new FlowHubWindow(null, null, null, null, null);
            Assert.Equal(0, window.NavListBox.SelectedIndex);
            Assert.Equal(0, window.MainTabControl.SelectedIndex);
            Assert.Equal("FLOW Active & Ready", window.StatusBadgeText.Text);
            Assert.Equal("Start Dictation", window.BtnToggleDictation.Content);
        });
    }

    [Fact]
    public void FlowHubWindow_Loaded_PreservesIdleCoordinatorState()
    {
        RunOnSta(() =>
        {
            var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
            var vad = new EnergyVAD(sampleRate: 16000.0);
            var asrRegistry = new ASREngineRegistry();
            var lang = new DeterministicTextSanitizer();
            var ins = new WindowsTextInsertionService();

            var coordinator = new VoiceSessionCoordinator(
                ringBuffer,
                vad,
                asrRegistry,
                lang,
                ins
            );

            var window = new FlowHubWindow(coordinator, null, null, null, null);

            // Assert before and after Loaded event
            Assert.Equal(SessionState.Idle, coordinator.CurrentState);
            Assert.False(coordinator.IsHandsFree);

            // Window Loaded assertion
            Assert.Equal("FLOW Active & Ready", window.StatusBadgeText.Text);
            Assert.Equal(0, window.NavListBox.SelectedIndex);
            Assert.Equal(0, window.MainTabControl.SelectedIndex);
        });
    }

    [Fact]
    public void FlowHubWindow_ConsumerTabs_ContainAllEightConsumerSections_AndZeroDeveloperTabs()
    {
        RunOnSta(() =>
        {
            var window = new FlowHubWindow(null, null, null, null, null);

            // Exactly 6 workspace navigation items (Home, History, Dictionary, Snippets, Styles, Scratchpad)
            Assert.Equal(6, window.NavListBox.Items.Count);

            // Exactly 2 preferences items (Settings, About)
            Assert.Equal(2, window.NavListBoxBottom.Items.Count);

            // Exactly 8 main tab pages
            Assert.Equal(8, window.MainTabControl.Items.Count);

            // Verify Tab Headers: Home, History, Dictionary, Snippets, Styles, Scratchpad, Settings, About
            var expectedHeaders = new[] { "Home", "History", "Dictionary", "Snippets", "Styles", "Scratchpad", "Settings", "About" };
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var tab = window.MainTabControl.Items[i] as System.Windows.Controls.TabItem;
                Assert.NotNull(tab);
                Assert.Equal(expectedHeaders[i], tab.Header);
            }

            // Inviolable requirement: ZERO Developer Mode or Command Mode tabs in the user-facing navigation
            foreach (var item in window.NavListBox.Items)
            {
                string text = (item as System.Windows.Controls.ListBoxItem)?.Content?.ToString() ?? "";
                Assert.DoesNotContain("Developer Mode", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Command Mode", text, StringComparison.OrdinalIgnoreCase);
            }

            foreach (var item in window.NavListBoxBottom.Items)
            {
                string text = (item as System.Windows.Controls.ListBoxItem)?.Content?.ToString() ?? "";
                Assert.DoesNotContain("Developer Mode", text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Command Mode", text, StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    [Fact]
    public void FlowHubWindow_SelectTab_SwitchesIndicesCorrectly()
    {
        RunOnSta(() =>
        {
            var window = new FlowHubWindow(null, null, null, null, null);

            // Test selecting each of the 8 tabs
            for (int i = 0; i < 8; i++)
            {
                window.SelectTab(i);
                Assert.Equal(i, window.MainTabControl.SelectedIndex);
                if (i <= 5)
                {
                    Assert.Equal(i, window.NavListBox.SelectedIndex);
                    Assert.Equal(-1, window.NavListBoxBottom.SelectedIndex);
                }
                else
                {
                    Assert.Equal(-1, window.NavListBox.SelectedIndex);
                    Assert.Equal(i - 6, window.NavListBoxBottom.SelectedIndex);
                }
            }
        });
    }

    [Fact]
    public async Task SqliteSettingsRepository_SaveAndLoad_RoundTripsAccurately()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_settings_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new SqlitePersonalizationDatabase(tempDbPath);
            var repo = new SqliteSettingsRepository(db);

            // Default load
            var initial = await repo.LoadSettingsAsync();
            Assert.Equal(0.015f, initial.VadThreshold);
            Assert.Equal("en", initial.Language);
            Assert.Equal(165, initial.HotkeyVk);
            Assert.Equal("Dark", initial.Theme);

            // Save custom
            var updated = new FlowAppSettings
            {
                VadThreshold = 0.042f,
                Language = "ta",
                HotkeyVk = 119,
                Theme = "Light"
            };
            await repo.SaveSettingsAsync(updated);

            // Reload
            var reloaded = await repo.LoadSettingsAsync();
            Assert.Equal(0.042f, reloaded.VadThreshold, precision: 3);
            Assert.Equal("ta", reloaded.Language);
            Assert.Equal(119, reloaded.HotkeyVk);
            Assert.Equal("Light", reloaded.Theme);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    [Fact]
    public void FlowHubWindow_SettingsWiring_AffectsCoordinatorAndHook()
    {
        RunOnSta(() =>
        {
            var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
            var vad = new EnergyVAD(sampleRate: 16000.0);
            var asrRegistry = new ASREngineRegistry();
            var lang = new DeterministicTextSanitizer();
            var ins = new WindowsTextInsertionService();

            var coordinator = new VoiceSessionCoordinator(
                ringBuffer,
                vad,
                asrRegistry,
                lang,
                ins
            );
            var hotkeyHook = new GlobalHotkeyHook(165);
            var window = new FlowHubWindow(
                coordinator,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                hotkeyHook);

            // Select Settings tab
            window.SelectTab(6);
            Assert.Equal(6, window.MainTabControl.SelectedIndex);

            // Modify VAD threshold via coordinator
            coordinator.SetVadThreshold(0.025f);
            Assert.Equal(0.025f, coordinator.CurrentVadThreshold, precision: 3);

            // Modify Language
            coordinator.SelectedLanguage = "hi";
            Assert.Equal("hi", coordinator.SelectedLanguage);

            // Modify Hotkey
            hotkeyHook.UpdateTargetKey(120); // F9
            Assert.Equal(120, hotkeyHook.TargetVk);
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
            throw new TargetInvocationException(exCaught);
        }
    }

    [Fact]
    public async Task RealDatabase_FullHubServicesLoad_DoesNotCrash()
    {
        string realDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW", "flow_personalization.db");
        var db = new SqlitePersonalizationDatabase(realDb);
        var histRepo = new SqliteHistoryRepository(db);
        var privacy = new HistoryPrivacyService();
        var ret = new HistoryRetentionService(histRepo);
        var stats = new ProductivityStatisticsService(histRepo);
        var exp = new HistoryExportService(histRepo);
        var histService = new HistoryService(histRepo, histRepo, stats, ret, exp, privacy);

        var scratchRepo = new SqliteScratchpadRepository(db);
        var scratchExp = new ScratchpadExportService();
        var scratchService = new ScratchpadService(scratchRepo, scratchRepo, scratchExp);

        var s = await histService.Statistics.GetStatisticsAsync(TimeRangeWindow.Today);
        var streak = await histService.Statistics.GetDailyStreakAsync();
        var histPage = await histService.Repository.GetPagedAsync(new HistoryFilter(IncludeDeleted: false), 0, 5);
        var scratchPage = await scratchService.Repository.GetPagedAsync(new ScratchpadFilter(), 0, 50);

        Assert.NotNull(s);
        Assert.NotNull(streak);
        Assert.NotNull(histPage);
        Assert.NotNull(scratchPage);
    }

    [Fact]
    public void GlobalHotkeyHook_AltSpace_PushToTalk_KeyDownAndKeyUp_ConsumedAndFiresEvents()
    {
        using var hook = new GlobalHotkeyHook();
        hook.Arm();

        bool downFired = false;
        bool downIsHandsFree = true;
        bool upFired = false;

        hook.HotkeyDown += (isHandsFree) =>
        {
            downFired = true;
            downIsHandsFree = isHandsFree;
        };
        hook.HotkeyUp += () => upFired = true;

        // Mock GetKeyState where VK_MENU is depressed
        short MockKeyState(int vk) => vk == GlobalHotkeyHook.VK_MENU ? unchecked((short)0x8000) : (short)0;

        // 1. Press Space while Alt is held -> Consumed, PushToTalk fired
        IntPtr downResult = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYDOWN, GlobalHotkeyHook.VK_SPACE, 0, MockKeyState);
        Assert.Equal((IntPtr)1, downResult);
        Assert.True(downFired, "HotkeyDown must fire on Alt+Space");
        Assert.False(downIsHandsFree, "Alt+Space must be Push-to-Talk (isHandsFree = false)");

        // 2. Release Space while Alt is held -> Consumed, HotkeyUp fired
        IntPtr upResult = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYUP, GlobalHotkeyHook.VK_SPACE, 0, MockKeyState);
        Assert.Equal((IntPtr)1, upResult);
        Assert.True(upFired, "HotkeyUp must fire on Space keyup");
    }

    [Fact]
    public void GlobalHotkeyHook_AltSpace_AltReleasedBeforeSpace_ConsumesSpaceAndFiresHotkeyUp()
    {
        using var hook = new GlobalHotkeyHook();
        hook.Arm();

        bool downFired = false;
        bool upFired = false;

        hook.HotkeyDown += (_) => downFired = true;
        hook.HotkeyUp += () => upFired = true;

        // Start with Alt held
        short MockKeyStateAltHeld(int vk) => vk == GlobalHotkeyHook.VK_MENU ? unchecked((short)0x8000) : (short)0;
        short MockKeyStateNoModifiers(int vk) => 0;

        // 1. Press Space while Alt is held -> Consumed
        IntPtr downResult = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYDOWN, GlobalHotkeyHook.VK_SPACE, 0, MockKeyStateAltHeld);
        Assert.Equal((IntPtr)1, downResult);
        Assert.True(downFired);

        // 2. User releases Alt before releasing Space -> Alt release consumed, HotkeyUp fired immediately
        IntPtr altUpResult = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYUP, GlobalHotkeyHook.VK_LMENU, 0, MockKeyStateNoModifiers);
        Assert.Equal((IntPtr)1, altUpResult);
        Assert.True(upFired, "HotkeyUp must fire immediately when Alt is released");

        // 3. User releases Space second (Alt already up) -> Trailing space MUST BE CONSUMED to prevent space leak!
        IntPtr trailingSpaceResult = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYUP, GlobalHotkeyHook.VK_SPACE, 0, MockKeyStateNoModifiers);
        Assert.Equal((IntPtr)1, trailingSpaceResult);
    }

    [Fact]
    public void GlobalHotkeyHook_AltB_ConsumesAndTogglesHandsFree()
    {
        using var hook = new GlobalHotkeyHook();
        hook.Arm();

        int downCount = 0;
        bool lastIsHandsFree = false;
        int upCount = 0;

        hook.HotkeyDown += (hf) =>
        {
            downCount++;
            lastIsHandsFree = hf;
        };
        hook.HotkeyUp += () => upCount++;

        short MockKeyState(int vk) => vk == GlobalHotkeyHook.VK_MENU ? unchecked((short)0x8000) : (short)0;

        // 1. Press Alt+B -> Toggle on (HotkeyDown with isHandsFree=true)
        IntPtr down1 = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYDOWN, GlobalHotkeyHook.VK_KEY_B, 0, MockKeyState);
        Assert.Equal((IntPtr)1, down1);
        Assert.Equal(1, downCount);
        Assert.True(lastIsHandsFree);

        // Release B -> Consumed
        IntPtr up1 = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYUP, GlobalHotkeyHook.VK_KEY_B, 0, MockKeyState);
        Assert.Equal((IntPtr)1, up1);

        // 2. Press Alt+B again -> Toggle off (HotkeyUp fired)
        IntPtr down2 = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYDOWN, GlobalHotkeyHook.VK_KEY_B, 0, MockKeyState);
        Assert.Equal((IntPtr)1, down2);
        Assert.Equal(1, upCount);

        // Release B -> Consumed
        IntPtr up2 = hook.ProcessHookEvent(GlobalHotkeyHook.WM_KEYUP, GlobalHotkeyHook.VK_KEY_B, 0, MockKeyState);
        Assert.Equal((IntPtr)1, up2);
    }

    [Fact]
    public void FloatingHudController_ClickedEvent_AndNonActivatingWindowMessages()
    {
        var hud = new FloatingHudController();

        bool clicked = false;
        hud.Clicked += () => clicked = true;

        // WM_MOUSEACTIVATE (0x0021) must return MA_NOACTIVATE (3)
        IntPtr ma = hud.DispatchMessageForTesting(0x0021, IntPtr.Zero, IntPtr.Zero);
        Assert.Equal((IntPtr)3, ma);

        // WM_NCHITTEST (0x0084) must return HTCLIENT (1)
        IntPtr ht = hud.DispatchMessageForTesting(0x0084, IntPtr.Zero, IntPtr.Zero);
        Assert.Equal((IntPtr)1, ht);

        // WM_LBUTTONUP (0x0202) must fire Clicked
        hud.DispatchMessageForTesting(0x0202, IntPtr.Zero, IntPtr.Zero);
        Assert.True(clicked, "Floating HUD must fire Clicked event on WM_LBUTTONUP");
    }

    [Fact]
    public async Task VoiceSessionCoordinator_LastTranscript_PreservedAndHistoryRecorded_WhenTargetChanged()
    {
        string tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_target_change_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new SqlitePersonalizationDatabase(tempDbPath);
            var histRepo = new SqliteHistoryRepository(db);
            var privacy = new HistoryPrivacyService();
            var ret = new HistoryRetentionService(histRepo);
            var stats = new ProductivityStatisticsService(histRepo);
            var exp = new HistoryExportService(histRepo);
            var histService = new HistoryService(histRepo, histRepo, stats, ret, exp, privacy);

            var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
            var vad = new EnergyVAD(sampleRate: 16000.0);
            var asrRegistry = new ASREngineRegistry();
            var mockAsr = new MockASREngine(id: "mock-asr")
            {
                DefaultTranscript = "The quick brown fox jumps over the lazy dog",
                SimulatedLatency = TimeSpan.Zero
            };
            asrRegistry.Register(mockAsr, isDefault: true);

            var lang = new DeterministicTextSanitizer();
            var mockInsertion = new TestInsertionService();
            var mockContext = new TargetSwitchedContextService();

            var coordinator = new VoiceSessionCoordinator(
                ringBuffer,
                vad,
                asrRegistry,
                lang,
                mockInsertion,
                contextService: mockContext,
                historyService: histService
            );

            string? transcriptCompletedText = null;
            coordinator.TranscriptCompleted += text => transcriptCompletedText = text;

            // Start session
            await coordinator.StartSessionAsync();

            // Feed 600ms of active speech audio
            float[] loudChunk = new float[1600];
            Array.Fill(loudChunk, 0.1f);
            for (int i = 0; i < 6; i++)
            {
                coordinator.ProcessAudioChunk(loudChunk);
            }

            // End session -> target validation fails
            bool success = await coordinator.EndSessionAsync();

            // Insertion must be aborted for safety
            Assert.False(success, "Insertion must be aborted when active target changed");
            Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);

            // But transcript MUST BE PRESERVED!
            Assert.NotNull(coordinator.LastTranscript);
            Assert.Contains("quick brown fox", coordinator.LastTranscript);
            Assert.Equal(coordinator.LastTranscript, transcriptCompletedText);

            // Allow background history persistence task to complete
            await Task.Delay(200);

            // Verify recorded into history
            var historyItems = await histService.Repository.GetPagedAsync(new HistoryFilter(IncludeDeleted: false), 0, 10);
            Assert.NotEmpty(historyItems.Items);
            Assert.Contains(historyItems.Items, h => h.Text?.Contains("quick brown fox") == true);
        }
        finally
        {
            if (File.Exists(tempDbPath))
            {
                try { File.Delete(tempDbPath); } catch { }
            }
        }
    }

    private sealed class TargetSwitchedContextService : Flow.Core.Context.IUIContextService
    {
        public bool IsFocusInPasswordField() => false;
        public string GetNearbyContext(int maxCharacters = 200) => "";
        public string GetSelectedText(int maxCharacters = 10000) => "";
        public bool HasSelectedText() => false;
        public Flow.Core.Context.ForegroundTargetInfo GetForegroundTargetInfo() => new((IntPtr)9999, 8888, "OtherApp.exe", "Other App");
        public Flow.Core.Context.ContextSnapshot CaptureContext(Guid sessionId, int maxNearbyCharacters = 200, int maxSelectionCharacters = 10000)
        {
            return new Flow.Core.Context.ContextSnapshot(sessionId, DateTimeOffset.UtcNow, new Flow.Core.Context.ForegroundTargetInfo((IntPtr)1234, 5678, "OriginalApp.exe", "Original App"), Flow.Core.Context.ApplicationCategory.GeneralProse, Flow.Core.Context.FocusedControlInfo.Empty, false, "", "");
        }
        public Flow.Core.Context.FocusedControlInfo GetFocusedControlInfo() => Flow.Core.Context.FocusedControlInfo.Empty;
        public Flow.Core.Context.ApplicationCategory GetApplicationCategory(Flow.Core.Context.ForegroundTargetInfo targetInfo) => Flow.Core.Context.ApplicationCategory.GeneralProse;
        public bool ValidateTargetStillActive(Flow.Core.Context.ForegroundTargetInfo capturedTarget) => false; // Reports target switched!
    }

    private sealed class TestInsertionService : ITextInsertionService
    {
        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(new InsertionResult(true, InsertionStrategy.UiaDirect, "TestApp", TimeSpan.FromMilliseconds(5)));

        public Task<bool> BacktrackAsync(Flow.Core.Backtrack.InsertionRecord record, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
