using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Native Windows power management notification manager.
/// Subscribes to Windows power broadcasts (sleep, suspend, resume, battery events)
/// via RegisterSuspendResumeNotification and forwards lifecycle triggers to FLOW.
/// </summary>
public sealed class WindowsPowerStateManager : IDisposable
{
    public const uint WM_POWERBROADCAST = 0x0218;
    public const int PBT_APMSUSPEND = 0x0004;
    public const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    public const int PBT_APMRESUMESUSPEND = 0x0007;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;

    private const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private readonly ILogger<WindowsPowerStateManager>? _logger;
    private IntPtr _hNotification = IntPtr.Zero;
    private volatile bool _isSuspended;
    private bool _isDisposed;

    public bool IsSuspended => _isSuspended;

    public event Action? Suspending;
    public event Action? Resuming;
    public event Action<int>? PowerBroadcastReceived;

    public WindowsPowerStateManager(IntPtr hwnd = default, ILogger<WindowsPowerStateManager>? logger = null)
    {
        _logger = logger;

        if (hwnd != IntPtr.Zero)
        {
            RegisterNotification(hwnd);
        }
    }

    private void RegisterNotification(IntPtr hwnd)
    {
        try
        {
            _hNotification = RegisterSuspendResumeNotification(hwnd, DEVICE_NOTIFY_WINDOW_HANDLE);
            if (_hNotification == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                _logger?.LogWarning("RegisterSuspendResumeNotification failed with Win32 error code: {ErrorCode}", err);
            }
            else
            {
                _logger?.LogInformation("Successfully registered Windows Suspend/Resume power notification handle: 0x{Handle:X}", _hNotification);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to register Windows power notification callback.");
        }
    }

    /// <summary>
    /// Processes a WM_POWERBROADCAST Windows message with wParam power event code.
    /// </summary>
    public void ProcessPowerBroadcast(int powerEvent)
    {
        _logger?.LogInformation("Received Windows Power Broadcast event: 0x{Event:X4}", powerEvent);
        PowerBroadcastReceived?.Invoke(powerEvent);

        switch (powerEvent)
        {
            case PBT_APMSUSPEND:
                _isSuspended = true;
                _logger?.LogWarning("Windows is entering sleep/suspend mode (PBT_APMSUSPEND).");
                Suspending?.Invoke();
                break;

            case PBT_APMRESUMEAUTOMATIC:
            case PBT_APMRESUMESUSPEND:
                _isSuspended = false;
                _logger?.LogInformation("Windows has resumed from sleep (PBT_APMRESUME).");
                Resuming?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Programmatically simulates a system suspend transition for automated testing.
    /// </summary>
    public void SimulateSuspend()
    {
        ProcessPowerBroadcast(PBT_APMSUSPEND);
    }

    /// <summary>
    /// Programmatically simulates a system resume transition for automated testing.
    /// </summary>
    public void SimulateResume()
    {
        ProcessPowerBroadcast(PBT_APMRESUMEAUTOMATIC);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            if (_hNotification != IntPtr.Zero)
            {
                try
                {
                    UnregisterSuspendResumeNotification(_hNotification);
                    _hNotification = IntPtr.Zero;
                }
                catch { }
            }
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterSuspendResumeNotification(IntPtr hRecipient, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterSuspendResumeNotification(IntPtr handle);
}
