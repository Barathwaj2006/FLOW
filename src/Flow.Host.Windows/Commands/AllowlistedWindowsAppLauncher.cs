using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Flow.Core.Commands;

namespace Flow.Host.Windows.Commands;

/// <summary>
/// Safe application and URL launcher strictly restricted to allowlisted targets (Section 19 & 20).
/// Inviolable rule: Zero arbitrary process execution. Zero calls to Process.Start, CreateProcess, ShellExecute.
/// </summary>
public static class AllowlistedWindowsAppLauncher
{
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    /// <summary>
    /// Brings an already-running allowlisted application to the foreground.
    /// Returns true if an existing window was activated.
    /// </summary>
    public static bool TryFocusRunningApp(AllowlistedApp app)
    {
        if (app == null) return false;

        string processName = app.ExecutableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? app.ExecutableName[..^4]
            : app.ExecutableName;

        var processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            return false;
        }

        foreach (var proc in processes)
        {
            try
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(proc.MainWindowHandle, SW_RESTORE);
                    return SetForegroundWindow(proc.MainWindowHandle);
                }
            }
            catch
            {
                // Access denied or process exited
            }
        }

        return false;
    }

    /// <summary>
    /// Launches a safe validated URL via Windows modern URI launcher.
    /// </summary>
    public static async Task<bool> LaunchSafeUrlAsync(Uri validatedUrl)
    {
        if (validatedUrl == null) return false;

        // Verify scheme is strictly https or http
        if (!validatedUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
            !validatedUrl.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return await global::Windows.System.Launcher.LaunchUriAsync(validatedUrl);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Activates an approved allowlisted app via its registered modern protocol URI or window focus.
    /// </summary>
    public static async Task<bool> ActivateAllowlistedAppAsync(AllowlistedApp app)
    {
        if (app == null) return false;

        // 1. Try focusing existing window
        if (TryFocusRunningApp(app))
        {
            return true;
        }

        // 2. If modern protocol is defined, launch via Windows URI launcher
        if (!string.IsNullOrEmpty(app.ProtocolScheme))
        {
            try
            {
                var uri = new Uri(app.ProtocolScheme);
                return await global::Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}
