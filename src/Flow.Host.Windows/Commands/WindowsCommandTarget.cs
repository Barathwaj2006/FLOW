using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Flow.Core.Context;

namespace Flow.Host.Windows.Commands;

/// <summary>
/// Helper for querying and verifying target window liveness and password safety on Windows.
/// </summary>
public static class WindowsCommandTarget
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static (IntPtr Hwnd, uint Pid) GetCurrentForeground()
    {
        IntPtr hwnd = GetForegroundWindow();
        uint pid = 0;
        if (hwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(hwnd, out pid);
        }
        return (hwnd, pid);
    }

    public static bool IsTargetStillActive(IntPtr initialHwnd, uint initialPid)
    {
        if (initialHwnd == IntPtr.Zero) return false;

        var (currentHwnd, currentPid) = GetCurrentForeground();
        return currentHwnd == initialHwnd && currentPid == initialPid;
    }

    public static bool IsPasswordTarget(IUIContextService? contextService)
    {
        if (contextService == null) return false;
        return contextService.IsFocusInPasswordField();
    }
}
