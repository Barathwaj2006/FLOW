using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Host.Windows.Updates;

/// <summary>
/// Status of the application updater lifecycle.
/// </summary>
public enum UpdateStatus
{
    Idle,
    Checking,
    UpdateAvailable,
    NoUpdateAvailable,
    Downloading,
    ReadyToRestart,
    Failed
}

/// <summary>
/// Snapshot of the current update state, versions, and download progress.
/// </summary>
public sealed record UpdateStatusInfo(
    UpdateStatus Status,
    string CurrentVersion,
    string? AvailableVersion = null,
    int DownloadProgressPercent = 0,
    string? ErrorMessage = null,
    DateTime? LastCheckedUtc = null
);

/// <summary>
/// Abstraction for checking, downloading, and applying background desktop application updates.
/// Decouples UI and Host coordination from unmanaged installer execution.
/// </summary>
public interface IFlowUpdateService : IDisposable
{
    /// <summary>
    /// Gets the current status and version metrics of the update engine.
    /// </summary>
    UpdateStatusInfo CurrentStatus { get; }

    /// <summary>
    /// Returns true if the application is running from an official installed package (capable of updating).
    /// Returns false if running in development mode or unpacked build.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Current semantic version string of the host application.
    /// </summary>
    string CurrentVersion { get; }

    /// <summary>
    /// Event triggered when update status, available version, or download percentage changes.
    /// </summary>
    event Action<UpdateStatusInfo>? StatusChanged;

    /// <summary>
    /// Queries the release feed for new versions.
    /// </summary>
    Task<UpdateStatusInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the latest update (full or delta binary patch) with progress reporting.
    /// </summary>
    Task<bool> DownloadUpdatesAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares session shutdown, applies the downloaded update, and restarts the application.
    /// </summary>
    Task<bool> ApplyUpdatesAndRestartAsync(bool saveSessionBeforeRestart = true);

    /// <summary>
    /// Starts periodic background update checks at the specified interval.
    /// </summary>
    void StartBackgroundCheckTimer(TimeSpan interval);

    /// <summary>
    /// Stops any active background update polling timer.
    /// </summary>
    void StopBackgroundCheckTimer();
}
