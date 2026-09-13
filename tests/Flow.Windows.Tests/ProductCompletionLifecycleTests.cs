using System;
using Flow.Host.Windows.Lifecycle;
using Flow.Host.Windows.Tray;
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
}
