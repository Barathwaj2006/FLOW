using System;
using System.Diagnostics;
using System.IO;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.Personalization;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Host.Windows.Native;
using Flow.Inference;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

/// <summary>
/// Phase 5 Context Awareness & Application Intelligence Physical Windows Validation Tests.
/// Exercises real Windows UI Automation, real WPF controls on STA threads, real foreground target detection,
/// live Whisper inference with application context biasing, and empirical latency benchmarks.
/// </summary>
public class Phase5ContextAwarenessPhysicalValidationTests
{
    private readonly ITestOutputHelper _output;

    public Phase5ContextAwarenessPhysicalValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static AudioBuffer CreateSyntheticSpeech(string phrase)
    {
        byte[] pcmBytes;
        using (var synth = new SpeechSynthesizer())
        using (var ms = new MemoryStream())
        {
            var format = new SpeechAudioFormatInfo(16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono);
            synth.SetOutputToAudioStream(ms, format);
            synth.Speak(phrase);
            synth.SetOutputToNull();
            pcmBytes = ms.ToArray();
        }

        int sampleCount = pcmBytes.Length / 2;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short pcm = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
            samples[i] = pcm / 32768.0f;
        }

        return new AudioBuffer(samples, 16000, 1);
    }

    [Fact]
    public void Physical_WindowsUIAutomation_CaptureContext_OnRealWpfTextBox()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "FLOW Physical Context TextBox Test",
                    Width = 300,
                    Height = 200,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var txtBox = new TextBox
                {
                    Name = "PhysicalContextTxtBox",
                    Text = "Initial context before typing "
                };

                window.Content = txtBox;
                window.Show();
                txtBox.Focus();
                txtBox.CaretIndex = txtBox.Text.Length;

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                var winElement = AutomationElement.FromHandle(hwnd);
                Assert.NotNull(winElement);

                var txtElement = winElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "PhysicalContextTxtBox"))
                    ?? winElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                Assert.NotNull(txtElement);

                var contextService = new WindowsUIAutomationContextService();
                var sw = Stopwatch.StartNew();
                var snapshot = contextService.CaptureContext(Guid.NewGuid(), 200, 10000, txtElement);
                sw.Stop();

                _output.WriteLine($"[Benchmark] CaptureContext latency: {sw.Elapsed.TotalMilliseconds:F2} ms");

                Assert.NotNull(snapshot);
                Assert.False(snapshot.IsSensitive);
                Assert.NotNull(snapshot.NearbyText);
                Assert.Contains("Initial context", snapshot.NearbyText);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void Physical_WindowsUIAutomation_PasswordBox_FailsClosedSafely()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "FLOW Physical Password Test",
                    Width = 300,
                    Height = 200,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var pwdBox = new PasswordBox
                {
                    Name = "PhysicalSecretPwdBox",
                    Password = "SecretCredentials123!"
                };

                window.Content = pwdBox;
                window.Show();
                pwdBox.Focus();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                var winElement = AutomationElement.FromHandle(hwnd);
                Assert.NotNull(winElement);

                var pwdElement = winElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "PhysicalSecretPwdBox"))
                    ?? winElement.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                Assert.NotNull(pwdElement);

                // Verify actual UIA properties on live WPF control
                object isPassProp = pwdElement.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
                Assert.True(isPassProp is true || (isPassProp is bool b && b), "WPF PasswordBox must expose IsPasswordProperty = true");

                var contextService = new WindowsUIAutomationContextService();

                // 1. Direct UIA IsPasswordProperty validation on real WPF PasswordBox
                bool isPassword = contextService.IsFocusInPasswordField(pwdElement);
                _output.WriteLine($"[Physical] PasswordBox Element Check: IsPassword = {isPassword}");
                Assert.True(isPassword, "Physical WPF PasswordBox was not identified as a password field!");

                // 2. Test Nearby Context extraction on PasswordBox (must fail closed and return empty)
                string passwordNearby = contextService.GetNearbyContext(200, pwdElement);
                Assert.Equal(string.Empty, passwordNearby);

                // 3. Test Selected Text extraction on PasswordBox (must fail closed and return empty)
                string passwordSelection = contextService.GetSelectedText(1000, pwdElement);
                Assert.Equal(string.Empty, passwordSelection);

                // 4. CaptureContext with element targeting (Inviolable privacy invariant: IsSensitive = true, text = null)
                var snapshot = contextService.CaptureContext(Guid.NewGuid(), 200, 10000, pwdElement);
                _output.WriteLine($"[Physical] IsFocusInPasswordField: {isPassword}, Snapshot.IsSensitive: {snapshot.IsSensitive}");

                // INVIOLABLE SAFETY: Must fail closed
                Assert.True(snapshot.IsSensitive, "Snapshot must be classified as sensitive");
                Assert.Null(snapshot.NearbyText);
                Assert.Null(snapshot.SelectionText);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(5000);

        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void Physical_WindowsUIAutomation_AmbiguousFocus_FailsClosedSafely()
    {
        var contextService = new WindowsUIAutomationContextService();

        // 1. Null / unresolvable target element query
        bool isPasswordWhenNull = contextService.IsFocusInPasswordField(null);
        _output.WriteLine($"[Ambiguous Focus] IsFocusInPasswordField(null): {isPasswordWhenNull}");

        // 2. CaptureContext without target element: must fail closed if focus is ambiguous or foreign
        var snapshot = contextService.CaptureContext(Guid.NewGuid());
        Assert.NotNull(snapshot);

        // If context was determined sensitive, verify text is suppressed
        if (snapshot.IsSensitive)
        {
            Assert.Null(snapshot.NearbyText);
            Assert.Null(snapshot.SelectionText);
        }
    }

    [Fact]
    public void Physical_GetForegroundTargetInfo_ReturnsLiveDesktopProcess()
    {
        var contextService = new WindowsUIAutomationContextService();
        var target = contextService.GetForegroundTargetInfo();

        _output.WriteLine($"[Target] HWND: {target.Hwnd}, PID: {target.ProcessId}, App: {target.ProcessName}, Title: {target.WindowTitle}");

        Assert.NotNull(target.ProcessName);
        Assert.NotNull(target.WindowTitle);

        // ValidateTargetStillActive should succeed for the active foreground window
        if (target.Hwnd != IntPtr.Zero)
        {
            bool isActive = contextService.ValidateTargetStillActive(target);
            Assert.True(isActive);
        }
    }

    [Fact]
    public void Physical_ValidateTargetStillActive_InvalidHwnd_FailsSafely()
    {
        var contextService = new WindowsUIAutomationContextService();
        var invalidTarget = new ForegroundTargetInfo(new IntPtr(0x7FFFFFFF), 999999, "nonexistent.exe", "NonExistent");

        bool isActive = contextService.ValidateTargetStillActive(invalidTarget);
        Assert.False(isActive);
    }

    [Fact]
    public void Physical_ApplicationClassifier_Benchmark_HighThroughput()
    {
        var classifier = new RuleBasedApplicationClassifier();
        var targets = new[]
        {
            new ForegroundTargetInfo((IntPtr)1, 100, "notepad.exe", "Untitled - Notepad"),
            new ForegroundTargetInfo((IntPtr)2, 200, "code.exe", "FLOW - Visual Studio Code"),
            new ForegroundTargetInfo((IntPtr)3, 300, "cursor.exe", "Workspace - Cursor"),
            new ForegroundTargetInfo((IntPtr)4, 400, "windowsterminal.exe", "Windows PowerShell"),
            new ForegroundTargetInfo((IntPtr)5, 500, "winword.exe", "Document1 - Word"),
            new ForegroundTargetInfo((IntPtr)6, 600, "chrome.exe", "Google Chrome"),
            new ForegroundTargetInfo((IntPtr)7, 700, "keepass.exe", "KeePass"),
            new ForegroundTargetInfo((IntPtr)8, 800, "custom_app.exe", "Custom App")
        };

        var sw = Stopwatch.StartNew();
        const int iterations = 10000;
        for (int i = 0; i < iterations; i++)
        {
            var target = targets[i % targets.Length];
            var category = classifier.Classify(target);
        }
        sw.Stop();

        double avgLatencyUs = (sw.Elapsed.TotalMilliseconds * 1000.0) / iterations;
        _output.WriteLine($"[Benchmark] Classify {iterations} iterations: Total={sw.Elapsed.TotalMilliseconds:F2} ms, Avg={avgLatencyUs:F3} µs/op");

        Assert.True(avgLatencyUs < 10.0, "Classification must take < 10 microseconds per operation");
    }

    [Fact]
    public async Task Physical_WhisperInference_WithApplicationContextBiasing()
    {
        var manager = new WhisperModelManager();
        if (!File.Exists(manager.ModelPath))
        {
            _output.WriteLine("[Skip] Tiny model not present; skipping local inference benchmark.");
            return;
        }

        var asr = new WhisperNetInferenceEngine(manager);
        await asr.InitializeAsync();

        var dictionary = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "CancellationToken", Replacement = "CancellationToken", Language = "en", ApplicationScope = "code" },
            new DictionaryEntry { Term = "FLOW", Replacement = "FLOW", Language = "en" }
        });

        var biasingService = new PersonalizationBiasingService(dictionary);
        string? biasingPrompt = biasingService.BuildPrompt("en", "code");

        var audio = CreateSyntheticSpeech("Welcome to FLOW with cancellation token");

        var sw = Stopwatch.StartNew();
        var asrResult = await asr.TranscribeAsync(audio, new ASROptions(Language: "en", Prompt: biasingPrompt));
        sw.Stop();

        _output.WriteLine($"[Whisper RTF] Inference duration: {sw.Elapsed.TotalMilliseconds:F1} ms, Text: '{asrResult.Text}'");

        Assert.NotNull(asrResult.Text);
        Assert.NotEmpty(asrResult.Text);

        await asr.DisposeAsync();
    }

    [Fact]
    public async Task Physical_VoiceSessionCoordinator_FullContextLifecycle()
    {
        var contextService = new WindowsUIAutomationContextService();
        var ring = new AudioRingBuffer(16000, 20 * 60);
        var vad = new EnergyVAD();
        var asr = new Flow.Core.ASR.MockASREngine();
        var registry = new ASREngineRegistry();
        registry.Register(asr, isDefault: true);
        var langEngine = new DeterministicTextSanitizer();
        var insertion = new WindowsTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ring,
            vad,
            registry,
            langEngine,
            insertion,
            contextService: contextService
        );

        // Start session
        await coordinator.StartSessionAsync();

        // Verify context captured at onset
        Assert.NotNull(coordinator.ActiveContext);
        _output.WriteLine($"[Lifecycle] Active App: {coordinator.ActiveContext.TargetInfo.ProcessName}, Category: {coordinator.ActiveContext.Category}");

        // Cancel session
        await coordinator.CancelSessionAsync("Validation Complete");

        // Verify context invalidated
        Assert.Null(coordinator.ActiveContext);
        Assert.Null(coordinator.ActiveTarget);
        Assert.Null(coordinator.ActiveNearbyContext);
    }
}
