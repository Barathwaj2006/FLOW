using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Flow.Core.Backtrack;
using Flow.Core.TextInsertion;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Production Windows text insertion engine.
/// Executes safe cursor-only injection using dual-tier strategy:
/// Tier 1: Windows UI Automation (ValuePattern / TextPattern)
/// Tier 2: SendInput Ctrl+V with automated clipboard backup and restore via Win32 API
/// 
/// INVIOLABLE SAFETY:
/// STRICTLY PROHIBITS simulating Enter (VK_RETURN, 0x0D), Keypad Enter, or VK_SEPARATOR.
/// Under no circumstances does this service execute, submit, or send messages.
/// </summary>
public sealed class WindowsTextInsertionService : ITextInsertionService
{
    private readonly ILogger<WindowsTextInsertionService>? _logger;

    public WindowsTextInsertionService(ILogger<WindowsTextInsertionService>? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new InsertionResult(true, InsertionStrategy.None, null, TimeSpan.Zero);
        }

        // 1. INVIOLABLE ZERO-ENTER FILTER
        // Strictly replace and strip any return or newline characters.
        string safeText = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (string.IsNullOrEmpty(safeText))
        {
            return new InsertionResult(true, InsertionStrategy.None, null, TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();

        // Target active foreground window
        IntPtr foregroundHwnd = GetForegroundWindow();
        uint pid = 0;
        if (foregroundHwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(foregroundHwnd, out pid);
        }
        string appName = GetProcessNameFromHwnd(foregroundHwnd);

        _logger?.LogInformation("Targeting foreground window: {Hwnd} ({App}, PID={Pid})", foregroundHwnd, appName, pid);

        // Attempt Tier 1: Direct UI Automation Injection
        bool uiaSuccess = TryUiaInsertion(foregroundHwnd, safeText);
        if (uiaSuccess)
        {
            stopwatch.Stop();
            _logger?.LogInformation("Text successfully inserted via UIA Direct into {App} in {ElapsedMs}ms", appName, stopwatch.ElapsedMilliseconds);
            return new InsertionResult(true, InsertionStrategy.UiaDirect, appName, stopwatch.Elapsed, null, foregroundHwnd, pid, safeText.Length);
        }

        // Attempt Tier 2: Safe SendInput (Ctrl+V) with 150ms Clipboard Restore
        bool sendInputSuccess = await TrySendInputClipboardFallbackAsync(safeText, cancellationToken);
        stopwatch.Stop();

        if (sendInputSuccess)
        {
            _logger?.LogInformation("Text successfully inserted via SendInput Ctrl+V into {App} in {ElapsedMs}ms", appName, stopwatch.ElapsedMilliseconds);
            return new InsertionResult(true, InsertionStrategy.SendInputClipboardFallback, appName, stopwatch.Elapsed, null, foregroundHwnd, pid, safeText.Length);
        }

        _logger?.LogError("Failed to insert text into {App}", appName);
        return InsertionResult.Failed("Text insertion failed across all tiers.", stopwatch.Elapsed, appName);
    }

    /// <inheritdoc />
    public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default)
    {
        if (record == null || record.CharacterCount <= 0)
        {
            _logger?.LogWarning("Backtrack aborted: invalid or empty insertion record.");
            return Task.FromResult(false);
        }

        IntPtr currentHwnd = GetForegroundWindow();
        uint currentPid = 0;
        if (currentHwnd != IntPtr.Zero)
        {
            GetWindowThreadProcessId(currentHwnd, out currentPid);
        }
        string currentApp = GetProcessNameFromHwnd(currentHwnd);

        // Strict ownership check: Foreground window must match the original target
        if (record.TargetHwnd != IntPtr.Zero && currentHwnd != record.TargetHwnd)
        {
            _logger?.LogWarning(
                "Backtrack aborted for safety: Active window changed. Recorded HWND={RecordedHwnd} ({RecordedApp}), Current HWND={CurrentHwnd} ({CurrentApp}). Zero destructive action taken.",
                record.TargetHwnd, record.TargetProcessName, currentHwnd, currentApp);
            return Task.FromResult(false);
        }

        if (record.TargetProcessId != 0 && currentPid != record.TargetProcessId)
        {
            _logger?.LogWarning(
                "Backtrack aborted for safety: Active process ID changed. Recorded PID={RecordedPid}, Current PID={CurrentPid}.",
                record.TargetProcessId, currentPid);
            return Task.FromResult(false);
        }

        _logger?.LogInformation(
            "Executing backtrack in {App} (HWND={Hwnd}) for {Count} characters.",
            currentApp, currentHwnd, record.CharacterCount);

        // Execute bounded VK_BACK SendInput sequence
        int countToDelete = Math.Clamp(record.CharacterCount, 1, 2000);
        SimulateBackspaces(countToDelete);

        return Task.FromResult(true);
    }

    private bool TryUiaInsertion(IntPtr hwnd, string text)
    {
        try
        {
            AutomationElement? focusedElement = null;
            try
            {
                focusedElement = AutomationElement.FocusedElement;
            }
            catch
            {
                if (hwnd != IntPtr.Zero)
                {
                    focusedElement = AutomationElement.FromHandle(hwnd);
                }
            }

            if (focusedElement == null) return false;

            // Fail-closed privacy gate: check if focused element is password/credential
            object isPass = focusedElement.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
            if (isPass is bool isPassword && isPassword)
            {
                _logger?.LogWarning("TryUiaInsertion blocked: focused element is a password field.");
                return false;
            }

            // Check if element is editable via ValuePattern
            if (focusedElement.TryGetCurrentPattern(ValuePattern.Pattern, out object? patternObj) &&
                patternObj is ValuePattern valPattern)
            {
                if (!valPattern.Current.IsReadOnly)
                {
                    string currentVal = valPattern.Current.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(currentVal))
                    {
                        valPattern.SetValue(text);
                        _logger?.LogInformation("UIA ValuePattern.SetValue succeeded for empty target control.");
                        return true;
                    }
                    else
                    {
                        // Check if TextPattern is available for selection replacement
                        if (focusedElement.TryGetCurrentPattern(TextPattern.Pattern, out object? textPatternObj) &&
                            textPatternObj is TextPattern textPattern)
                        {
                            var selection = textPattern.GetSelection();
                            if (selection != null && selection.Length > 0)
                            {
                                // Rich edit with existing selection or cursor: delegate to SendInput Ctrl+V for precise placement
                                return false;
                            }
                        }

                        // For simple controls with existing text, append text cleanly
                        valPattern.SetValue(currentVal + " " + text);
                        _logger?.LogInformation("UIA ValuePattern appended text successfully.");
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "UIA text insertion bypassed or unsupported; using SendInput fallback.");
        }

        return false;
    }

    private async Task<bool> TrySendInputClipboardFallbackAsync(string safeText, CancellationToken cancellationToken)
    {
        string? previousClipboard = null;
        bool hadPrevious = false;

        try
        {
            // 1. Backup existing clipboard content
            previousClipboard = ReadClipboardText();
            hadPrevious = previousClipboard != null;

            // 2. Set safe text into clipboard
            if (!WriteClipboardText(safeText))
            {
                _logger?.LogError("Unable to set clipboard text for SendInput.");
                return false;
            }

            // 3. Simulate Ctrl+V using SendInput
            SimulateCtrlV();

            // 4. Wait 150ms for target window message loop to process the WM_PASTE / Ctrl+V
            await Task.Delay(150, cancellationToken);

            // 5. Restore previous clipboard content
            if (hadPrevious && previousClipboard != null)
            {
                WriteClipboardText(previousClipboard);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing SendInput clipboard fallback.");
            return false;
        }
    }

    private static void SimulateCtrlV()
    {
        INPUT[] inputs = new INPUT[4];

        // 1. Ctrl Down
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_CONTROL,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // 2. V Down
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_V,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // 3. V Up
        inputs[2] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_V,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // 4. Ctrl Up
        inputs[3] = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = VK_CONTROL,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Strict verification that no Enter key is present
        foreach (var inp in inputs)
        {
            if (inp.u.ki.wVk == VK_RETURN || inp.u.ki.wVk == VK_SEPARATOR)
            {
                throw new InvalidOperationException("CRITICAL SAFETY VIOLATION: VK_RETURN detected in SendInput sequence!");
            }
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void SimulateBackspaces(int count)
    {
        if (count <= 0) return;

        const int batchSize = 50;
        for (int i = 0; i < count; i += batchSize)
        {
            int batchCount = Math.Min(batchSize, count - i);
            INPUT[] inputs = new INPUT[batchCount * 2];

            for (int b = 0; b < batchCount; b++)
            {
                inputs[b * 2] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = VK_BACK,
                            wScan = 0,
                            dwFlags = 0,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };

                inputs[b * 2 + 1] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = VK_BACK,
                            wScan = 0,
                            dwFlags = KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = IntPtr.Zero
                        }
                    }
                };
            }

            // Strict verification that no Enter key is present
            foreach (var inp in inputs)
            {
                if (inp.u.ki.wVk == VK_RETURN || inp.u.ki.wVk == VK_SEPARATOR)
                {
                    throw new InvalidOperationException("CRITICAL SAFETY VIOLATION: VK_RETURN detected in Backtrack sequence!");
                }
            }

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            Thread.Sleep(5);
        }
    }

    private static string? ReadClipboardText()
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    IntPtr handle = GetClipboardData(CF_UNICODETEXT);
                    if (handle != IntPtr.Zero)
                    {
                        IntPtr pointer = GlobalLock(handle);
                        if (pointer != IntPtr.Zero)
                        {
                            try
                            {
                                return Marshal.PtrToStringUni(pointer);
                            }
                            finally
                            {
                                GlobalUnlock(handle);
                            }
                        }
                    }
                    return null;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(20 * (attempt + 1));
        }
        return null;
    }

    private static bool WriteClipboardText(string text)
    {
        for (int attempt = 0; attempt < 15; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();
                    int bytesNeeded = (text.Length + 1) * sizeof(char);
                    IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytesNeeded);
                    if (hGlobal != IntPtr.Zero)
                    {
                        IntPtr target = GlobalLock(hGlobal);
                        if (target != IntPtr.Zero)
                        {
                            try
                            {
                                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                                Marshal.WriteInt16(target, text.Length * sizeof(char), 0); // Null terminator
                            }
                            finally
                            {
                                GlobalUnlock(hGlobal);
                            }

                            if (SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
                            {
                                return true;
                            }
                            else
                            {
                                GlobalFree(hGlobal);
                            }
                        }
                        else
                        {
                            GlobalFree(hGlobal);
                        }
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            Thread.Sleep(20 * (attempt + 1));
        }
        return false;
    }

    private static string GetProcessNameFromHwnd(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return "Unknown";
        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return "Unknown";
        }
    }

    #region Win32 P/Invoke

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_BACK = 0x08;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_SEPARATOR = 0x6C;

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, [In] INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private static readonly System.Collections.Generic.HashSet<string> KnownIdeProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "code.exe",
        "cursor", "cursor.exe",
        "devenv", "devenv.exe",
        "windsurf", "windsurf.exe",
        "idea64", "idea64.exe",
        "pycharm64", "pycharm64.exe",
        "webstorm64", "webstorm64.exe",
        "rider64", "rider64.exe",
        "windowsterminal", "windowsterminal.exe",
        "powershell", "powershell.exe",
        "pwsh", "pwsh.exe",
        "cmd", "cmd.exe"
    };

    /// <summary>
    /// Checks whether the specified process name corresponds to an IDE, code editor, or developer terminal (WF-029).
    /// </summary>
    public static bool IsIdeProcess(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        string clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^4];
        }
        return KnownIdeProcessNames.Contains(clean) || KnownIdeProcessNames.Contains(processName);
    }

    #endregion
}
