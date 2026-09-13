using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Low-level Windows keyboard hook (WH_KEYBOARD_LL) for push-to-talk and hands-free double-tap mode.
/// Hardened with startup arming gate, synthetic input rejection (LLKHF_INJECTED), extended key validation,
/// pre-existing key-down quarantine, and debounce filtering to prevent accidental auto-recording.
/// </summary>
public sealed class GlobalHotkeyHook : IDisposable
{
    public const int DefaultHotkeyVk = 0xA5; // VK_RMENU (Right Alt)
    private const int VK_ESCAPE = 0x1B;
    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_BACK = 0x08;
    private const int VK_SPACE = 0x20;
    private const int VK_MENU = 0x12; // Alt key
    private const int VK_KEY_B = 0x42;

    private const int LLKHF_EXTENDED = 0x01;
    private const int LLKHF_INJECTED = 0x10;
    private const double MinDoubleTapIntervalMs = 40.0;

    private readonly int _targetVk;
    private readonly double _doubleTapThresholdMs;

    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isArmed;
    private bool _isKeyDown;
    private bool _isKeyQuarantined;
    private bool _isCommandModeKeyDown;
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
    /// Fired when dedicated command mode recording begins (WF-036: Ctrl + TargetKey).
    /// </summary>
    public event Action? CommandModeHotkeyDown;

    /// <summary>
    /// Fired when dedicated command mode recording concludes.
    /// </summary>
    public event Action? CommandModeHotkeyUp;

    /// <summary>
    /// Fired when the active session is cancelled via Escape.
    /// </summary>
    public event Action? HotkeyCancelled;

    /// <summary>
    /// Fired when a backtrack operation is requested via shortcut (Shift+RightAlt or RightAlt+Backspace).
    /// </summary>
    public event Action? BacktrackRequested;

    public bool IsHooked => _hookId != IntPtr.Zero;
    public bool IsArmed => _isArmed;
    public bool IsHandsFreeActive => _isHandsFreeActive;

    public GlobalHotkeyHook(int targetVk = DefaultHotkeyVk, double doubleTapThresholdMs = 350.0)
    {
        _targetVk = targetVk;
        _doubleTapThresholdMs = doubleTapThresholdMs;
    }

    /// <summary>
    /// Installs the low-level Windows keyboard hook in a disarmed state.
    /// Must call <see cref="Arm"/> once host initialization and UI presentation are complete.
    /// </summary>
    public void Start(bool autoArm = false)
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr moduleHandle = GetModuleHandle(curModule?.ModuleName);

        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, moduleHandle, 0);

        if (autoArm)
        {
            Arm();
        }
    }

    /// <summary>
    /// Arms the keyboard hook to accept user hotkeys.
    /// Inspects whether the target key is already held down at arm time; if so, quarantines the key until a genuine release.
    /// </summary>
    public void Arm()
    {
        short physicalState = GetAsyncKeyState(_targetVk);
        if ((physicalState & 0x8000) != 0)
        {
            // Target key is physically depressed at arm time -> quarantine until released
            _isKeyQuarantined = true;
            _isKeyDown = true;
        }
        else
        {
            _isKeyQuarantined = false;
            _isKeyDown = false;
        }

        _isHandsFreeActive = false;
        _isCommandModeKeyDown = false;
        _lastKeyUpTimestamp = 0;
        _isArmed = true;
    }

    /// <summary>
    /// Disarms the keyboard hook, ignoring all keyboard events.
    /// </summary>
    public void Disarm()
    {
        _isArmed = false;
        _isKeyDown = false;
        _isKeyQuarantined = false;
        _isHandsFreeActive = false;
        _isCommandModeKeyDown = false;
        _lastKeyUpTimestamp = 0;
    }

    /// <summary>
    /// Uninstalls the keyboard hook and resets all transient input states.
    /// </summary>
    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        Disarm();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vkCode = kbd.vkCode;
            int flags = kbd.flags;
            int message = wParam.ToInt32();

            // Ignore synthetic injected keystrokes to protect against external automation false triggers
            if ((flags & LLKHF_INJECTED) != 0)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // If not armed, transparently pass all keystrokes through
            if (!_isArmed)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // 1. Handle Escape key for immediate cancellation
            if (vkCode == VK_ESCAPE && (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                if (_isKeyDown || _isHandsFreeActive || _isCommandModeKeyDown)
                {
                    _isKeyDown = false;
                    _isCommandModeKeyDown = false;
                    _isHandsFreeActive = false;
                    HotkeyCancelled?.Invoke();
                }
            }
            // 2. Handle Backtrack shortcuts: Shift + TargetKey or TargetKey + Backspace
            else if (((vkCode == _targetVk && (GetKeyState(VK_SHIFT) & 0x8000) != 0) ||
                      (vkCode == VK_BACK && (GetKeyState(_targetVk) & 0x8000) != 0)) &&
                     (message == WM_KEYDOWN || message == WM_SYSKEYDOWN))
            {
                BacktrackRequested?.Invoke();
                return (IntPtr)1;
            }
            // 3. Handle Command Mode shortcut: Ctrl + TargetKey (WF-036)
            else if (vkCode == _targetVk && ((GetKeyState(VK_CONTROL) & 0x8000) != 0 || _isCommandModeKeyDown))
            {
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (!_isCommandModeKeyDown)
                    {
                        _isCommandModeKeyDown = true;
                        CommandModeHotkeyDown?.Invoke();
                        return (IntPtr)1;
                    }
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    if (_isCommandModeKeyDown)
                    {
                        _isCommandModeKeyDown = false;
                        CommandModeHotkeyUp?.Invoke();
                        return (IntPtr)1;
                    }
                }
            }
            // 4. Handle Alt+Space Hold-to-Talk shortcut (Consumes shortcut to prevent SC_KEYMENU / system menu)
            else if (vkCode == VK_SPACE && (GetKeyState(VK_MENU) & 0x8000) != 0)
            {
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (!_isKeyDown)
                    {
                        _isKeyDown = true;
                        HotkeyDown?.Invoke(false); // Push-To-Talk
                    }
                    return (IntPtr)1; // Consume key to prevent system menu
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    if (_isKeyDown)
                    {
                        _isKeyDown = false;
                        HotkeyUp?.Invoke();
                    }
                    return (IntPtr)1; // Consume keyup
                }
            }
            // 5. Handle Alt+B Toggle shortcut (Consumes shortcut to prevent app character injection)
            else if (vkCode == VK_KEY_B && (GetKeyState(VK_MENU) & 0x8000) != 0)
            {
                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    if (!_isHandsFreeActive)
                    {
                        _isHandsFreeActive = true;
                        HotkeyDown?.Invoke(true); // Toggle on
                    }
                    else
                    {
                        _isHandsFreeActive = false;
                        HotkeyUp?.Invoke(); // Toggle off
                    }
                    return (IntPtr)1; // Consume key
                }
                else if (message == WM_KEYUP || message == WM_SYSKEYUP)
                {
                    return (IntPtr)1; // Consume keyup
                }
            }
            // 6. Handle configured dictation hotkey (default: Right Alt / VK_RMENU)
            else if (vkCode == _targetVk)
            {
                // For VK_RMENU, verify extended key flag on systems where layout differentiation is required
                if (_targetVk == DefaultHotkeyVk && (flags & LLKHF_EXTENDED) == 0)
                {
                    // Left Alt pressed -> not target hotkey
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                if (message == WM_KEYDOWN || message == WM_SYSKEYDOWN)
                {
                    // If key was held down before hook arming, ignore key-down events until a clean release
                    if (_isKeyQuarantined)
                    {
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

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

                        if (elapsedMs >= MinDoubleTapIntervalMs && elapsedMs <= _doubleTapThresholdMs && _lastKeyUpTimestamp > 0)
                        {
                            // Double-tap confirmed -> Enter Hands-Free Mode
                            _isHandsFreeActive = true;
                            _lastKeyUpTimestamp = 0; // Consume the double-tap
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
                    if (_isKeyQuarantined)
                    {
                        // Clean release observed -> key is now safe to use
                        _isKeyQuarantined = false;
                        _isKeyDown = false;
                        _lastKeyUpTimestamp = 0;
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

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

    #region Win32 P/Invoke & Structures

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    #endregion
}
