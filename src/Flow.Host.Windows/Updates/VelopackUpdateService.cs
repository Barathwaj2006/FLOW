using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Session;
using Flow.Host.Windows.Native;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace Flow.Host.Windows.Updates;

/// <summary>
/// Production Velopack update manager for FLOW Windows Native desktop app.
/// Interfaces with GitHub Releases or custom update feeds, handles delta binary patching,
/// manages background check timers, and executes safe session termination before restarting.
/// </summary>
public sealed class VelopackUpdateService : IFlowUpdateService
{
    public const string DefaultGithubRepositoryUrl = "https://github.com/Barathwaj2006/FLOW";

    private readonly ILogger<VelopackUpdateService>? _logger;
    private readonly VoiceSessionCoordinator? _coordinator;
    private readonly WasapiAudioCapture? _capture;
    private readonly UpdateManager? _updateManager;
    private readonly string _feedUrl;
    private UpdateInfo? _latestUpdateInfo;
    private Timer? _backgroundTimer;
    private readonly object _lock = new();
    private bool _isDisposed;

    public bool IsInstalled => _updateManager?.IsInstalled ?? false;

    public string CurrentVersion { get; }

    public UpdateStatusInfo CurrentStatus { get; private set; }

    public event Action<UpdateStatusInfo>? StatusChanged;

    public VelopackUpdateService(
        VoiceSessionCoordinator? coordinator = null,
        WasapiAudioCapture? capture = null,
        string? feedUrl = null,
        ILogger<VelopackUpdateService>? logger = null)
    {
        _coordinator = coordinator;
        _capture = capture;
        _logger = logger;
        _feedUrl = string.IsNullOrWhiteSpace(feedUrl) ? DefaultGithubRepositoryUrl : feedUrl;

        // Resolve application assembly version
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        CurrentVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        try
        {
            var source = new GithubSource(_feedUrl, accessToken: null, prerelease: false);
            _updateManager = new UpdateManager(source);
            _logger?.LogInformation("Velopack UpdateManager initialized. Feed: {Feed}, Installed: {IsInstalled}, AppVersion: {Version}",
                _feedUrl, _updateManager.IsInstalled, CurrentVersion);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to initialize Velopack UpdateManager. Running in standalone fallback mode.");
            _updateManager = null;
        }

        CurrentStatus = new UpdateStatusInfo(
            Status: Updates.UpdateStatus.Idle,
            CurrentVersion: CurrentVersion
        );
    }

    /// <inheritdoc />
    public async Task<UpdateStatusInfo> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Checking for application updates against {Feed}...", _feedUrl);
        SetStatus(Updates.UpdateStatus.Checking, 0, null);

        if (_updateManager == null || !_updateManager.IsInstalled)
        {
            _logger?.LogInformation("Application is running in uninstalled/dev mode. Bypassing remote update check.");
            var status = SetStatus(Updates.UpdateStatus.NoUpdateAvailable, 0, null, DateTime.UtcNow);
            return status;
        }

        try
        {
            var updateInfo = await _updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            _latestUpdateInfo = updateInfo;

            if (updateInfo != null && updateInfo.TargetFullRelease != null)
            {
                string targetVer = updateInfo.TargetFullRelease.Version?.ToString() ?? "Newer";
                _logger?.LogInformation("Update available! Target Version: {TargetVersion}", targetVer);
                return SetStatus(Updates.UpdateStatus.UpdateAvailable, 0, null, DateTime.UtcNow, targetVer);
            }
            else
            {
                _logger?.LogInformation("FLOW is currently up-to-date ({Version}).", CurrentVersion);
                return SetStatus(Updates.UpdateStatus.NoUpdateAvailable, 0, null, DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception while checking for updates.");
            return SetStatus(Updates.UpdateStatus.Failed, 0, ex.Message, DateTime.UtcNow);
        }
    }

    /// <inheritdoc />
    public async Task<bool> DownloadUpdatesAsync(IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (_latestUpdateInfo == null)
        {
            _logger?.LogWarning("DownloadUpdatesAsync called but no update has been checked or available.");
            return false;
        }

        if (_updateManager == null || !_updateManager.IsInstalled)
        {
            _logger?.LogWarning("Cannot download updates in uninstalled/dev environment.");
            return false;
        }

        try
        {
            string targetVer = _latestUpdateInfo.TargetFullRelease?.Version?.ToString() ?? "Unknown";
            _logger?.LogInformation("Starting download of update package {TargetVersion}...", targetVer);
            SetStatus(Updates.UpdateStatus.Downloading, 0, null, null, targetVer);

            var internalProgress = new Action<int>(percent =>
            {
                progress?.Report(percent);
                SetStatus(Updates.UpdateStatus.Downloading, percent, null, null, targetVer);
            });

            await _updateManager.DownloadUpdatesAsync(_latestUpdateInfo, internalProgress, cancellationToken).ConfigureAwait(false);

            _logger?.LogInformation("Update download complete. Package verified and ready to apply on restart.");
            SetStatus(Updates.UpdateStatus.ReadyToRestart, 100, null, null, targetVer);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download update package.");
            SetStatus(Updates.UpdateStatus.Failed, 0, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ApplyUpdatesAndRestartAsync(bool saveSessionBeforeRestart = true)
    {
        if (_latestUpdateInfo == null || _updateManager == null || !_updateManager.IsInstalled)
        {
            _logger?.LogWarning("ApplyUpdatesAndRestart called without a valid downloaded update or in dev mode.");
            return false;
        }

        try
        {
            _logger?.LogInformation("Preparing application for restart into updated version...");

            // 1. Safe capture and coordinator teardown
            if (saveSessionBeforeRestart)
            {
                try
                {
                    _capture?.Stop();
                    if (_coordinator != null && _coordinator.CurrentState == SessionState.Recording)
                    {
                        await _coordinator.EndSessionAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Non-fatal error during pre-restart session cleanup.");
                }
            }

            // 2. Invoke Velopack update and restart
            _logger?.LogInformation("Executing Velopack ApplyUpdatesAndRestart...");
            _updateManager.ApplyUpdatesAndRestart(_latestUpdateInfo);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply update and restart.");
            SetStatus(Updates.UpdateStatus.Failed, 0, ex.Message);
            return false;
        }
    }

    /// <inheritdoc />
    public void StartBackgroundCheckTimer(TimeSpan interval)
    {
        lock (_lock)
        {
            StopBackgroundCheckTimer();

            // Initial check delayed 45 seconds after launch, then periodic
            _backgroundTimer = new Timer(async _ =>
            {
                try
                {
                    await CheckForUpdatesAsync().ConfigureAwait(false);
                }
                catch { }
            }, null, TimeSpan.FromSeconds(45), interval);

            _logger?.LogInformation("Background update polling timer armed. Interval: {IntervalHours:F1} hours.", interval.TotalHours);
        }
    }

    /// <inheritdoc />
    public void StopBackgroundCheckTimer()
    {
        lock (_lock)
        {
            if (_backgroundTimer != null)
            {
                _backgroundTimer.Dispose();
                _backgroundTimer = null;
            }
        }
    }

    private UpdateStatusInfo SetStatus(
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
        if (!_isDisposed)
        {
            StopBackgroundCheckTimer();
            _isDisposed = true;
        }
    }
}
