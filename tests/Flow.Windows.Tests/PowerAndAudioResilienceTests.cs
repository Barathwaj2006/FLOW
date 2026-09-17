using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TranscriptProcessing;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.UI;
using Flow.Inference;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Flow.Windows.Tests;

/// <summary>
/// Comprehensive physical and unit tests for Step 5: Power & Audio Resilience.
/// Verifies Windows sleep/wake transitions, WASAPI COM endpoint invalidation/disconnection,
/// debounce protections, and rollover to fallback default capture endpoints.
/// </summary>
public class PowerAndAudioResilienceTests
{
    [Fact]
    public void WindowsPowerStateManager_InitialState_IsNotSuspended()
    {
        using var manager = new WindowsPowerStateManager(IntPtr.Zero, NullLogger<WindowsPowerStateManager>.Instance);
        Assert.False(manager.IsSuspended);
    }

    [Fact]
    public void WindowsPowerStateManager_SimulateSuspend_SetsIsSuspendedTrue_AndFiresSuspending()
    {
        using var manager = new WindowsPowerStateManager(IntPtr.Zero, NullLogger<WindowsPowerStateManager>.Instance);
        bool suspendingFired = false;
        int receivedBroadcast = 0;

        manager.Suspending += () => suspendingFired = true;
        manager.PowerBroadcastReceived += ev => receivedBroadcast = ev;

        manager.SimulateSuspend();

        Assert.True(manager.IsSuspended);
        Assert.True(suspendingFired);
        Assert.Equal(WindowsPowerStateManager.PBT_APMSUSPEND, receivedBroadcast);
    }

    [Fact]
    public void WindowsPowerStateManager_SimulateResume_SetsIsSuspendedFalse_AndFiresResuming()
    {
        using var manager = new WindowsPowerStateManager(IntPtr.Zero, NullLogger<WindowsPowerStateManager>.Instance);
        manager.SimulateSuspend();
        Assert.True(manager.IsSuspended);

        bool resumingFired = false;
        int receivedBroadcast = 0;

        manager.Resuming += () => resumingFired = true;
        manager.PowerBroadcastReceived += ev => receivedBroadcast = ev;

        manager.SimulateResume();

        Assert.False(manager.IsSuspended);
        Assert.True(resumingFired);
        Assert.Equal(WindowsPowerStateManager.PBT_APMRESUMEAUTOMATIC, receivedBroadcast);
    }

    [Fact]
    public void WindowsPowerStateManager_ProcessPowerBroadcast_HandlesPBT_APMRESUMESUSPEND()
    {
        using var manager = new WindowsPowerStateManager(IntPtr.Zero, NullLogger<WindowsPowerStateManager>.Instance);
        manager.SimulateSuspend();

        bool resumingFired = false;
        manager.Resuming += () => resumingFired = true;

        manager.ProcessPowerBroadcast(WindowsPowerStateManager.PBT_APMRESUMESUSPEND);

        Assert.False(manager.IsSuspended);
        Assert.True(resumingFired);
    }

    [Fact]
    public void FloatingHudController_CustomWndProc_ForwardsWM_POWERBROADCAST()
    {
        using var hud = new FloatingHudController();
        int receivedPowerEvent = 0;
        hud.PowerBroadcastReceived += ev => receivedPowerEvent = ev;

        IntPtr result = hud.DispatchMessageForTesting(
            WindowsPowerStateManager.WM_POWERBROADCAST,
            (IntPtr)WindowsPowerStateManager.PBT_APMSUSPEND,
            IntPtr.Zero);

        Assert.Equal((IntPtr)1, result);
        Assert.Equal(WindowsPowerStateManager.PBT_APMSUSPEND, receivedPowerEvent);
    }

    [Theory]
    [InlineData(unchecked((int)0x88890004), true)]  // AUDCLNT_E_DEVICE_INVALIDATED
    [InlineData(unchecked((int)0x88890026), true)]  // AUDCLNT_E_RESOURCES_INVALIDATED
    [InlineData(unchecked((int)0x88890020), true)]  // AUDCLNT_E_SERVICE_NOT_RUNNING
    [InlineData(unchecked((int)0x8889000F), true)]  // AUDCLNT_E_ENDPOINT_CREATE_FAILED
    [InlineData(0, false)]                          // S_OK
    [InlineData(1, false)]                          // S_FALSE
    [InlineData(unchecked((int)0x80004005), false)] // E_FAIL
    public void WasapiAudioCapture_IsDeviceInvalidatedError_CorrectlyCategorizesHResults(int hr, bool expectedInvalidated)
    {
        bool actual = WasapiAudioCapture.IsDeviceInvalidatedError(hr);
        Assert.Equal(expectedInvalidated, actual);
    }

    [Fact]
    public void WasapiAudioCapture_DeviceInvalidatedEvent_FiresWithDeviceName()
    {
        using var capture = new WasapiAudioCapture();
        string? notifiedDevice = null;
        capture.DeviceInvalidated += dev => notifiedDevice = dev;

        capture.SimulateDeviceInvalidated("Headset-Mic-Endpoint-1");

        Assert.Equal("Headset-Mic-Endpoint-1", notifiedDevice);
    }

    [Fact]
    public void WasapiAudioCapture_SwitchToDevice_UpdatesTargetAndFiresDeviceSwitched()
    {
        using var capture = new WasapiAudioCapture();
        string? switchedDevice = null;
        capture.DeviceSwitched += dev => switchedDevice = dev;

        Assert.Null(capture.TargetDeviceId);

        capture.SwitchToDevice("USB-Mic-Endpoint-2");

        Assert.Equal("USB-Mic-Endpoint-2", capture.TargetDeviceId);
        Assert.Equal("USB-Mic-Endpoint-2", switchedDevice);

        capture.SwitchToDevice(null);
        Assert.Null(capture.TargetDeviceId);
        Assert.Null(switchedDevice);
    }

    [Fact]
    public void WasapiDeviceManager_RefreshDevices_ExecutesSafely()
    {
        using var manager = new WasapiDeviceManager(NullLogger<WasapiDeviceManager>.Instance);
        var devices = manager.RefreshDevices();

        Assert.NotNull(devices);
        foreach (var dev in devices)
        {
            Assert.False(string.IsNullOrEmpty(dev.Id));
            Assert.False(string.IsNullOrEmpty(dev.Name));
        }
    }

    [Fact]
    public void WasapiDeviceManager_IsDeviceActive_ReturnsFalseForBogusDeviceId()
    {
        using var manager = new WasapiDeviceManager(NullLogger<WasapiDeviceManager>.Instance);
        bool isActive = manager.IsDeviceActive("{00000000-0000-0000-0000-000000000000}");
        Assert.False(isActive);
    }

    [Fact]
    public void WasapiDeviceManager_IsDeviceActive_ReturnsTrueForDefaultDeviceIfPresent()
    {
        using var manager = new WasapiDeviceManager(NullLogger<WasapiDeviceManager>.Instance);
        string? defaultId = manager.GetDefaultCaptureDeviceId();
        if (!string.IsNullOrEmpty(defaultId))
        {
            bool isActive = manager.IsDeviceActive(defaultId);
            Assert.True(isActive);
        }
    }

    [Fact]
    public void WasapiDeviceManager_SimulateEvents_TriggersSubscribers()
    {
        using var manager = new WasapiDeviceManager(NullLogger<WasapiDeviceManager>.Instance);
        string? defaultChangedId = null;
        string? stateChangedId = null;
        int newStateValue = 0;

        manager.DefaultDeviceChanged += id => defaultChangedId = id;
        manager.DeviceStateChanged += (id, state) =>
        {
            stateChangedId = id;
            newStateValue = state;
        };

        manager.SimulateDefaultDeviceChanged("new-default-endpoint");
        manager.SimulateDeviceStateChanged("endpoint-123", 1);

        Assert.Equal("new-default-endpoint", defaultChangedId);
        Assert.Equal("endpoint-123", stateChangedId);
        Assert.Equal(1, newStateValue);
    }

    [Fact]
    public async Task SystemSleep_Simulation_CancelsActiveVoiceSessionCleanly()
    {
        // Assemble session coordinator
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
        var vad = new EnergyVAD(sampleRate: 16000.0);
        var registry = new ASREngineRegistry();
        registry.Register(new MockASREngine(), isDefault: true);
        var pipeline = new TranscriptProcessingPipeline();
        var insertion = new WindowsTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer, vad, registry, pipeline, insertion,
            NullLogger<VoiceSessionCoordinator>.Instance);

        using var powerManager = new WindowsPowerStateManager(IntPtr.Zero, NullLogger<WindowsPowerStateManager>.Instance);
        using var capture = new WasapiAudioCapture();

        powerManager.Suspending += () =>
        {
            capture.Stop();
            _ = coordinator.CancelSessionAsync("System sleep");
        };

        // Start session
        bool started = await coordinator.StartSessionAsync(isHandsFree: true);
        Assert.True(started);
        Assert.Equal(SessionState.Recording, coordinator.CurrentState);

        // Simulate sleep broadcast
        powerManager.SimulateSuspend();

        // Allow async cancellation to complete
        await Task.Delay(150);

        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.False(capture.IsCapturing);
    }

    [Fact]
    public async Task AudioDeviceInvalidation_Simulation_RecoversGracefully()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
        var vad = new EnergyVAD(sampleRate: 16000.0);
        var registry = new ASREngineRegistry();
        registry.Register(new MockASREngine(), isDefault: true);
        var pipeline = new TranscriptProcessingPipeline();
        var insertion = new WindowsTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer, vad, registry, pipeline, insertion,
            NullLogger<VoiceSessionCoordinator>.Instance);

        using var devManager = new WasapiDeviceManager(NullLogger<WasapiDeviceManager>.Instance);
        using var capture = new WasapiAudioCapture(deviceManager: devManager);

        bool rolledOver = false;
        string? targetFallback = null;

        capture.DeviceInvalidated += oldId =>
        {
            _ = Task.Run(async () =>
            {
                if (coordinator.CurrentState == SessionState.Recording)
                {
                    await coordinator.CancelSessionAsync("Microphone unplugged");
                }
                devManager.RefreshDevices();
                targetFallback = devManager.GetDefaultCaptureDeviceId();
                capture.SwitchToDevice(targetFallback);
                rolledOver = true;
            });
        };

        // Start session
        bool started = await coordinator.StartSessionAsync(isHandsFree: true);
        Assert.True(started);
        Assert.Equal(SessionState.Recording, coordinator.CurrentState);

        // Simulate USB unplug
        capture.SimulateDeviceInvalidated("USB-Headset-Unplugged");

        // Allow async recovery
        await Task.Delay(250);

        Assert.True(rolledOver);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.False(capture.IsCapturing);
    }
}
