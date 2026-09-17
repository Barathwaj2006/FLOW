using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Host.Windows.UI;
using Flow.Host.Windows.Updates;
using Xunit;

namespace Flow.Windows.Tests;

public sealed class VelopackUpdateServiceTests
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
    public void MockUpdateService_InitialState_IsIdle()
    {
        var mock = new MockUpdateService("1.2.3");

        Assert.Equal("1.2.3", mock.CurrentVersion);
        Assert.Equal(UpdateStatus.Idle, mock.CurrentStatus.Status);
        Assert.Equal("1.2.3", mock.CurrentStatus.CurrentVersion);
        Assert.Equal(0, mock.CurrentStatus.DownloadProgressPercent);
        Assert.Null(mock.CurrentStatus.AvailableVersion);
        Assert.False(mock.RestartRequested);
        Assert.False(mock.BackgroundTimerActive);
    }

    [Fact]
    public async Task MockUpdateService_CheckForUpdates_FindsUpdate_TransitionsState()
    {
        var mock = new MockUpdateService("1.0.0")
        {
            SimulatedAvailableVersion = "1.1.0",
            ShouldCheckFindUpdate = true
        };

        UpdateStatusInfo? capturedEvent = null;
        mock.StatusChanged += status => capturedEvent = status;

        var result = await mock.CheckForUpdatesAsync();

        Assert.Equal(1, mock.CheckCallCount);
        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.1.0", result.AvailableVersion);
        Assert.NotNull(capturedEvent);
        Assert.Equal(UpdateStatus.UpdateAvailable, capturedEvent!.Status);
        Assert.Equal("1.1.0", capturedEvent.AvailableVersion);
    }

    [Fact]
    public async Task MockUpdateService_CheckForUpdates_NoUpdateAvailable_ReturnsNoUpdate()
    {
        var mock = new MockUpdateService("1.0.0")
        {
            ShouldCheckFindUpdate = false
        };

        var result = await mock.CheckForUpdatesAsync();

        Assert.Equal(UpdateStatus.NoUpdateAvailable, result.Status);
        Assert.Null(result.AvailableVersion);
    }

    [Fact]
    public async Task MockUpdateService_CheckForUpdates_SimulatedError_FailsGracefully()
    {
        var mock = new MockUpdateService("1.0.0")
        {
            SimulatedError = "GitHub API rate limit exceeded"
        };

        var result = await mock.CheckForUpdatesAsync();

        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.Equal("GitHub API rate limit exceeded", result.ErrorMessage);
    }

    [Fact]
    public async Task MockUpdateService_DownloadUpdates_ReportsProgressAndReachesReadyToRestart()
    {
        var mock = new MockUpdateService("1.0.0")
        {
            SimulatedAvailableVersion = "1.1.0"
        };

        int lastReported = 0;
        var progress = new Progress<int>(p => lastReported = p);

        bool success = await mock.DownloadUpdatesAsync(progress);

        Assert.True(success);
        Assert.Equal(1, mock.DownloadCallCount);
        Assert.Equal(100, lastReported);
        Assert.Equal(UpdateStatus.ReadyToRestart, mock.CurrentStatus.Status);
        Assert.Equal(100, mock.CurrentStatus.DownloadProgressPercent);
        Assert.Equal("1.1.0", mock.CurrentStatus.AvailableVersion);
    }

    [Fact]
    public async Task MockUpdateService_DownloadUpdates_Failure_SetsFailedState()
    {
        var mock = new MockUpdateService("1.0.0")
        {
            ShouldDownloadSucceed = false
        };

        bool success = await mock.DownloadUpdatesAsync();

        Assert.False(success);
        Assert.Equal(UpdateStatus.Failed, mock.CurrentStatus.Status);
        Assert.NotNull(mock.CurrentStatus.ErrorMessage);
    }

    [Fact]
    public async Task MockUpdateService_ApplyUpdatesAndRestart_SetsRestartFlag()
    {
        var mock = new MockUpdateService("1.0.0");

        bool success = await mock.ApplyUpdatesAndRestartAsync();

        Assert.True(success);
        Assert.Equal(1, mock.ApplyCallCount);
        Assert.True(mock.RestartRequested);
    }

    [Fact]
    public void MockUpdateService_BackgroundTimer_ControlsActiveFlag()
    {
        var mock = new MockUpdateService();

        mock.StartBackgroundCheckTimer(TimeSpan.FromMinutes(30));
        Assert.True(mock.BackgroundTimerActive);

        mock.StopBackgroundCheckTimer();
        Assert.False(mock.BackgroundTimerActive);
    }

    [Fact]
    public void VelopackUpdateService_InstantiatesSafely_InDevEnvironment()
    {
        using var service = new VelopackUpdateService();

        Assert.NotNull(service.CurrentVersion);
        Assert.NotEmpty(service.CurrentVersion);
        Assert.False(service.IsInstalled);
        Assert.Equal(UpdateStatus.Idle, service.CurrentStatus.Status);
        Assert.Equal(service.CurrentVersion, service.CurrentStatus.CurrentVersion);
    }

    [Fact]
    public async Task VelopackUpdateService_DevMode_CheckForUpdates_ReturnsNoUpdateSafely()
    {
        using var service = new VelopackUpdateService();

        // In test runner / uninstalled dev mode, CheckForUpdatesAsync must safely return NoUpdateAvailable
        var status = await service.CheckForUpdatesAsync();

        Assert.Equal(UpdateStatus.NoUpdateAvailable, status.Status);
        Assert.Null(status.ErrorMessage);
    }

    [Fact]
    public async Task VelopackUpdateService_DevMode_DownloadUpdates_ReturnsFalseSafely()
    {
        using var service = new VelopackUpdateService();

        bool downloaded = await service.DownloadUpdatesAsync();
        Assert.False(downloaded);
    }

    [Fact]
    public async Task VelopackUpdateService_DevMode_ApplyUpdates_ReturnsFalseSafely()
    {
        using var service = new VelopackUpdateService();

        bool applied = await service.ApplyUpdatesAndRestartAsync();
        Assert.False(applied);
    }

    [Fact]
    public void VelopackUpdateService_BackgroundTimer_StartsAndStopsWithoutError()
    {
        using var service = new VelopackUpdateService();

        service.StartBackgroundCheckTimer(TimeSpan.FromMinutes(10));
        service.StopBackgroundCheckTimer();
    }

    [Fact]
    public void FlowShellWindow_HandleAction_GetUpdateStatus_ReturnsValidStatus()
    {
        RunOnSta(() =>
        {
            var mock = new MockUpdateService("1.2.3");
            var window = new FlowShellWindow(updateService: mock);

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
            window.HandleActionAsync("req-update-1", "get-update-status", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("get-update-status", respondedAction);
            Assert.True(wasSuccess);
            Assert.NotNull(responsePayload);

            string json = JsonSerializer.Serialize(responsePayload);
            Assert.Contains("\"currentVersion\":\"1.2.3\"", json);
            Assert.Contains("\"status\":\"Idle\"", json);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_CheckUpdate_CallsServiceAndReturnsResult()
    {
        RunOnSta(() =>
        {
            var mock = new MockUpdateService("1.0.0")
            {
                SimulatedAvailableVersion = "1.5.0",
                ShouldCheckFindUpdate = true
            };
            var window = new FlowShellWindow(updateService: mock);

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
            window.HandleActionAsync("req-update-2", "check-update", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("check-update", respondedAction);
            Assert.True(wasSuccess);
            Assert.Equal(1, mock.CheckCallCount);

            string json = JsonSerializer.Serialize(responsePayload);
            Assert.Contains("\"status\":\"UpdateAvailable\"", json);
            Assert.Contains("\"availableVersion\":\"1.5.0\"", json);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_DownloadUpdate_CallsServiceAndReturnsSuccess()
    {
        RunOnSta(() =>
        {
            var mock = new MockUpdateService("1.0.0");
            var window = new FlowShellWindow(updateService: mock);

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
            window.HandleActionAsync("req-update-3", "download-update", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("download-update", respondedAction);
            Assert.True(wasSuccess);
            Assert.Equal(1, mock.DownloadCallCount);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_ApplyUpdate_CallsServiceAndReturnsSuccess()
    {
        RunOnSta(() =>
        {
            var mock = new MockUpdateService("1.0.0");
            var window = new FlowShellWindow(updateService: mock);

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
            window.HandleActionAsync("req-update-4", "apply-update", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("apply-update", respondedAction);
            Assert.True(wasSuccess);
            Assert.Equal(1, mock.ApplyCallCount);
            Assert.True(mock.RestartRequested);
        });
    }

    [Fact]
    public void FlowShellWindow_UpdateStatusChanged_BroadcastsEvent()
    {
        RunOnSta(() =>
        {
            var mock = new MockUpdateService("1.0.0");
            var window = new FlowShellWindow(updateService: mock);

            string? broadcastEvent = null;
            object? broadcastPayload = null;

            window.EventBroadcasted += (ev, payload) =>
            {
                broadcastEvent = ev;
                broadcastPayload = payload;
            };

            mock.SetStatus(UpdateStatus.ReadyToRestart, 100, null, null, "1.2.0");

            Assert.Equal("update-status-changed", broadcastEvent);
            Assert.NotNull(broadcastPayload);

            string json = JsonSerializer.Serialize(broadcastPayload);
            Assert.Contains("\"status\":\"ReadyToRestart\"", json);
            Assert.Contains("\"availableVersion\":\"1.2.0\"", json);
        });
    }
}
