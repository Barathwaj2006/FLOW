using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Low-level Windows keyboard hook (WH_KEYBOARD_LL) for push-to-talk and hands-free double-tap mode.
/// Accurately differentiates single hold-to-speak from double-tap hands-free toggling.
/// </summary>
public sealed class GlobalHotkeyHook : IDisposable
{
    public const int DefaultHotkeyVk = 0xA5; // VK_RMENU (Right Alt)
    private const int VK_ESCAPE = 0x1B;

    private readonly int _targetVk;
    private readonly double _doubleTapThresholdMs;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isKeyDown;
    private bool _isHandsFreeActive;
    private long _lastKeyUpTimestamp;
    private bool _isDisposed;

    /// <summary>
    /// Fired when recording should begin (either via PTT hold or Hands-Free double-tap).
    /// Parameter indicates whether Hands-Free mode is active.
    /// </summary>
    public event Action<bool>? HotkeyDown;

    /// <summary>
    /// Fired when recording should conclude.
    /// </summary>
    public event Action? HotkeyUp;

    /// <summary>
    /// Fired when the active session is cancelled via Escape.
    /// </summary>
    public event Action? HotkeyCancelled;

    public bool IsHooked => _hookId != IntPtr.Zero;
    public bool IsHandsFreeActive => _isHandsFreeActive;

    public GlobalHotkeyHook(int targetVk = DefaultHotkeyVk, double doubleTapThresholdMs = 350.0)
    {
        _targetVk = targetVk;
        _doubleTapThresholdMs = doubleTapThresholdMs;
    }

    /// <summary>
    /// Installs the low-level Windows keyboard hook.
    /// </summary>
    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr moduleHandle = GetModuleHandle(curModule?.ModuleName);

        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);
    }

    /// <summary>
    /// Uninstalls the keyboard hook.
    /// </summary>
    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        _isKeyDown = false;
        _isHandsFreeActive = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int message = wParam.ToInt32();

            // 1. Handle Escape key for immediate cancellation
            if (vkCode == VK_ESCAPE && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                if (_isKeyDown || _isHandsFreeActive)
                {
                    _isKeyDown = false;
                    _isHandsFreeActive = false;
                    HotkeyCancelled?.Invoke();
                }
            }
            // 2. Handle configured hotkey (default: Right Alt)
            else if (vkCode == _targetVk)
            {
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (!_isKeyDown)
                    {
                        _isKeyDown = true;

                        // If already in Hands-Free mode, any subsequent press immediately stops recording
                        if (_isHandsFreeActive)
                        {
                            _isHandsFreeActive = false;
                            HotkeyUp?.Invoke();
                            return CallNextHookEx(_hookId, nCode, wParam, lParam);
                        }

                        // Evaluate time since last key up for double-tap detection
                        long now = Stopwatch.GetTimestamp();
                        double elapsedMs = (double)(now - _lastKeyUpTimestamp) * 1000.0 / Stopwatch.Frequency;

                        if (elapsedMs <= _doubleTapThresholdMs && _lastKeyUpTimestamp > 0)
                        {
                            // Double-tap confirmed -> Enter Hands-Free Mode
                            _isHandsFreeActive = true;
                            HotkeyDown?.Invoke(true);
                        }
                        else
                        {
                            // Standard Push-To-Talk KeyDown
                            HotkeyDown?.Invoke(false);
                        }
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    if (_isKeyDown)
                    {
                        _isKeyDown = false;
                        _lastKeyUpTimestamp = Stopwatch.GetTimestamp();

                        // If Hands-Free mode is active, releasing the key does NOT stop recording
                        if (!_isHandsFreeActive)
                        {
                            HotkeyUp?.Invoke();
                        }
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _isDisposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #region Win32 P/Invoke

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    #endregion
}
