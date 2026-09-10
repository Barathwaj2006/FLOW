using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Low-level Windows keyboard hook (WH_KEYBOARD_LL) for push-to-talk and global hotkey handling.
/// Accurately differentiates KeyDown from KeyUp to support natural hold-to-speak interactions.
/// </summary>
public sealed class GlobalHotkeyHook : IDisposable
{
    public const int DefaultHotkeyVk = 0xA5; // VK_RMENU (Right Alt)
    private const int VK_ESCAPE = 0x1B;

    private readonly int _targetVk;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isKeyDown;
    private bool _isDisposed;

    public event Action? HotkeyDown;
    public event Action? HotkeyUp;
    public event Action? HotkeyCancelled;

    public bool IsHooked => _hookId != IntPtr.Zero;

    public GlobalHotkeyHook(int targetVk = DefaultHotkeyVk)
    {
        _targetVk = targetVk;
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
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int message = wParam.ToInt32();

            // Cancel session on ESC key
            if (vkCode == VK_ESCAPE && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                if (_isKeyDown)
                {
                    _isKeyDown = false;
                    HotkeyCancelled?.Invoke();
                }
            }
            else if (vkCode == _targetVk)
            {
                if ((message == WM_KEYDOWN || message == WM_SYSKEYDOWN) && !_isKeyDown)
                {
                    _isKeyDown = true;
                    HotkeyDown?.Invoke();
                }
                else if ((message == WM_KEYUP || message == WM_SYSKEYUP) && _isKeyDown)
                {
                    _isKeyDown = false;
                    HotkeyUp?.Invoke();
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
