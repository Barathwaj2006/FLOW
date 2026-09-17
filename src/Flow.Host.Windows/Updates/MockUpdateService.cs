using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Host.Windows.Updates;

/// <summary>
/// Mock update service for unit and integration testing.
/// Provides programmatic simulation of update checks, download progress, failures, and restart execution.
/// </summary>
public sealed class MockUpdateService : IFlowUpdateService
{
    public bool IsInstalled { get; set; } = true;
    public string CurrentVersion { get; set; } = "1.0.0";
    public string? SimulatedAvailableVersion { get; set; } = "1.0.1";
    public bool ShouldCheckFindUpdate { get; set; } = true;
    public bool ShouldDownloadSucceed { get; set; } = true;
    public bool ShouldApplySucceed { get; set; } = true;
    public string? SimulatedError { get; set; }

    public int CheckCallCount { get; private set; }
    public int DownloadCallCount { get; private set; }
    public int ApplyCallCount { get; private set; }
    public bool RestartRequested { get; private set; }
    public bool BackgroundTimerActive { get; private set; }

    public UpdateStatusInfo CurrentStatus { get; private set; }

    public event Action<UpdateStatusInfo>? StatusChanged;

    public MockUpdateService(string version = "1.0.0")
    {
        CurrentVersion = version;
        CurrentStatus = new UpdateStatusInfo(UpdateStatus.Idle, CurrentVersion);
    }

    public Task<UpdateStatusInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        CheckCallCount++;
        if (!string.IsNullOrEmpty(SimulatedError))
        {
            var err = SetStatus(UpdateStatus.Failed, 0, SimulatedError, DateTime.UtcNow);
            return Task.FromResult(err);
        }

        if (ShouldCheckFindUpdate && !string.IsNullOrEmpty(SimulatedAvailableVersion))
        {
            var avail = SetStatus(UpdateStatus.UpdateAvailable, 0, null, DateTime.UtcNow, SimulatedAvailableVersion);
            return Task.FromResult(avail);
        }

        var noUp = SetStatus(UpdateStatus.NoUpdateAvailable, 0, null, DateTime.UtcNow);
        return Task.FromResult(noUp);
    }

    public async Task<bool> DownloadUpdatesAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        DownloadCallCount++;
        if (!ShouldDownloadSucceed)
        {
            SetStatus(UpdateStatus.Failed, 0, "Simulated download failure");
            return false;
        }

        SetStatus(UpdateStatus.Downloading, 0, null, null, SimulatedAvailableVersion);
        for (int i = 25; i <= 100; i += 25)
        {
            progress?.Report(i);
            SetStatus(UpdateStatus.Downloading, i, null, null, SimulatedAvailableVersion);
            await Task.Delay(10, cancellationToken);
        }

        SetStatus(UpdateStatus.ReadyToRestart, 100, null, null, SimulatedAvailableVersion);
        return true;
    }

    public Task<bool> ApplyUpdatesAndRestartAsync(bool saveSessionBeforeRestart = true)
    {
        ApplyCallCount++;
        if (!ShouldApplySucceed)
        {
            SetStatus(UpdateStatus.Failed, 0, "Simulated restart failure");
            return Task.FromResult(false);
        }

        RestartRequested = true;
        return Task.FromResult(true);
    }

    public void StartBackgroundCheckTimer(TimeSpan interval)
    {
        BackgroundTimerActive = true;
    }

    public void StopBackgroundCheckTimer()
    {
        BackgroundTimerActive = false;
    }

    public UpdateStatusInfo SetStatus(
        UpdateStatus status,
        int progress = 0,
        string? errorMessage = null,
        DateTime? lastCheckedUtc = null,
        string? availableVersion = null)
    {
        var info = new UpdateStatusInfo(
            Status: status,
            CurrentVersion: CurrentVersion,
            AvailableVersion: availableVersion ?? CurrentStatus.AvailableVersion,
            DownloadProgressPercent: progress,
            ErrorMessage: errorMessage,
            LastCheckedUtc: lastCheckedUtc ?? CurrentStatus.LastCheckedUtc
        );

        CurrentStatus = info;
        StatusChanged?.Invoke(info);
        return info;
    }

    public void Dispose()
    {
        BackgroundTimerActive = false;
    }
}
