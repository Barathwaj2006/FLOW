using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;

namespace Flow.Host.Windows.Diagnostics;

public sealed record DiagnosticsInfo(
    int TotalCrashCount,
    DateTime? LastCrashUtc,
    string? LastCrashReason,
    string LogsDirectory,
    long LogsTotalSizeBytes,
    string CrashesDirectory,
    bool EnableAnonymousTelemetry,
    double UptimeSeconds
);

public interface IFlowDiagnosticsService
{
    Task<DiagnosticsInfo> GetDiagnosticsInfoAsync(CancellationToken ct = default);
    Task<string> ExportDiagnosticsBundleAsync(string? targetDirectory = null, CancellationToken ct = default);
    void OpenLogsFolder();
    void ClearCrashReports();
    Task SetTelemetryOptInAsync(bool enabled, CancellationToken ct = default);
}

public sealed class FlowDiagnosticsService : IFlowDiagnosticsService
{
    private readonly ISettingsRepository? _settingsRepo;
    private readonly string _logsDirectory;
    private readonly string _crashesDirectory;
    private readonly DateTime _processStartTime = DateTime.UtcNow;

    public FlowDiagnosticsService(
        ISettingsRepository? settingsRepo = null,
        string? logsDirectory = null,
        string? crashesDirectory = null)
    {
        _settingsRepo = settingsRepo;
        _logsDirectory = string.IsNullOrWhiteSpace(logsDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW", "logs")
            : logsDirectory;

        _crashesDirectory = string.IsNullOrWhiteSpace(crashesDirectory)
            ? CrashReporter.CrashesDirectory
            : crashesDirectory;
    }

    public async Task<DiagnosticsInfo> GetDiagnosticsInfoAsync(CancellationToken ct = default)
    {
        long logsSize = 0;
        try
        {
            if (Directory.Exists(_logsDirectory))
            {
                foreach (var file in Directory.GetFiles(_logsDirectory, "*.log"))
                {
                    logsSize += new FileInfo(file).Length;
                }
            }
        }
        catch { }

        var manifest = CrashReporter.CheckPreviousCrash();
        bool telemetryOptIn = false;

        if (_settingsRepo != null)
        {
            var settings = await _settingsRepo.LoadSettingsAsync(ct);
            telemetryOptIn = settings.EnableAnonymousTelemetry;
        }

        double uptime = (DateTime.UtcNow - _processStartTime).TotalSeconds;

        return new DiagnosticsInfo(
            TotalCrashCount: manifest?.TotalCrashCount ?? 0,
            LastCrashUtc: manifest?.LastCrashUtc,
            LastCrashReason: manifest?.LastCrashReason,
            LogsDirectory: _logsDirectory,
            LogsTotalSizeBytes: logsSize,
            CrashesDirectory: _crashesDirectory,
            EnableAnonymousTelemetry: telemetryOptIn,
            UptimeSeconds: Math.Round(uptime, 1)
        );
    }

    public async Task<string> ExportDiagnosticsBundleAsync(string? targetDirectory = null, CancellationToken ct = default)
    {
        string outDir = string.IsNullOrWhiteSpace(targetDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : targetDirectory;

        if (string.IsNullOrWhiteSpace(outDir))
        {
            outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW");
        }

        Directory.CreateDirectory(outDir);

        string zipName = $"FLOW-Diagnostics-{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        string zipPath = Path.Combine(outDir, zipName);

        // Create temporary export directory
        string tempDir = Path.Combine(Path.GetTempPath(), $"flow_diag_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 1. Generate sanitized system summary
            var sysInfo = new
            {
                ExportTimestampUtc = DateTime.UtcNow,
                AppVersion = typeof(FlowDiagnosticsService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                OsVersion = Environment.OSVersion.VersionString,
                ProcessorCount = Environment.ProcessorCount,
                Is64BitProcess = Environment.Is64BitProcess,
                WorkingSetMB = Math.Round(Environment.WorkingSet / (1024.0 * 1024.0), 2)
            };
            await File.WriteAllTextAsync(Path.Combine(tempDir, "system_summary.json"),
                JsonSerializer.Serialize(sysInfo, new JsonSerializerOptions { WriteIndented = true }), ct);

            // 2. Copy recent sanitized logs (last 3 days)
            if (Directory.Exists(_logsDirectory))
            {
                string logsSubDir = Path.Combine(tempDir, "logs");
                Directory.CreateDirectory(logsSubDir);

                var logFiles = Directory.GetFiles(_logsDirectory, "*.log");
                var cutoff = DateTime.UtcNow.AddDays(-3);
                foreach (var log in logFiles)
                {
                    var fi = new FileInfo(log);
                    if (fi.LastWriteTimeUtc >= cutoff)
                    {
                        string dest = Path.Combine(logsSubDir, Path.GetFileName(log));
                        File.Copy(log, dest, overwrite: true);
                    }
                }
            }

            // 3. Copy crash manifest and metadata if available (strictly json metadata, excluding raw memory dumps)
            if (Directory.Exists(_crashesDirectory))
            {
                string crashSubDir = Path.Combine(tempDir, "crashes");
                Directory.CreateDirectory(crashSubDir);

                foreach (var jsonFile in Directory.GetFiles(_crashesDirectory, "*.json"))
                {
                    string dest = Path.Combine(crashSubDir, Path.GetFileName(jsonFile));
                    File.Copy(jsonFile, dest, overwrite: true);
                }
            }

            // 4. Archive into zip
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);

            return zipPath;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr ILCreateFromPathW(string pszPath);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern void ILFree(IntPtr pidl);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItem(IntPtr pidlFolder, uint cidl, IntPtr apidl, uint dwFlags);

    public void OpenLogsFolder()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory)) Directory.CreateDirectory(_logsDirectory);

            IntPtr pidl = ILCreateFromPathW(_logsDirectory);
            if (pidl != IntPtr.Zero)
            {
                try
                {
                    SHOpenFolderAndSelectItem(pidl, 0, IntPtr.Zero, 0);
                }
                finally
                {
                    ILFree(pidl);
                }
            }
        }
        catch { }
    }

    public void ClearCrashReports()
    {
        CrashReporter.ClearCrashDumps();
    }

    public async Task SetTelemetryOptInAsync(bool enabled, CancellationToken ct = default)
    {
        if (_settingsRepo == null) return;
        var current = await _settingsRepo.LoadSettingsAsync(ct);
        var updated = current with { EnableAnonymousTelemetry = enabled };
        await _settingsRepo.SaveSettingsAsync(updated, ct);
    }
}
