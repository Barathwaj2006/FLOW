using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Flow.Host.Windows.Diagnostics;

/// <summary>
/// Native Windows post-mortem crash reporter.
/// Intercepts unhandled managed and native exceptions, generates compact minidumps via DbgHelp.dll,
/// sanitizes crash metadata, and records crash manifests for startup recovery diagnostics.
/// </summary>
public static class CrashReporter
{
    private const int MiniDumpNormal = 0x00000000;
    private const int MiniDumpWithThreadInfo = 0x00001000;
    private const int MiniDumpWithUnloadedModules = 0x00000020;
    private const int DefaultDumpType = MiniDumpNormal | MiniDumpWithThreadInfo | MiniDumpWithUnloadedModules;

    [DllImport("dbghelp.dll", EntryPoint = "MiniDumpWriteDump", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MiniDumpWriteDump(
        IntPtr hProcess,
        uint processId,
        SafeFileHandle hFile,
        int dumpType,
        IntPtr exceptionParam,
        IntPtr userStreamParam,
        IntPtr callbackParam);

    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static string? _customCrashesDir;
    private static bool _isInstalled;
    private static readonly object SyncLock = new();

    public static string CrashesDirectory => _customCrashesDir ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FLOW", "crashes");

    public static void SetCustomCrashesDirectory(string? directory)
    {
        _customCrashesDir = directory;
    }

    /// <summary>
    /// Installs unhandled exception handlers on AppDomain and TaskScheduler.
    /// </summary>
    public static void Install()
    {
        lock (SyncLock)
        {
            if (_isInstalled) return;
            _isInstalled = true;

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }
    }

    public static void Uninstall()
    {
        lock (SyncLock)
        {
            if (!_isInstalled) return;
            _isInstalled = false;

            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        WriteCrashDump(ex, e.IsTerminating ? "FatalUnhandledException" : "NonFatalUnhandledException");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashDump(e.Exception, "UnobservedTaskException");
    }

    /// <summary>
    /// Generates a post-mortem minidump and sanitized crash metadata JSON file.
    /// Returns the paths of the written files, or null if writing failed.
    /// </summary>
    public static (string? dumpPath, string? metadataPath) WriteCrashDump(Exception? ex, string reason = "ManualCrashReport")
    {
        lock (SyncLock)
        {
            try
            {
                string crashesDir = CrashesDirectory;
                Directory.CreateDirectory(crashesDir);

                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string dumpFilename = $"flow_crash_{timestamp}.dmp";
                string metaFilename = $"flow_crash_{timestamp}.json";

                string dumpPath = Path.Combine(crashesDir, dumpFilename);
                string metaPath = Path.Combine(crashesDir, metaFilename);

                // 1. Write Native MiniDump
                bool dumpSucceeded = false;
                try
                {
                    using var currentProc = Process.GetCurrentProcess();
                    using var fileStream = new FileStream(dumpPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    dumpSucceeded = MiniDumpWriteDump(
                        currentProc.Handle,
                        (uint)currentProc.Id,
                        fileStream.SafeFileHandle,
                        DefaultDumpType,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero);
                }
                catch { }

                // 2. Write Sanitized Crash Metadata JSON
                var metadata = new CrashMetadata
                {
                    TimestampUtc = DateTime.UtcNow,
                    Reason = reason,
                    ExceptionType = ex?.GetType().FullName ?? "Unknown",
                    ExceptionMessage = PiiDataScrubber.Scrub(ex?.Message ?? "No exception message"),
                    StackTrace = PiiDataScrubber.Scrub(ex?.StackTrace ?? Environment.StackTrace),
                    OsVersion = Environment.OSVersion.VersionString,
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    ProcessUptimeSeconds = (DateTime.UtcNow - StartTime).TotalSeconds,
                    WorkingSetMB = Math.Round(Environment.WorkingSet / (1024.0 * 1024.0), 2),
                    MiniDumpGenerated = dumpSucceeded
                };

                string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(metaPath, json);

                // 3. Update Crash Manifest
                UpdateCrashManifest(metadata);

                // 4. Prune old crash dumps (keep last 5)
                PruneOldCrashes(crashesDir, maxToKeep: 5);

                return (dumpSucceeded ? dumpPath : null, metaPath);
            }
            catch
            {
                return (null, null);
            }
        }
    }

    private static void UpdateCrashManifest(CrashMetadata latestCrash)
    {
        try
        {
            string manifestPath = Path.Combine(CrashesDirectory, "crash_manifest.json");
            CrashManifest manifest;

            if (File.Exists(manifestPath))
            {
                string existing = File.ReadAllText(manifestPath);
                manifest = JsonSerializer.Deserialize<CrashManifest>(existing) ?? new CrashManifest();
            }
            else
            {
                manifest = new CrashManifest();
            }

            manifest.TotalCrashCount++;
            manifest.LastCrashUtc = latestCrash.TimestampUtc;
            manifest.LastCrashReason = latestCrash.Reason;
            manifest.LastExceptionType = latestCrash.ExceptionType;

            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>
    /// Checks if a previous crash was recorded and returns manifest details.
    /// </summary>
    public static CrashManifest? CheckPreviousCrash()
    {
        try
        {
            string manifestPath = Path.Combine(CrashesDirectory, "crash_manifest.json");
            if (File.Exists(manifestPath))
            {
                string json = File.ReadAllText(manifestPath);
                return JsonSerializer.Deserialize<CrashManifest>(json);
            }
        }
        catch { }
        return null;
    }

    public static void ClearCrashDumps()
    {
        try
        {
            string dir = CrashesDirectory;
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "flow_crash_*.*"))
                {
                    try { File.Delete(f); } catch { }
                }

                string manifestPath = Path.Combine(dir, "crash_manifest.json");
                if (File.Exists(manifestPath))
                {
                    try { File.Delete(manifestPath); } catch { }
                }
            }
        }
        catch { }
    }

    private static void PruneOldCrashes(string crashesDir, int maxToKeep)
    {
        try
        {
            var files = Directory.GetFiles(crashesDir, "flow_crash_*.dmp");
            if (files.Length > maxToKeep)
            {
                Array.Sort(files); // oldest first by timestamp in filename
                for (int i = 0; i < files.Length - maxToKeep; i++)
                {
                    try
                    {
                        File.Delete(files[i]);
                        string meta = Path.ChangeExtension(files[i], ".json");
                        if (File.Exists(meta)) File.Delete(meta);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }
}

public sealed class CrashMetadata
{
    public DateTime TimestampUtc { get; set; }
    public string Reason { get; set; } = "";
    public string ExceptionType { get; set; } = "";
    public string ExceptionMessage { get; set; } = "";
    public string StackTrace { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public string ProcessArchitecture { get; set; } = "";
    public double ProcessUptimeSeconds { get; set; }
    public double WorkingSetMB { get; set; }
    public bool MiniDumpGenerated { get; set; }
}

public sealed class CrashManifest
{
    public int TotalCrashCount { get; set; }
    public DateTime? LastCrashUtc { get; set; }
    public string? LastCrashReason { get; set; }
    public string? LastExceptionType { get; set; }
}
