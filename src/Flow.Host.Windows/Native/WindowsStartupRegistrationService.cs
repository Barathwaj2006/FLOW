using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Manages automatic startup of the FLOW application on Windows user login
/// via the HKCU\Software\Microsoft\Windows\CurrentVersion\Run registry key.
/// </summary>
public static class WindowsStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "FLOW";

    /// <summary>
    /// Checks whether FLOW is currently registered to launch on user login.
    /// </summary>
    public static bool IsStartupEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables automatic startup of FLOW on user login.
    /// When enabled, registers with --tray argument so it starts quietly in notification area.
    /// </summary>
    public static bool SetStartupEnabled(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return false;

            if (enable)
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                    ?? Environment.ProcessPath 
                    ?? string.Empty;

                if (string.IsNullOrEmpty(exePath)) return false;

                // Launch minimized to system tray on boot
                string command = $"\"{exePath}\" --tray";
                key.SetValue(AppName, command);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
