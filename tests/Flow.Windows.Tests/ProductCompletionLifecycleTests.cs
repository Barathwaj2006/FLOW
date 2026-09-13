using System;
using System.Reflection;
using System.Threading;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Commands;
using Flow.Core.Language;
using Flow.Core.Session;
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
            Assert.Equal("Toggle Dictation", window.BtnToggleDictation.Content);
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
}
