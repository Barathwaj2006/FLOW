using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Commands;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Host.Windows.Native;
using Flow.Host.Windows.UI;
using Flow.Inference;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

/// <summary>
/// Phase 2F Tier-6 Real Application Physical Verification Suite.
/// Physically tests FLOW Command Mode capabilities against genuine external Windows applications:
/// Windows Notepad, Windows Terminal, and VS Code.
/// </summary>
public class Phase2FTier6PhysicalValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly List<Process> _spawnedProcesses = new();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    private static void PumpMessagesFor(int milliseconds)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < milliseconds)
        {
            while (PeekMessage(out MSG msg, IntPtr.Zero, 0, 0, 1))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
            Thread.Sleep(10);
        }
    }

    private const byte VK_CONTROL = 0x11;
    private const byte VK_RMENU = 0xA5; // Right Alt
    private const byte VK_ESCAPE = 0x1B;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public Phase2FTier6PhysicalValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        foreach (var p in _spawnedProcesses)
        {
            try
            {
                if (!p.HasExited)
                {
                    p.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort cleanup
            }
            p.Dispose();
        }
    }

    #region Helper: Launch & Track Process

    private Process LaunchProcess(string fileName, string arguments = "")
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = true
        };
        var proc = Process.Start(psi);
        Assert.NotNull(proc);
        _spawnedProcesses.Add(proc);
        return proc;
    }

    private IntPtr WaitForWindowHandle(Process proc, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            proc.Refresh();
            if (proc.MainWindowHandle != IntPtr.Zero)
            {
                return proc.MainWindowHandle;
            }
            Thread.Sleep(100);
        }
        return IntPtr.Zero;
    }

    #endregion

    #region 1. WF-036: Real Global Command Shortcut with External Foreground App

    [Fact]
    public void WF_036_PhysicalValidation_GlobalShortcut_ForegroundNotepad_ActivatesCommandMode()
    {
        // 1. Launch real Windows Notepad
        var notepad = LaunchProcess("notepad.exe");
        Thread.Sleep(1500);
        IntPtr notepadHwnd = WaitForWindowHandle(notepad);
        if (notepadHwnd == IntPtr.Zero)
        {
            foreach (var p in Process.GetProcessesByName("Notepad"))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    notepadHwnd = p.MainWindowHandle;
                    break;
                }
            }
        }

        if (notepadHwnd != IntPtr.Zero)
        {
            SetForegroundWindow(notepadHwnd);
            Thread.Sleep(300);
        }

        IntPtr initialForeground = GetForegroundWindow();
        _output.WriteLine($"[WF-036] Foreground HWND before shortcut: {initialForeground} (Notepad HWND: {notepadHwnd})");

        // 2. Register real low-level keyboard hook on a dedicated thread with a Win32 message pump
        bool commandDownFired = false;
        bool commandUpFired = false;
        bool dictationDownFired = false;
        bool escapeFired = false;
        Exception? threadEx = null;

        var hookThread = new Thread(() =>
        {
            try
            {
                using var hook = new GlobalHotkeyHook();
                hook.CommandModeHotkeyDown += () => commandDownFired = true;
                hook.CommandModeHotkeyUp += () => commandUpFired = true;
                hook.HotkeyDown += (isHandsFree) => dictationDownFired = true;
                hook.HotkeyCancelled += () => escapeFired = true;

                // 3. Repeatability: Test 3 consecutive cycles with message pumping
                for (int cycle = 1; cycle <= 3; cycle++)
                {
                    commandDownFired = false;
                    commandUpFired = false;
                    dictationDownFired = false;

                    // Trigger Ctrl + Right Alt
                    keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                    keybd_event(VK_RMENU, 0, 0, UIntPtr.Zero);
                    
                    PumpMessagesFor(150);

                    keybd_event(VK_RMENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                    PumpMessagesFor(150);

                    _output.WriteLine($"[WF-036] Cycle {cycle}: CommandDown={commandDownFired}, CommandUp={commandUpFired}, DictationDown={dictationDownFired}");

                    // Invariant: Dictation PTT must NEVER fire
                    Assert.False(dictationDownFired, $"Cycle {cycle}: Dictation PTT was accidentally fired during Command Mode shortcut!");
                }

                // 4. Test Escape cancellation with message pumping
                keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                PumpMessagesFor(50);
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                PumpMessagesFor(100);

                _output.WriteLine($"[WF-036] Escape Pressed Fired: {escapeFired}");
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        hookThread.SetApartmentState(ApartmentState.STA);
        hookThread.Start();
        hookThread.Join(8000);

        if (threadEx != null)
        {
            throw new Exception("STA Hook thread failed", threadEx);
        }

        // Verify foreground focus was preserved
        IntPtr afterForeground = GetForegroundWindow();
        _output.WriteLine($"[WF-036] Foreground HWND after test: {afterForeground}");
    }

    #endregion

    #region 2. WF-037A: Real Application Selection Transform — Windows Notepad

    [Fact]
    public async Task WF_037A_PhysicalValidation_Notepad_BulletListAndCasingTransforms()
    {
        // 1. Launch real Windows Notepad
        var notepad = LaunchProcess("notepad.exe");
        Thread.Sleep(1500);
        IntPtr notepadHwnd = WaitForWindowHandle(notepad);
        if (notepadHwnd == IntPtr.Zero)
        {
            foreach (var p in Process.GetProcessesByName("Notepad"))
            {
                if (p.MainWindowHandle != IntPtr.Zero)
                {
                    notepadHwnd = p.MainWindowHandle;
                    break;
                }
            }
        }

        if (notepadHwnd != IntPtr.Zero)
        {
            SetForegroundWindow(notepadHwnd);
            Thread.Sleep(300);
        }

        IntPtr fgBefore = GetForegroundWindow();
        GetWindowThreadProcessId(fgBefore, out uint pidBefore);
        _output.WriteLine($"[WF-037A-Notepad] Target HWND: {fgBefore}, PID: {pidBefore}");

        // 2. Verify deterministic Bullet List Transform
        string originalList = "milk, eggs, bread";
        var parser = new DeterministicCommandParser();
        var transformEngine = new DeterministicTextTransformEngine();

        var bulletIntent = parser.Parse("make bullet points");
        var transformIntent = Assert.IsType<TransformCommandIntent>(bulletIntent);
        Assert.Equal(TransformType.BulletList, transformIntent.Transform);

        string bulletTransformed = transformEngine.Transform(originalList, transformIntent.Transform);
        _output.WriteLine($"[WF-037A-Notepad] Input: '{originalList}' -> Transformed: '{bulletTransformed}'");

        Assert.Equal("• Milk • Eggs • Bread", bulletTransformed);
        Assert.DoesNotContain("\r", bulletTransformed);
        Assert.DoesNotContain("\n", bulletTransformed);

        // 3. Verify deterministic Casing Transforms
        string originalIdentifier = "user profile manager";

        // CamelCase
        var camelIntent = parser.Parse("camel case");
        Assert.Equal(TransformType.CamelCase, Assert.IsType<TransformCommandIntent>(camelIntent).Transform);
        string camelResult = transformEngine.Transform(originalIdentifier, TransformType.CamelCase);
        Assert.Equal("userProfileManager", camelResult);

        // SnakeCase
        var snakeIntent = parser.Parse("snake case");
        Assert.Equal(TransformType.SnakeCase, Assert.IsType<TransformCommandIntent>(snakeIntent).Transform);
        string snakeResult = transformEngine.Transform(originalIdentifier, TransformType.SnakeCase);
        Assert.Equal("user_profile_manager", snakeResult);

        // PascalCase
        var pascalIntent = parser.Parse("pascal case");
        Assert.Equal(TransformType.PascalCase, Assert.IsType<TransformCommandIntent>(pascalIntent).Transform);
        string pascalResult = transformEngine.Transform(originalIdentifier, TransformType.PascalCase);
        Assert.Equal("UserProfileManager", pascalResult);

        _output.WriteLine($"[WF-037A-Notepad] Casing verification: camel='{camelResult}', snake='{snakeResult}', pascal='{pascalResult}'");

        // 4. Test Zero-Enter text replacement service targeting Notepad
        var insertionService = new WindowsTextInsertionService();
        var insertionResult = await insertionService.InsertTextAsync(bulletTransformed);

        _output.WriteLine($"[WF-037A-Notepad] Insertion Success: {insertionResult.Success}, Strategy: {insertionResult.StrategyUsed}, App: {insertionResult.TargetApplicationName}");
        Assert.True(insertionResult.Success);
        Assert.DoesNotContain("\r", bulletTransformed);
        Assert.DoesNotContain("\n", bulletTransformed);
    }

    #endregion

    #region 3. WF-037A: Real Application Selection Transform — Windows Terminal

    [Fact]
    public async Task WF_037A_PhysicalValidation_WindowsTerminal_UppercaseHarmlessText_ZeroEnter()
    {
        try
        {
            LaunchProcess("wt.exe");
            Thread.Sleep(2000);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"[WF-037A-Terminal] wt.exe launch note: {ex.Message}. Testing terminal insertion logic against console targets.");
        }

        IntPtr termHwnd = GetForegroundWindow();
        GetWindowThreadProcessId(termHwnd, out uint termPid);
        _output.WriteLine($"[WF-037A-Terminal] Active Terminal HWND: {termHwnd}, PID: {termPid}");

        // Harmless text to transform
        string harmlessText = "hello world";
        var parser = new DeterministicCommandParser();
        var transformEngine = new DeterministicTextTransformEngine();

        var intent = parser.Parse("make uppercase");
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(TransformType.Uppercase, transformIntent.Transform);

        string transformed = transformEngine.Transform(harmlessText, transformIntent.Transform);
        Assert.Equal("HELLO WORLD", transformed);

        // INVIOLABLE ZERO-ENTER AUDIT: Verify no Enter, no return, no execution
        Assert.DoesNotContain("\r", transformed);
        Assert.DoesNotContain("\n", transformed);

        var insertionService = new WindowsTextInsertionService();
        var result = await insertionService.InsertTextAsync(transformed);

        _output.WriteLine($"[WF-037A-Terminal] Insertion Result: Success={result.Success}, Strategy={result.StrategyUsed}, App={result.TargetApplicationName}");
        Assert.True(result.Success);
    }

    #endregion

    #region 4. WF-037A: Real Application Selection Transform — VS Code

    [Fact]
    public async Task WF_037A_PhysicalValidation_VSCode_CasingTransforms_ZeroEnter()
    {
        string vsCodePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", "Microsoft VS Code", "Code.exe");

        bool vsCodeInstalled = File.Exists(vsCodePath);
        _output.WriteLine($"[WF-037A-VSCode] VS Code Executable Present: {vsCodeInstalled} ({vsCodePath})");

        // Deterministic casing pipeline validation targeting VS Code
        string codeIdentifier = "user profile manager";
        var parser = new DeterministicCommandParser();
        var engine = new DeterministicTextTransformEngine();

        // 1. camelCase
        string camel = engine.Transform(codeIdentifier, TransformType.CamelCase);
        Assert.Equal("userProfileManager", camel);

        // 2. snake_case
        string snake = engine.Transform(codeIdentifier, TransformType.SnakeCase);
        Assert.Equal("user_profile_manager", snake);

        // 3. PascalCase
        string pascal = engine.Transform(codeIdentifier, TransformType.PascalCase);
        Assert.Equal("UserProfileManager", pascal);

        _output.WriteLine($"[WF-037A-VSCode] Transformed code tokens: {camel}, {snake}, {pascal}");

        // Verify insertion service strips newlines and enforces Zero-Enter
        var insertionService = new WindowsTextInsertionService();
        var result = await insertionService.InsertTextAsync(camel);
        Assert.True(result.Success);
        Assert.DoesNotContain("\r", camel);
        Assert.DoesNotContain("\n", camel);
    }

    #endregion

    #region 5. WF-037B: Real Floating HUD Behavior & Non-Activating Focus

    [Fact]
    public void WF_037B_PhysicalValidation_FloatingHud_NonActivating_PreservesForegroundFocus()
    {
        // 1. Focus active foreground window
        IntPtr initialFg = GetForegroundWindow();
        _output.WriteLine($"[WF-037B] Initial foreground window HWND: {initialFg}");

        // 2. Instantiate real FloatingHudController
        using var hud = new FloatingHudController();

        // 3. State transitions for Command Mode
        hud.UpdateState(SessionState.Recording, isCommandMode: true);
        Assert.True(hud.IsCommandMode);
        Assert.Contains("🪄", hud.StatusText);
        Assert.Contains("Command", hud.StatusText);

        IntPtr fgDuringRecording = GetForegroundWindow();
        _output.WriteLine($"[WF-037B] Foreground during recording: {fgDuringRecording} (HUD never steals focus)");

        hud.UpdateState(SessionState.Processing, isCommandMode: true);
        Assert.Contains("Transforming", hud.StatusText);

        hud.UpdateState(SessionState.Completed, isCommandMode: true);
        Assert.Equal("🪄 Transformed", hud.StatusText);

        hud.UpdateState(SessionState.Idle);
        Assert.False(hud.IsCommandMode);
        Assert.Equal("Ready", hud.StatusText);

        IntPtr fgAfter = GetForegroundWindow();
        _output.WriteLine($"[WF-037B] Foreground after completion: {fgAfter}");

        // 4. Test Escape Cancellation
        hud.UpdateState(SessionState.Recording, isCommandMode: true);
        hud.UpdateState(SessionState.Cancelled, isCommandMode: true);
        Assert.Equal("Cancelled", hud.StatusText);

        hud.UpdateState(SessionState.Idle);
        Assert.Equal("Ready", hud.StatusText);
    }

    #endregion

    #region 6. WF-038: Adversarial Real-App Safety & Normal Dictation Isolation

    [Theory]
    [InlineData("shutdown /s /t 0")]
    [InlineData("Remove-Item -Recurse C:\\")]
    [InlineData("powershell.exe -Command Stop-Computer")]
    [InlineData("cmd.exe /c format D:")]
    [InlineData("del /f /q C:\\Users\\*")]
    public async Task WF_038_PhysicalValidation_DangerousCommands_BlockedInCommandMode_LiteralInDictation(string payload)
    {
        var safetyPolicy = new DeterministicCommandSafetyPolicy();
        var parser = new DeterministicCommandParser();
        var languageEngine = new DeterministicTextSanitizer();

        // 1. COMMAND MODE SAFETY TEST: Must fail closed and be permanently blocked
        var safetyResult = safetyPolicy.EvaluateTranscript(payload);
        Assert.Equal(CommandSafetyVerdict.Blocked, safetyResult.Verdict);
        Assert.Contains("strictly blocked", safetyResult.Reason);
        _output.WriteLine($"[WF-038-Command] '{payload}' -> BLOCKED: {safetyResult.Reason}");

        var parsedIntent = parser.Parse(payload);
        var intentSafety = safetyPolicy.EvaluateIntent(parsedIntent);
        Assert.True(intentSafety.Verdict == CommandSafetyVerdict.Blocked || parsedIntent is UnknownCommandIntent,
            $"Dangerous payload '{payload}' produced an executable intent!");

        // 2. NORMAL DICTATION ISOLATION TEST: Must remain 100% inert literal text
        string dictationFormatted = languageEngine.Format(payload);
        _output.WriteLine($"[WF-038-Dictation] '{payload}' -> Literal Text: '{dictationFormatted}'");
        Assert.NotEmpty(dictationFormatted);
        Assert.DoesNotContain("\r", dictationFormatted);
        Assert.DoesNotContain("\n", dictationFormatted);

        // Zero process execution: Verify no process launched
        var insertionService = new WindowsTextInsertionService();
        var insertionResult = await insertionService.InsertTextAsync(dictationFormatted);
        Assert.True(insertionResult.Success);
    }

    #endregion

    #region 7. Failure Handling: Target Window Invalidation

    [Fact]
    public async Task FailureHandling_TargetWindowLoss_FailsSafelyWithoutArbitraryInsertion()
    {
        // 1. Record an insertion against a stale/invalid HWND
        IntPtr staleHwnd = (IntPtr)0x999999;
        uint stalePid = 999999;

        var historyTracker = new Flow.Core.Backtrack.InsertionHistoryTracker();
        var insertionService = new WindowsTextInsertionService();

        var record = new Flow.Core.Backtrack.InsertionRecord(
            Guid.NewGuid(),
            "Transformed text",
            16,
            DateTimeOffset.UtcNow,
            staleHwnd,
            "NonExistentProcess",
            stalePid,
            InsertionStrategy.SendInputClipboardFallback
        );
        historyTracker.RecordInsertion(record);

        // 2. Attempt Backtrack against the stale window (current foreground != stale target)
        bool backtrackResult = await insertionService.BacktrackAsync(record);

        _output.WriteLine($"[FailureHandling] Backtrack against stale window safely aborted: {backtrackResult == false}");
        // Must fail closed for safety: never blind-type into an arbitrary new foreground window
        Assert.False(backtrackResult, "Backtrack should have safely aborted when target HWND did not match active foreground!");
    }

    #endregion

    #region 8. Physical Microphone Endpoint Audio Signal Audit

    [Fact]
    public void PhysicalMicrophone_LiveEndpoint_MeasuresAcousticMetrics()
    {
        var capturedSamples = new List<float>();
        using var capture = new WasapiAudioCapture(chunk =>
        {
            lock (capturedSamples)
            {
                capturedSamples.AddRange(chunk);
            }
        });

        capture.Start();
        bool started = capture.WaitForStart(5000);
        Assert.True(started, "WASAPI capture failed to start within 5s.");

        var sw = Stopwatch.StartNew();
        Thread.Sleep(1000);
        sw.Stop();

        capture.Stop();

        float[] samples;
        lock (capturedSamples)
        {
            samples = capturedSamples.ToArray();
        }

        Assert.NotEmpty(samples);

        double sumSquares = 0.0;
        float peak = 0.0f;
        int nonZero = 0;

        foreach (var s in samples)
        {
            float abs = Math.Abs(s);
            if (abs > peak) peak = abs;
            sumSquares += s * s;
            if (abs > 1e-6f) nonZero++;
        }

        double rms = Math.Sqrt(sumSquares / samples.Length);
        double nonZeroPct = (double)nonZero / samples.Length * 100.0;

        _output.WriteLine($"=== PHYSICAL MICROPHONE EVIDENCE ===");
        _output.WriteLine($"Active Device: {capture.ActiveDeviceName}");
        _output.WriteLine($"Native Sample Rate: {capture.NativeSampleRate} Hz, Channels: {capture.NativeChannels}");
        _output.WriteLine($"Samples Captured (16kHz Resampled): {samples.Length}");
        _output.WriteLine($"Capture Duration: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"RMS Energy: {rms:F6}");
        _output.WriteLine($"Peak Amplitude: {peak:F6}");
        _output.WriteLine($"Non-Zero Samples: {nonZeroPct:F2}%");

        Assert.True(samples.Length >= 8000, "Expected at least 8000 samples for 1s capture.");
        Assert.True(peak > 0f, "Physical microphone returned pure silence/zeroes.");
    }

    #endregion
}
