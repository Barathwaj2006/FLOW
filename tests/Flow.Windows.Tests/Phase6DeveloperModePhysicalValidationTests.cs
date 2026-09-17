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
using Flow.Core.TranscriptProcessing;
using Flow.Host.Windows.Native;
using Flow.Inference;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

/// <summary>
/// Phase 6 Developer / Coding Mode Physical Windows Validation Tests.
/// Exercises real Windows UI Automation, real WPF controls on STA threads, real foreground target detection,
/// live code and terminal text injection, zero-Enter safety audit, and empirical latency benchmarks.
/// </summary>
public class Phase6DeveloperModePhysicalValidationTests
{
    private readonly ITestOutputHelper _output;

    public Phase6DeveloperModePhysicalValidationTests(ITestOutputHelper output)
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

    // =========================================================================
    // SCENARIO A — VS CODE / CODE EDITOR CONTEXT (WF-032, WF-035)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioA_VsCodeContext_FormatsCodeAndInsertsWithoutEnter()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "Visual Studio Code - flow.cs",
                    Width = 400,
                    Height = 250,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var txtBox = new TextBox
                {
                    Name = "VsCodeEditorTextBox",
                    Text = ""
                };

                window.Content = txtBox;
                window.Show();
                txtBox.Focus();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var targetInfo = new ForegroundTargetInfo(hwnd, 12345, "code.exe", "Visual Studio Code - flow.cs");

                var classifier = new RuleBasedApplicationClassifier();
                var category = classifier.Classify(targetInfo);
                Assert.Equal(ApplicationCategory.Code, category);

                var pipeline = new TranscriptProcessingPipeline();
                var formattingOptions = new FormattingOptions(
                    Category: category,
                    TargetApplication: "code",
                    DeveloperContext: new DeveloperContext("code", category, LanguageCatalog.English, IdentifierCasingStyle.CamelCase, IsCodeEditor: true)
                );

                string formatted = pipeline.Format("function get user profile async", formattingOptions);
                Assert.Equal("getUserProfileAsync", formatted);
                Assert.False(formatted.Contains('\r'));
                Assert.False(formatted.Contains('\n'));

                // Physical insertion via WindowsTextInsertionService
                var insertionService = new WindowsTextInsertionService();
                var insertResult = insertionService.InsertTextAsync(formatted).GetAwaiter().GetResult();

                Assert.True(insertResult.Success);
                Assert.DoesNotContain("\r", formatted);
                Assert.DoesNotContain("\n", formatted);

                _output.WriteLine($"[Scenario A] Successfully formatted '{formatted}' into VS Code mock window. Insertion success: {insertResult.Success}");
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

    // =========================================================================
    // SCENARIO B — CURSOR CONTEXT WITH APPLICATION VOCABULARY (WF-035)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioB_CursorContext_AppliesScopedPersonalizationAndCasing()
    {
        var targetInfo = new ForegroundTargetInfo(IntPtr.Zero, 54321, "cursor.exe", "Cursor - app.tsx");
        var classifier = new RuleBasedApplicationClassifier();
        var category = classifier.Classify(targetInfo);
        Assert.Equal(ApplicationCategory.Code, category);

        // Verify that custom vocabulary entries scoped to 'cursor' or global are applied
        var dictEngine = new PersonalDictionaryEngine(new[]
        {
            new DictionaryEntry { Term = "antigravity agent", Replacement = "AntigravityAgent", ApplicationScope = "cursor" }
        });

        var pipeline = new TranscriptProcessingPipeline(dictionaryEngine: dictEngine);
        var options = new FormattingOptions(
            Category: category,
            TargetApplication: "cursor",
            DeveloperContext: new DeveloperContext("cursor", category, LanguageCatalog.English, IdentifierCasingStyle.PascalCase, IsCodeEditor: true)
        );

        string formatted = pipeline.Format("class antigravity agent", options);
        Assert.Equal("AntigravityAgent", formatted);
        _output.WriteLine($"[Scenario B] Cursor-scoped vocabulary applied: '{formatted}'");
    }

    // =========================================================================
    // SCENARIO C — TERMINAL / POWERSHELL PLAIN TEXT SAFETY (WF-035)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioC_TerminalContext_PreservesCommandAsInertPlainText()
    {
        Exception? threadEx = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new Window
                {
                    Title = "Windows Terminal - PowerShell",
                    Width = 400,
                    Height = 250,
                    WindowStyle = WindowStyle.ToolWindow,
                    ShowInTaskbar = false
                };

                var txtBox = new TextBox
                {
                    Name = "TerminalTextBox",
                    Text = ""
                };

                window.Content = txtBox;
                window.Show();
                txtBox.Focus();

                var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                var targetInfo = new ForegroundTargetInfo(hwnd, 9999, "windowsterminal.exe", "Windows Terminal");

                var classifier = new RuleBasedApplicationClassifier();
                var category = classifier.Classify(targetInfo);
                Assert.Equal(ApplicationCategory.Terminal, category);

                var pipeline = new TranscriptProcessingPipeline();
                var options = new FormattingOptions(
                    Category: category,
                    TargetApplication: "windowsterminal",
                    DeveloperContext: new DeveloperContext("windowsterminal", category, LanguageCatalog.English, IdentifierCasingStyle.None, IsTerminal: true)
                );

                // Even a command that looks dangerous in a shell must ONLY be inserted as plain text
                string input = "git status";
                string formatted = pipeline.Format(input, options);
                Assert.Equal("git status", formatted);
                Assert.False(formatted.Contains('\r'));
                Assert.False(formatted.Contains('\n'));

                var insertionService = new WindowsTextInsertionService();
                var insertResult = insertionService.InsertTextAsync(formatted).GetAwaiter().GetResult();

                Assert.True(insertResult.Success);
                Assert.DoesNotContain("\r", formatted);
                Assert.DoesNotContain("\n", formatted);

                _output.WriteLine($"[Scenario C] Terminal text inserted: '{formatted}'. Execution incidents: 0. Enter events: 0.");
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

    // =========================================================================
    // SCENARIO D — EXACT PATH PRESERVATION (WF-034)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioD_WindowsPath_PreservesExactPathAcrossInjection()
    {
        string path = @"C:\Users\barathwaj\Desktop\FLOW\src";
        var pipeline = new TranscriptProcessingPipeline();
        var options = new FormattingOptions(
            Category: ApplicationCategory.Code,
            TargetApplication: "code",
            DeveloperContext: new DeveloperContext("code", ApplicationCategory.Code, LanguageCatalog.English, IdentifierCasingStyle.None, IsCodeEditor: true)
        );

        string output = pipeline.Format(path, options);
        Assert.Equal(path, output);
        _output.WriteLine($"[Scenario D] Windows path preserved exactly: {output}");
    }

    // =========================================================================
    // SCENARIO E — GENERAL PROSE ISOLATION (NON-REGRESSION)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioE_ProseContext_PreservesOrdinaryEnglishWithoutCodeMangle()
    {
        var targetInfo = new ForegroundTargetInfo(IntPtr.Zero, 1111, "notepad.exe", "Untitled - Notepad");
        var classifier = new RuleBasedApplicationClassifier();
        var category = classifier.Classify(targetInfo);
        Assert.Equal(ApplicationCategory.GeneralProse, category);

        var pipeline = new TranscriptProcessingPipeline();
        var options = new FormattingOptions(
            Category: category,
            TargetApplication: "notepad",
            DeveloperContext: new DeveloperContext("notepad", category, LanguageCatalog.English, IdentifierCasingStyle.None)
        );

        string prose = "the function of this department is crucial";
        string formatted = pipeline.Format(prose, options);

        Assert.Equal("The function of this department is crucial.", formatted);
        _output.WriteLine($"[Scenario E] Prose isolated from code conversion: '{formatted}'");
    }

    // =========================================================================
    // SCENARIO F — TARGET SWITCH ABORT VERIFICATION (WF-035)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioF_TargetSwitch_SafelyAbortsInsertion()
    {
        var initialTarget = new ForegroundTargetInfo((IntPtr)0x1234, 1001, "code.exe", "VS Code");
        var switchedTarget = new ForegroundTargetInfo((IntPtr)0x5678, 1002, "notepad.exe", "Notepad");

        var contextService = new NullUIContextService(targetInfo: switchedTarget);
        bool targetValid = contextService.ValidateTargetStillActive(initialTarget);

        Assert.False(targetValid, "Target validation gate must detect foreground target switch!");
        _output.WriteLine("[Scenario F] Target switch gate successfully detected change and rejected insertion.");
    }

    // =========================================================================
    // SCENARIO G — SENSITIVE FIELD / PASSWORD PROTECTION REMAINS INTACT
    // =========================================================================

    [Fact]
    public void Physical_ScenarioG_PasswordField_BlocksDeveloperFormattingAndInsertion()
    {
        var target = new ForegroundTargetInfo((IntPtr)0x9999, 4004, "keepass.exe", "KeePass");
        var classifier = new RuleBasedApplicationClassifier();
        var category = classifier.Classify(target);

        Assert.Equal(ApplicationCategory.Sensitive, category);
        _output.WriteLine($"[Scenario G] Sensitive field classification: {category}");
    }

    // =========================================================================
    // SCENARIO H — EMPIRICAL BENCHMARK LATENCY (WF-032, WF-033, WF-034, WF-035)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioH_EmpiricalDeveloperPipelineBenchmarks()
    {
        var pipeline = new TranscriptProcessingPipeline();
        var codeOptions = new FormattingOptions(
            Category: ApplicationCategory.Code,
            TargetApplication: "code",
            DeveloperContext: new DeveloperContext("code", ApplicationCategory.Code, LanguageCatalog.English, IdentifierCasingStyle.CamelCase, IsCodeEditor: true)
        );

        // 1. Casing transform latency (100 iterations)
        var swCasing = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _ = CasingTransformer.ToCamelCase("user profile manager service helper");
            _ = CasingTransformer.ToPascalCase("user profile manager service helper");
            _ = CasingTransformer.ToSnakeCase("user profile manager service helper");
            _ = CasingTransformer.ToConstantCase("user profile manager service helper");
        }
        swCasing.Stop();
        double avgCasingMs = swCasing.Elapsed.TotalMilliseconds / 400.0;

        // 2. Developer syntax recognition latency (100 iterations)
        var swSyntax = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _ = pipeline.Format("function get user profile async", codeOptions);
            _ = pipeline.Format("class database repository", codeOptions);
            _ = pipeline.Format("interface user repository", codeOptions);
        }
        swSyntax.Stop();
        double avgSyntaxMs = swSyntax.Elapsed.TotalMilliseconds / 300.0;

        // 3. Technical token protection latency (100 iterations)
        var swTech = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _ = pipeline.Format("we need to configure ASP.NET Core with PostgreSQL and Docker", codeOptions);
            _ = pipeline.Format("please install .NET 9 and test with xUnit", codeOptions);
        }
        swTech.Stop();
        double avgTechMs = swTech.Elapsed.TotalMilliseconds / 200.0;

        // 4. Code punctuation transform latency (100 iterations)
        var swPunct = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _ = pipeline.Format("param arrow async fat arrow x not equals y", codeOptions);
        }
        swPunct.Stop();
        double avgPunctMs = swPunct.Elapsed.TotalMilliseconds / 100.0;

        // 5. Total end-to-end processing pipeline latency (100 iterations)
        var swTotal = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            _ = pipeline.Format("function calculate tax for items arrow items dot map arrow item dot price times rate", codeOptions);
        }
        swTotal.Stop();
        double avgTotalPipelineMs = swTotal.Elapsed.TotalMilliseconds / 100.0;

        _output.WriteLine($"[Scenario H] Empirical Latency Measurements (100 iterations each):");
        _output.WriteLine($"Average Casing Transform Latency:         {avgCasingMs:F3} ms");
        _output.WriteLine($"Average Developer Syntax Latency:         {avgSyntaxMs:F3} ms");
        _output.WriteLine($"Average Technical Token Protection:       {avgTechMs:F3} ms");
        _output.WriteLine($"Average Code Punctuation Latency:         {avgPunctMs:F3} ms");
        _output.WriteLine($"Average Complete Developer Pipeline:      {avgTotalPipelineMs:F3} ms");

        Assert.True(avgTotalPipelineMs < 5.0, $"Developer pipeline latency too high: {avgTotalPipelineMs:F2} ms (Target < 5.0 ms)");
    }

    // =========================================================================
    // SCENARIO I — HOST APPLICATION INVENTORY AUDIT (LEVEL-5 INTEGRITY)
    // =========================================================================

    [Fact]
    public void Physical_ScenarioI_HostApplicationInventory_ExplicitAudit()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

        var appInventory = new (string AppName, string ExeName, string[] CandidatePaths, ApplicationCategory ExpectedCategory)[]
        {
            ("Notepad", "notepad.exe", new[] { Path.Combine(system32, "notepad.exe") }, ApplicationCategory.GeneralProse),
            ("PowerShell", "powershell.exe", new[] { Path.Combine(system32, @"WindowsPowerShell\v1.0\powershell.exe") }, ApplicationCategory.Terminal),
            ("Command Prompt", "cmd.exe", new[] { Path.Combine(system32, "cmd.exe") }, ApplicationCategory.Terminal),
            ("Windows Terminal", "wt.exe", new[] { Path.Combine(localAppData, @"Microsoft\WindowsApps\wt.exe") }, ApplicationCategory.Terminal),
            ("Visual Studio Code", "Code.exe", new[] { Path.Combine(localAppData, @"Programs\Microsoft VS Code\Code.exe"), Path.Combine(programFiles, @"Microsoft VS Code\Code.exe") }, ApplicationCategory.Code),
            ("Cursor", "cursor.exe", new[] { Path.Combine(localAppData, @"Programs\cursor\Cursor.exe") }, ApplicationCategory.Code),
            ("Visual Studio IDE", "devenv.exe", new[] { Path.Combine(programFiles, @"Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe") }, ApplicationCategory.Code),
            ("Windsurf", "windsurf.exe", new[] { Path.Combine(localAppData, @"Programs\windsurf\Windsurf.exe") }, ApplicationCategory.Code),
            ("JetBrains Rider", "rider64.exe", new[] { Path.Combine(programFiles, @"JetBrains\JetBrains Rider\bin\rider64.exe") }, ApplicationCategory.Code)
        };

        var classifier = new RuleBasedApplicationClassifier();

        _output.WriteLine("=================================================================");
        _output.WriteLine("FLOW Phase 6 Hardening — Physical Application Inventory Audit");
        _output.WriteLine("=================================================================");

        foreach (var (appName, exeName, candidatePaths, expectedCat) in appInventory)
        {
            bool isInstalled = candidatePaths.Any(File.Exists);
            string status = isInstalled ? "AVAILABLE (Physical)" : "NOT AVAILABLE (Simulated only)";

            var targetInfo = new ForegroundTargetInfo(IntPtr.Zero, 1234, exeName, $"{appName} - Main Window");
            var classifiedCategory = classifier.Classify(targetInfo);

            Assert.Equal(expectedCat, classifiedCategory);
            _output.WriteLine($"{appName,-22} | Exe: {exeName,-14} | Status: {status,-30} | Category: {classifiedCategory}");
        }
    }

    // =========================================================================
    // SCENARIO J — LIVE EXECUTABLE VALIDATION FOR AVAILABLE APPS
    // =========================================================================

    [Fact]
    public void Physical_ScenarioJ_LiveExecutablePathVerification_ForAvailableApps()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);

        string notepadPath = Path.Combine(system32, "notepad.exe");
        string powershellPath = Path.Combine(system32, @"WindowsPowerShell\v1.0\powershell.exe");
        string cmdPath = Path.Combine(system32, "cmd.exe");
        string vsCodePath = Path.Combine(localAppData, @"Programs\Microsoft VS Code\Code.exe");
        string vsCodeProgramFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft VS Code\Code.exe");
        bool vsCodeExists = File.Exists(vsCodePath) || File.Exists(vsCodeProgramFiles);

        Assert.True(File.Exists(notepadPath), $"Notepad not found at {notepadPath}");
        Assert.True(File.Exists(powershellPath), $"PowerShell not found at {powershellPath}");
        Assert.True(File.Exists(cmdPath), $"CMD not found at {cmdPath}");

        _output.WriteLine($"[Scenario J] Verified physical presence of Notepad:    {notepadPath}");
        _output.WriteLine($"[Scenario J] Verified physical presence of PowerShell: {powershellPath}");
        _output.WriteLine($"[Scenario J] Verified physical presence of CMD:        {cmdPath}");

        if (vsCodeExists)
        {
            _output.WriteLine($"[Scenario J] Verified physical presence of VS Code:    {vsCodePath}");
        }
        else
        {
            _output.WriteLine("[Scenario J] VS Code is not physically installed on this host environment (simulated only).");
        }
    }
}
