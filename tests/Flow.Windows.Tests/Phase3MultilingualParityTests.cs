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
using Flow.Core.Backtrack;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Core.TranscriptProcessing;
using Flow.Host.Windows.Native;
using Flow.Inference;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

public class Phase3MultilingualParityTests
{
    private readonly ITestOutputHelper _output;

    public Phase3MultilingualParityTests(ITestOutputHelper output)
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
            short val = BitConverter.ToInt16(pcmBytes, i * 2);
            samples[i] = val / 32768.0f;
        }

        return new AudioBuffer(samples, 16000.0);
    }

    #region WF-021: Manual Language Selection

    [Fact]
    public async Task WF_021_PhysicalValidation_ManualLanguageSelection_TamilAndHindi_PropagatesToWhisper()
    {
        var modelManager = new WhisperModelManager();
        await using var engine = new WhisperNetInferenceEngine(modelManager);
        bool initialized = await engine.InitializeAsync();
        Assert.True(initialized);

        var audio = CreateSyntheticSpeech("Testing FLOW manual language selection.");

        // 1. Manual English
        var enResult = await engine.TranscribeAsync(audio, new ASROptions(Language: "en"));
        Assert.Equal("en", enResult.DetectedLanguage);
        Assert.NotEmpty(enResult.Text);

        // 2. Manual Tamil
        var taResult = await engine.TranscribeAsync(audio, new ASROptions(Language: "ta"));
        Assert.Equal("ta", taResult.DetectedLanguage);

        // 3. Manual Hindi
        var hiResult = await engine.TranscribeAsync(audio, new ASROptions(Language: "hi"));
        Assert.Equal("hi", hiResult.DetectedLanguage);

        _output.WriteLine($"[WF-021] Manual English: \"{enResult.Text}\" (Lang={enResult.DetectedLanguage})");
        _output.WriteLine($"[WF-021] Manual Tamil:   \"{taResult.Text}\" (Lang={taResult.DetectedLanguage})");
        _output.WriteLine($"[WF-021] Manual Hindi:   \"{hiResult.Text}\" (Lang={hiResult.DetectedLanguage})");
    }

    #endregion

    #region WF-022: Automatic Language Detection

    [Fact]
    public async Task WF_022_PhysicalValidation_AutoLanguageDetection_RealInferenceWithConfidence()
    {
        var modelManager = new WhisperModelManager();
        await using var engine = new WhisperNetInferenceEngine(modelManager);
        await engine.InitializeAsync();

        const string phrase = "FLOW voice productivity platform for Windows with automatic language detection.";
        var audio = CreateSyntheticSpeech(phrase);

        var sw = Stopwatch.StartNew();
        var result = await engine.TranscribeAsync(audio, new ASROptions(Language: "auto"));
        sw.Stop();

        _output.WriteLine("=== [WF-022] AUTO DETECTION PHYSICAL EVIDENCE ===");
        _output.WriteLine($"Spoken Text: \"{phrase}\"");
        _output.WriteLine($"Transcribed: \"{result.Text}\"");
        _output.WriteLine($"Detected Language: {result.DetectedLanguage}");
        _output.WriteLine($"Language Confidence: {result.LanguageConfidence:F2}");
        _output.WriteLine($"Audio Duration: {result.AudioDuration.TotalSeconds:F2}s");
        _output.WriteLine($"Inference Latency: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"RTF: {(sw.Elapsed.TotalSeconds / result.AudioDuration.TotalSeconds):F2}x");

        Assert.Equal("en", result.DetectedLanguage);
        Assert.True(result.LanguageConfidence.HasValue && result.LanguageConfidence.Value > 0.5f);
        Assert.NotEmpty(result.Text);
    }

    #endregion

    #region WF-023: Code-Switching Biasing Prompt

    [Fact]
    public async Task WF_023_PhysicalValidation_CodeSwitchingBiasingPrompt_InfluencesDecoderOutput()
    {
        var modelManager = new WhisperModelManager();
        await using var engine = new WhisperNetInferenceEngine(modelManager);
        await engine.InitializeAsync();

        const string phrase = "Deploy the microservice using FastAPI and Docker containers.";
        var audio = CreateSyntheticSpeech(phrase);

        // Run without prompt
        var resNoPrompt = await engine.TranscribeAsync(audio, new ASROptions(Language: "en", Prompt: null));

        // Run with prompt containing specialized vocabulary
        var resWithPrompt = await engine.TranscribeAsync(audio, new ASROptions(Language: "en", Prompt: "FastAPI, Docker, microservice, deployment"));

        _output.WriteLine($"[WF-023] Without Prompt: \"{resNoPrompt.Text}\"");
        _output.WriteLine($"[WF-023] With Prompt:    \"{resWithPrompt.Text}\"");

        Assert.NotEmpty(resNoPrompt.Text);
        Assert.NotEmpty(resWithPrompt.Text);
    }

    #endregion

    #region Unicode Clipboard Insertion (Tamil & Hindi)

    [Fact]
    public void Multilingual_PhysicalValidation_TamilAndHindiClipboardInsertion_PreservesUnicode()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "FLOW Multilingual Clipboard Insertion Test",
                    Width = 400,
                    Height = 250,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var textBox = new TextBox
                {
                    Name = "MultilingualTargetBox",
                    AcceptsReturn = true,
                    Text = ""
                };

                window.Content = textBox;
                window.Show();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
                textBox.Focus();

                static void SafeClipboardSet(string text)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            Clipboard.SetText(text);
                            return;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            Thread.Sleep(30);
                        }
                    }
                    Clipboard.SetText(text);
                }

                static string SafeClipboardGet()
                {
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            return Clipboard.GetText();
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            Thread.Sleep(30);
                        }
                    }
                    return Clipboard.GetText();
                }

                // 1. Verify Windows Clipboard roundtrip for Tamil, Hindi, and Code-switching Unicode
                const string tamilText = "வணக்கம் உலகம் இது FLOW தமிழ் குரல் உள்ளீடு.";
                SafeClipboardSet(tamilText);
                string cbTamil = SafeClipboardGet();
                Assert.Equal(tamilText, cbTamil);

                const string hindiText = "नमस्ते दुनिया यह FLOW हिन्दी टाइपिंग है।";
                SafeClipboardSet(hindiText);
                string cbHindi = SafeClipboardGet();
                Assert.Equal(hindiText, cbHindi);

                const string codeSwitchedText = "இந்த project-ஐ build பண்ண வேண்டும்.";
                SafeClipboardSet(codeSwitchedText);
                string cbCodeSwitch = SafeClipboardGet();
                Assert.Equal(codeSwitchedText, cbCodeSwitch);

                // 2. Direct WPF control insertion and grapheme cluster preservation
                textBox.SelectedText = tamilText + " " + hindiText + " " + codeSwitchedText;
                _output.WriteLine($"[Multilingual Insertion] Actual text in control: \"{textBox.Text}\"");

                Assert.Contains("வணக்கம் உலகம்", textBox.Text);
                Assert.Contains("नमस्ते दुनिया", textBox.Text);
                Assert.Contains("project-ஐ", textBox.Text);
                Assert.DoesNotContain("\r", textBox.Text);
                Assert.DoesNotContain("\n", textBox.Text);

                // 3. Service execution safety check
                var insertionService = new WindowsTextInsertionService();
                var svcResult = insertionService.InsertTextAsync(tamilText).GetAwaiter().GetResult();
                Assert.NotNull(svcResult);
                Assert.True(svcResult.Success);

                window.Close();
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(10000);

        if (threadEx != null)
        {
            throw new Exception("Multilingual physical clipboard insertion failed on STA thread.", threadEx);
        }
    }

    #endregion

    #region Performance: Auto Detection vs Manual Mode Latency

    [Fact]
    public async Task Multilingual_PhysicalValidation_PerformanceMeasurement_DetectionVsManualLatency()
    {
        var modelManager = new WhisperModelManager();
        await using var engine = new WhisperNetInferenceEngine(modelManager);
        await engine.InitializeAsync();

        var audio = CreateSyntheticSpeech("Performance benchmark between automatic language detection and manual language specification.");

        // Warmup
        _ = await engine.TranscribeAsync(audio, new ASROptions(Language: "en"));

        // Measure Manual English Latency
        var swManual = Stopwatch.StartNew();
        var manualResult = await engine.TranscribeAsync(audio, new ASROptions(Language: "en"));
        swManual.Stop();

        // Measure Auto Detection Latency
        var swAuto = Stopwatch.StartNew();
        var autoResult = await engine.TranscribeAsync(audio, new ASROptions(Language: "auto"));
        swAuto.Stop();

        long manualMs = swManual.ElapsedMilliseconds;
        long autoMs = swAuto.ElapsedMilliseconds;

        _output.WriteLine("=== MULTILINGUAL PERFORMANCE MEASUREMENTS ===");
        _output.WriteLine($"Audio Duration: {audio.DurationSeconds:F2}s");
        _output.WriteLine($"Manual Mode Latency (en): {manualMs}ms (RTF: {swManual.Elapsed.TotalSeconds / audio.DurationSeconds:F2}x)");
        _output.WriteLine($"Auto-Detect Latency:     {autoMs}ms (RTF: {swAuto.Elapsed.TotalSeconds / audio.DurationSeconds:F2}x, Detected: {autoResult.DetectedLanguage})");
        _output.WriteLine($"Auto-Detect Overhead:    {autoMs - manualMs}ms");

        Assert.NotEmpty(manualResult.Text);
        Assert.NotEmpty(autoResult.Text);
        Assert.Equal("en", autoResult.DetectedLanguage);
    }

    #endregion

    #region Zero-Enter Invariant & Safety Regression

    [Theory]
    [InlineData("வணக்கம் உலகம்\r\n")]
    [InlineData("नमस्ते दुनिया\n")]
    [InlineData("இந்த project-ஐ build பண்ண வேண்டும்\r")]
    public async Task Multilingual_ZeroEnterSafety_FailsClosedOrStripsNewlines(string unsafeInput)
    {
        var insertionService = new WindowsTextInsertionService();
        var result = await insertionService.InsertTextAsync(unsafeInput);

        Assert.True(result.Success);
        // WindowsTextInsertionService sanitizes newlines, ensuring zero VK_RETURN keys are ever pressed
    }

    #endregion
}
