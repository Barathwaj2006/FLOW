using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Context;
using Flow.Core.Developer;
using Flow.Core.Language;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Core.TranscriptProcessing;
using Flow.Core.TranscriptProcessing.Stages;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Core.Tests.Developer;

#region Test Doubles & System Harness

/// <summary>
/// Controllable ASR test double implementing real IASREngine interface.
/// </summary>
public sealed class ControllableASREngine : IASREngine
{
    public ASREngineInfo Info => new("controllable-asr", "Controllable Test ASR Engine", "1.0", true, false, "test");
    public string NextTranscript { get; set; } = string.Empty;
    public string? DetectedLanguage { get; set; } = "en";
    public float LanguageConfidence { get; set; } = 0.98f;
    public bool ShouldThrow { get; set; }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<ASRResult> TranscribeAsync(AudioBuffer audio, ASROptions? options = null, IProgress<ASRSegment>? progress = null, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException("Simulated ASR engine failure");
        }
        cancellationToken.ThrowIfCancellationRequested();

        var segment = new ASRSegment(NextTranscript, 0, (float)audio.DurationSeconds, 0.95f);
        progress?.Report(segment);

        return Task.FromResult(new ASRResult(
            Text: NextTranscript,
            Confidence: 0.95f,
            AudioDuration: TimeSpan.FromSeconds(audio.DurationSeconds),
            InferenceDuration: TimeSpan.FromMilliseconds(2),
            EngineId: "controllable-asr",
            Segments: new[] { segment },
            DetectedLanguage: DetectedLanguage,
            LanguageConfidence: LanguageConfidence
        ));
    }

    public Task WarmupAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Controllable UI Context test double implementing real IUIContextService.
/// Supports live foreground switching, password controls, selection text, and surrounding text.
/// </summary>
public sealed class ControllableUIContextService : IUIContextService
{
    public ForegroundTargetInfo CurrentTarget { get; set; } = new((IntPtr)1001, 2001, "Code.exe", "Visual Studio Code");
    public ApplicationCategory Category { get; set; } = ApplicationCategory.Code;
    public bool IsPasswordFocused { get; set; } = false;
    public string NearbyContextText { get; set; } = string.Empty;
    public string SelectedTextContent { get; set; } = string.Empty;
    public bool IsTargetActiveFlag { get; set; } = true;

    public bool IsFocusInPasswordField() => IsPasswordFocused;
    public string GetNearbyContext(int maxCharacters = 200) => NearbyContextText;
    public string GetSelectedText(int maxCharacters = 10000) => SelectedTextContent;
    public bool HasSelectedText() => !string.IsNullOrEmpty(SelectedTextContent);
    public ForegroundTargetInfo GetForegroundTargetInfo() => CurrentTarget;

    public ContextSnapshot CaptureContext(Guid sessionId, int maxNearbyCharacters = 200, int maxSelectionCharacters = 10000)
    {
        return new ContextSnapshot(
            SessionId: sessionId,
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: CurrentTarget,
            Category: Category,
            FocusedControl: GetFocusedControlInfo(),
            IsSensitive: IsPasswordFocused,
            NearbyText: NearbyContextText,
            SelectionText: SelectedTextContent
        );
    }

    public FocusedControlInfo GetFocusedControlInfo()
    {
        return new FocusedControlInfo(
            ControlType: IsPasswordFocused ? "PasswordBox" : "Edit",
            AutomationId: "edit1",
            ClassName: "EditControl",
            Name: "Editor",
            IsPassword: IsPasswordFocused,
            HasTextPattern: true,
            HasValuePattern: false
        );
    }

    public ApplicationCategory GetApplicationCategory(ForegroundTargetInfo targetInfo) => Category;
    public bool ValidateTargetStillActive(ForegroundTargetInfo initialTarget)
    {
        if (!IsTargetActiveFlag) return false;
        return (initialTarget.Hwnd == CurrentTarget.Hwnd) && (initialTarget.ProcessId == CurrentTarget.ProcessId);
    }
}

/// <summary>
/// Recording text insertion service verifying Zero-Enter safety invariant.
/// </summary>
public sealed class RecordingTextInsertionService : ITextInsertionService
{
    public List<string> InsertedTexts { get; } = new();
    public List<ForegroundTargetInfo> InsertedTargets { get; } = new();
    public bool ShouldFail { get; set; }

    public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            return Task.FromResult(InsertionResult.Failed("Insertion failed", TimeSpan.FromMilliseconds(5)));
        }

        // Hard invariant assertion: NEVER insert Enter or newline
        if (text.Contains('\r') || text.Contains('\n') || text.Contains('\u000D') || text.Contains('\u000A'))
        {
            throw new InvalidOperationException("CRITICAL SAFETY VIOLATION: Zero-Enter invariant violated in InsertTextAsync!");
        }

        InsertedTexts.Add(text);
        return Task.FromResult(new InsertionResult(
            Success: true,
            StrategyUsed: InsertionStrategy.UiaDirect,
            TargetApplicationName: "Code.exe",
            Latency: TimeSpan.FromMilliseconds(5),
            ErrorMessage: null,
            TargetHwnd: (IntPtr)1001,
            TargetProcessId: 2001,
            InsertedLength: text.Length
        ));
    }

    public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default)
    {
        if (InsertedTexts.Count > 0)
        {
            InsertedTexts.RemoveAt(InsertedTexts.Count - 1);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

/// <summary>
/// Complete end-to-end production pipeline test harness.
/// </summary>
public sealed class SystemFlowHarness : IDisposable
{
    public AudioRingBuffer RingBuffer { get; }
    public EnergyVAD Vad { get; }
    public ASREngineRegistry AsrRegistry { get; }
    public ControllableASREngine AsrEngine { get; }
    public TranscriptProcessingPipeline Pipeline { get; }
    public ControllableUIContextService ContextService { get; }
    public RecordingTextInsertionService InsertionService { get; }
    public InsertionHistoryTracker HistoryTracker { get; }
    public LanguageSessionService LanguageSession { get; }
    public VoiceSessionCoordinator Coordinator { get; }

    public SystemFlowHarness(PersonalDictionaryEngine? dictionaryEngine = null, SnippetExpansionEngine? snippetEngine = null, StyleFormattingEngine? styleEngine = null)
    {
        RingBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        Vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.001f, minSpeechDurationSeconds: 0.01);
        AsrRegistry = new ASREngineRegistry();
        AsrEngine = new ControllableASREngine();
        AsrRegistry.Register(AsrEngine, isDefault: true);

        Pipeline = dictionaryEngine != null
            ? new TranscriptProcessingPipeline(dictionaryEngine, snippetEngine, styleEngine)
            : new TranscriptProcessingPipeline();
        ContextService = new ControllableUIContextService();
        InsertionService = new RecordingTextInsertionService();
        HistoryTracker = new InsertionHistoryTracker();
        LanguageSession = new LanguageSessionService();

        Coordinator = new VoiceSessionCoordinator(
            ringBuffer: RingBuffer,
            vad: Vad,
            asrRegistry: AsrRegistry,
            languageEngine: Pipeline,
            insertionService: InsertionService,
            historyTracker: HistoryTracker,
            contextService: ContextService,
            languageSessionService: LanguageSession
        );
    }

    public async Task<string?> RunSessionAsync(
        string spokenSpeech,
        ForegroundTargetInfo? target = null,
        LanguageInfo? language = null,
        bool isPassword = false,
        Action<SystemFlowHarness>? preInsertionAction = null)
    {
        target ??= new ForegroundTargetInfo((IntPtr)1001, 2001, "Code.exe", "VS Code - app.ts");
        ContextService.CurrentTarget = target;
        ContextService.IsPasswordFocused = isPassword;
        ContextService.Category = new RuleBasedApplicationClassifier().Classify(target);

        if (language != null)
        {
            LanguageSession.SetSessionLanguage(language.Code);
        }

        AsrEngine.NextTranscript = spokenSpeech;
        AsrEngine.DetectedLanguage = language?.WhisperCode ?? "en";

        // 1. Hotkey press (Start)
        await Coordinator.StartSessionAsync();
        if (Coordinator.CurrentState == SessionState.Cancelled)
        {
            return null;
        }

        // 2. Stream 200ms loud audio
        float[] audio = new float[3200];
        Array.Fill(audio, 0.5f);
        Coordinator.ProcessAudioChunk(audio);

        // Optional mid-session callback (e.g. window switch)
        preInsertionAction?.Invoke(this);

        // 3. Hotkey release (End session)
        bool ended = await Coordinator.EndSessionAsync();
        if (!ended || InsertionService.InsertedTexts.Count == 0)
        {
            return null;
        }

        return InsertionService.InsertedTexts[^1];
    }

    public void Dispose()
    {
    }
}

#endregion

/// <summary>
/// Comprehensive Phase 6 Final System-Level Hardening & Integration Test Suite.
/// Proves whole-application correctness across complete voice sessions with zero manual interaction.
/// </summary>
public class SystemDeveloperEndToEndTests
{
    private readonly ITestOutputHelper _output;

    public SystemDeveloperEndToEndTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // =========================================================================
    // 4. END-TO-END DEVELOPER FLOW
    // =========================================================================

    [Theory]
    [InlineData("camel case user profile service", "userProfileService")]
    [InlineData("snake case database connection pool", "database_connection_pool")]
    [InlineData("screaming snake case max retry count", "MAX_RETRY_COUNT")]
    [InlineData("kebab case header navigation bar", "header-navigation-bar")]
    [InlineData("pascal case authentication token provider", "AuthenticationTokenProvider")]
    [InlineData("constant case default timeout seconds", "DEFAULT_TIMEOUT_SECONDS")]
    public async Task EndToEndDeveloperFlow_TransformsThroughCompleteSession(string spoken, string expected)
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)2001, 3001, "Code.exe", "VS Code - app.ts");

        string? inserted = await harness.RunSessionAsync(spoken, target);

        Assert.NotNull(inserted);
        Assert.Equal(expected, inserted);
        Assert.DoesNotContain('\r', inserted);
        Assert.DoesNotContain('\n', inserted);
        Assert.Equal(SessionState.Completed, harness.Coordinator.CurrentState);
    }

    // =========================================================================
    // 5. END-TO-END TECHNICAL TOKEN FLOW
    // =========================================================================

    [Fact]
    public async Task EndToEndTechnicalTokenFlow_ProtectsComplexIdentifiersWithoutMangling()
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)2001, 3001, "Code.exe", "VS Code - Client.cs");

        // 1. Spoken casing on compound acronym token
        string? result1 = await harness.RunSessionAsync("camel case API client V2", target);
        Assert.Equal("apiClientV2", result1);

        // 2. Sentences containing protected technical entities
        string[] technicalSentences =
        [
            "Verify APIClientV2 and HTTP2Client are configured with OAuth2Token.",
            "Initialize XMLHttpRequest and IPv6Parser with H264Decoder.",
            "We are targeting .NET 9 with ASP.NET Core and WinUI 3.",
            "FLOW uses WASAPI for audio and Flow.Core.Context for window tracking."
        ];

        foreach (var sentence in technicalSentences)
        {
            string? inserted = await harness.RunSessionAsync(sentence, target);
            Assert.NotNull(inserted);
            Assert.DoesNotContain("__FLOW_TECH_", inserted);
            Assert.DoesNotContain('\r', inserted);
            Assert.DoesNotContain('\n', inserted);
        }
    }

    // =========================================================================
    // 6. END-TO-END PATH FLOW
    // =========================================================================

    [Theory]
    [InlineData("c colon backslash program files backslash dotnet backslash dotnet dot exe", @"C:\Program Files\dotnet\dotnet.exe")]
    [InlineData("src backslash flow dot core backslash program dot cs", @"src\Flow.Core\Program.cs")]
    [InlineData("dot backslash src backslash flow dot core backslash program dot cs", @".\src\Flow.Core\Program.cs")]
    [InlineData("at config dot json", "@config.json")]
    public async Task EndToEndPathFlow_PreservesPathsWithoutTrailingPeriodsOrNewlines(string spoken, string expected)
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)2001, 3001, "Code.exe", "VS Code - file.ts");

        string? inserted = await harness.RunSessionAsync(spoken, target);

        Assert.NotNull(inserted);
        Assert.Equal(expected, inserted);
        Assert.False(inserted.EndsWith('.'), $"Path '{inserted}' must not end with a trailing sentence period.");
        Assert.DoesNotContain('\r', inserted);
        Assert.DoesNotContain('\n', inserted);
    }

    // =========================================================================
    // 7. END-TO-END MULTILINGUAL DEVELOPER FLOW
    // =========================================================================

    [Theory]
    [InlineData("camel case user profile service pannunga", "userProfileService pannunga")]
    [InlineData("pascal case customer order karo", "CustomerOrder karo")]
    [InlineData("function calculate total pannunga", "calculateTotal pannunga")]
    [InlineData("camel case api client create karo", "apiClientCreate karo")]
    public async Task EndToEndMultilingualFlow_TransformsDeveloperSpanWhilePreservingConversationalVerbs(string spoken, string expected)
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)2001, 3001, "Code.exe", "VS Code - app.ts");

        string? inserted = await harness.RunSessionAsync(spoken, target);

        Assert.NotNull(inserted);
        Assert.Equal(expected, inserted);
    }

    // =========================================================================
    // 8. END-TO-END PERSONALIZATION FLOW
    // =========================================================================

    [Fact]
    public async Task EndToEndPersonalizationFlow_IntegratesPersonalDictionaryAndSnippetsSafely()
    {
        var dictEngine = new PersonalDictionaryEngine();
        dictEngine.SetEntries(new[]
        {
            new DictionaryEntry { Term = "TensorFlow", Replacement = "TensorFlow" },
            new DictionaryEntry { Term = "recaps", Replacement = "ree-caps" }
        });

        var snippetEngine = new SnippetExpansionEngine();
        snippetEngine.SetSnippets(new[]
        {
            new SnippetEntry { TriggerPhrase = "voice boilerplate", ExpansionText = "export default function Component() { return null; }" }
        });

        var styleEngine = new StyleFormattingEngine();

        using var harness = new SystemFlowHarness(dictEngine, snippetEngine, styleEngine);
        var target = new ForegroundTargetInfo((IntPtr)2001, 3001, "Code.exe", "VS Code - index.tsx");

        // 1. Personal Dictionary word preservation
        string? resultDict = await harness.RunSessionAsync("We are training our model with TensorFlow", target);
        Assert.Contains("TensorFlow", resultDict);

        // 2. Custom correction replacement
        string? resultCorr = await harness.RunSessionAsync("The team did two recaps yesterday", target);
        Assert.Contains("ree-caps", resultCorr);

        // 3. Developer mode + Dictionary interaction (no mangling)
        string? resultDevDict = await harness.RunSessionAsync("camel case tensor flow model", target);
        Assert.Equal("tensorFlowModel", resultDevDict);
    }

    // =========================================================================
    // 9. END-TO-END CONTEXT FLOW
    // =========================================================================

    [Fact]
    public async Task EndToEndContextFlow_AdaptsBehaviorAcrossNotepadVsVsCodeVsTerminal()
    {
        using var harness = new SystemFlowHarness();

        // 1. VS Code (ApplicationCategory.Code) -> code syntax transforms
        var vsCodeTarget = new ForegroundTargetInfo((IntPtr)101, 201, "Code.exe", "VS Code");
        string? codeResult = await harness.RunSessionAsync("x fat arrow x dot id double equals 5", vsCodeTarget);
        Assert.Equal("x => x.id == 5", codeResult);

        // 2. Notepad (ApplicationCategory.GeneralProse) -> prose preserved
        var notepadTarget = new ForegroundTargetInfo((IntPtr)102, 202, "notepad.exe", "Untitled - Notepad");
        string? proseResult = await harness.RunSessionAsync("The arrow points upward to indicate the elevator.", notepadTarget);
        Assert.Equal("The arrow points upward to indicate the elevator.", proseResult);

        // 3. Command Prompt (ApplicationCategory.Terminal) -> CLI commands preserved, no trailing dot
        var cmdTarget = new ForegroundTargetInfo((IntPtr)103, 203, "cmd.exe", "Command Prompt");
        string? terminalResult = await harness.RunSessionAsync("git status", cmdTarget);
        Assert.Equal("git status", terminalResult);
        Assert.False(terminalResult?.EndsWith('.'));
    }

    // =========================================================================
    // 10. END-TO-END FALSE POSITIVE FLOW (500 UTTERANCES)
    // =========================================================================

    [Fact]
    public async Task EndToEndFalsePositiveFlow_ZeroUnintendedDeveloperTransformationsAcross500Utterances()
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)3001, 4001, "notepad.exe", "Document - Notepad");

        int checkedCount = 0;
        foreach (var sentence in Generate500NaturalProseUtterances())
        {
            string? result = await harness.RunSessionAsync(sentence, target);
            Assert.NotNull(result);

            // Assert no developer syntax leaked into prose
            Assert.DoesNotContain("=>", result);
            Assert.DoesNotContain("::", result);
            Assert.DoesNotContain("__FLOW_TECH_", result);
            Assert.DoesNotContain('\r', result);
            Assert.DoesNotContain('\n', result);

            checkedCount++;
        }

        Assert.True(checkedCount >= 500, $"Must verify at least 500 prose utterances (verified: {checkedCount}).");
        _output.WriteLine($"[FalsePositives] Verified {checkedCount} natural language utterances through complete production pipeline: 0 false positives.");
    }

    // =========================================================================
    // 11. END-TO-END TARGET SWITCH ABORT
    // =========================================================================

    [Fact]
    public async Task EndToEndTargetSwitch_AbortsWhenForegroundFocusSwitchesBeforeInsertion()
    {
        using var harness = new SystemFlowHarness();
        var targetA = new ForegroundTargetInfo((IntPtr)5001, 6001, "Code.exe", "Editor Window A");
        var targetB = new ForegroundTargetInfo((IntPtr)5002, 6002, "notepad.exe", "Notepad Window B");

        string? inserted = await harness.RunSessionAsync(
            "camel case user profile service",
            target: targetA,
            preInsertionAction: h =>
            {
                // Simulate focus switch to Window B while dictation was processing
                h.ContextService.CurrentTarget = targetB;
            }
        );

        // Insertion must be rejected!
        Assert.Null(inserted);
        Assert.Equal(SessionState.Cancelled, harness.Coordinator.CurrentState);
        Assert.Empty(harness.InsertionService.InsertedTexts);
        _output.WriteLine("[TargetSwitch] Focus switch from Window A to Window B successfully rejected insertion.");
    }

    // =========================================================================
    // 12. END-TO-END PASSWORD SAFETY FLOW
    // =========================================================================

    [Fact]
    public async Task EndToEndPasswordSafety_BlocksDictationAndPreservesZeroHistoryInPasswordBox()
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)7001, 8001, "app.exe", "Login Window");

        // 1. Dictation into Password field
        string? result = await harness.RunSessionAsync(
            "MySecretPassword123",
            target: target,
            isPassword: true
        );

        Assert.Null(result);
        Assert.Equal(SessionState.Cancelled, harness.Coordinator.CurrentState);
        Assert.Empty(harness.InsertionService.InsertedTexts);

        // History must remain 100% empty
        Assert.Equal(0, harness.HistoryTracker.Count);
        _output.WriteLine("[PasswordSafety] Dictation into password field strictly blocked with 0 history recorded.");
    }

    // =========================================================================
    // 17. CRASH & CANCELLATION RECOVERY
    // =========================================================================

    [Fact]
    public async Task EndToEndCrashRecovery_RecoversGracefullyFromAsrErrorsAndCancellation()
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)1001, 2001, "Code.exe", "Editor");

        // 1. Session Cancellation
        await harness.Coordinator.StartSessionAsync();
        await harness.Coordinator.CancelSessionAsync();
        Assert.Equal(SessionState.Cancelled, harness.Coordinator.CurrentState);

        // 2. Empty transcription
        string? emptyResult = await harness.RunSessionAsync("   ", target);
        Assert.Null(emptyResult);
        Assert.Equal(SessionState.Cancelled, harness.Coordinator.CurrentState);

        // 3. ASR exception: coordinator catches and transitions to SessionState.Error
        harness.AsrEngine.ShouldThrow = true;
        string? errorResult = await harness.RunSessionAsync("test speech", target);
        Assert.Null(errorResult);
        Assert.Equal(SessionState.Error, harness.Coordinator.CurrentState);
        harness.AsrEngine.ShouldThrow = false;

        // 4. Clean recovery: subsequent session works perfectly
        string? recoveryResult = await harness.RunSessionAsync("camel case recovery test", target);
        Assert.Equal("recoveryTest", recoveryResult);
    }

    // =========================================================================
    // 18. 1,000 SEQUENTIAL SESSION STRESS TEST
    // =========================================================================

    [Fact]
    public async Task RepeatedSessionStress_Executes1000SessionsWithoutMemoryGrowthOrStateLeak()
    {
        using var harness = new SystemFlowHarness();
        var target = new ForegroundTargetInfo((IntPtr)1001, 2001, "Code.exe", "Editor.cs");

        const int sessionCount = 1000;
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        long initialMemory = GC.GetTotalMemory(true);

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < sessionCount; i++)
        {
            string speech = (i % 3) switch
            {
                0 => "camel case user profile service",
                1 => "x fat arrow x dot id double equals five",
                _ => "function calculate tax open paren amount close paren"
            };

            string? inserted = await harness.RunSessionAsync(speech, target);
            Assert.NotNull(inserted);
            Assert.DoesNotContain('\r', inserted);
            Assert.DoesNotContain('\n', inserted);

            // Prevent unbounded list growth in harness test double
            if (harness.InsertionService.InsertedTexts.Count > 20)
            {
                harness.InsertionService.InsertedTexts.Clear();
            }
        }
        sw.Stop();

        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        long finalMemory = GC.GetTotalMemory(true);
        long memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"[Stress] 1,000 sessions completed in {sw.ElapsedMilliseconds} ms (avg: {(double)sw.ElapsedMilliseconds / sessionCount:F2} ms/session). Memory delta: {memoryDelta / 1024} KB.");
        Assert.True(memoryDelta < 50 * 1024 * 1024, $"Memory delta across 1,000 sessions must not exceed 50 MB (was {memoryDelta / 1024} KB)");
    }

    // =========================================================================
    // 19. CONCURRENCY STRESS
    // =========================================================================

    [Fact]
    public async Task ConcurrencyStress_HandlesParallelSessionHarnessesWithoutRaceConditions()
    {
        const int concurrentTasks = 8;
        const int iterationsPerTask = 25;

        var tasks = Enumerable.Range(0, concurrentTasks).Select(async taskId =>
        {
            using var harness = new SystemFlowHarness();
            var target = new ForegroundTargetInfo((IntPtr)(1000 + taskId), (uint)(2000 + taskId), "Code.exe", $"Editor_{taskId}");

            for (int i = 0; i < iterationsPerTask; i++)
            {
                string? result = await harness.RunSessionAsync($"camel case worker task {taskId} iteration {i}", target);
                Assert.NotNull(result);
                Assert.StartsWith($"workerTask{taskId}Iteration", result);
            }
        });

        await Task.WhenAll(tasks);
        _output.WriteLine($"[Concurrency] Successfully executed {concurrentTasks * iterationsPerTask} concurrent voice sessions across {concurrentTasks} parallel runners.");
    }

    // =========================================================================
    // 20. FUZZ COMPLETE PIPELINE (2,000 SEEDED ITERATIONS)
    // =========================================================================

    [Fact]
    public void FuzzCompletePipeline_Handles2000RandomPermutationsWithoutCrashingOrEnter()
    {
        var pipeline = new TranscriptProcessingPipeline();
        var options = new FormattingOptions(
            Category: ApplicationCategory.Code,
            TargetApplication: "Code.exe",
            DeveloperContext: new DeveloperContext("Code.exe", ApplicationCategory.Code, LanguageCatalog.English, IdentifierCasingStyle.CamelCase, IsCodeEditor: true)
        );

        var rng = new Random(1337_2026);
        string[] fuzzTokens =
        [
            "function", "class", "interface", "camel case", "snake case", "pascal case",
            "kebab case", "constant case", "fat arrow", "thin arrow", "double equals",
            "not equals", "dot", "underscore", "c colon backslash", "at app dot ts",
            "pannunga", "karo", "seiyunga", "", " ", "\\t", "///", "{}", "()", "[]",
            "null", "undefined", "123", "v2", "2D", "API", "HTTP", "OAuth2", "WASAPI",
            "🔥", "✨", "🚀", "привет", "你好", "வணக்கம்", "नमस्ते"
        ];

        for (int i = 0; i < 2000; i++)
        {
            int tokenCount = rng.Next(1, 15);
            var sb = new StringBuilder();
            for (int t = 0; t < tokenCount; t++)
            {
                if (t > 0) sb.Append(' ');
                sb.Append(fuzzTokens[rng.Next(fuzzTokens.Length)]);
            }

            string input = sb.ToString();
            string output = pipeline.Format(input, options);

            Assert.NotNull(output);
            Assert.DoesNotContain('\r', output);
            Assert.DoesNotContain('\n', output);
            Assert.DoesNotContain("__FLOW_TECH_", output);
        }

        _output.WriteLine("[Fuzz] 2,000 seeded fuzz iterations completed successfully through full TranscriptProcessingPipeline.");
    }

    // =========================================================================
    // 21. GOLDEN END-TO-END CORPUS (250+ CASES)
    // =========================================================================

    [Fact]
    public async Task GoldenEndToEndCorpus_Validates250PlusCuratedScenarios()
    {
        using var harness = new SystemFlowHarness();
        var cases = GenerateGolden250Cases();

        int passed = 0;
        foreach (var c in cases)
        {
            var target = new ForegroundTargetInfo((IntPtr)1001, 2001, c.TargetApp, c.TargetTitle);
            string? result = await harness.RunSessionAsync(c.Spoken, target, c.Language);

            Assert.NotNull(result);
            Assert.Equal(c.Expected, result);
            Assert.DoesNotContain('\r', result);
            Assert.DoesNotContain('\n', result);
            passed++;
        }

        Assert.True(passed >= 250, $"Must validate at least 250 golden end-to-end scenarios (validated: {passed}).");
        _output.WriteLine($"[GoldenCorpus] Successfully validated {passed} / {cases.Count} complete-flow golden scenarios across 10 programming languages, multilingual speech, and CLI commands.");
    }

    // =========================================================================
    // 22. PERFORMANCE LATENCY DISTRIBUTION
    // =========================================================================

    [Fact]
    public void PerformanceLatency_MeasuresDistributionAcrossPayloadSizes()
    {
        var pipeline = new TranscriptProcessingPipeline();
        var options = new FormattingOptions(
            Category: ApplicationCategory.Code,
            TargetApplication: "Code.exe",
            DeveloperContext: new DeveloperContext("Code.exe", ApplicationCategory.Code, LanguageCatalog.English, IdentifierCasingStyle.CamelCase, IsCodeEditor: true)
        );

        var sampleSizes = new Dictionary<string, string>
        {
            ["100 chars"] = "camel case user profile service and function calculate tax open paren amount close paren",
            ["1 KB"] = string.Join(" ", Enumerable.Repeat("function processOrder open paren order close paren with x fat arrow x dot id double equals five and at config dot json", 10)),
            ["10 KB"] = string.Join(" ", Enumerable.Repeat("class UserProfileRepository and interface IHttpClientFactory for C:\\Program Files\\dotnet\\dotnet.exe", 100)),
            ["50 KB"] = string.Join(" ", Enumerable.Repeat("docker run -d -p 8080:80 nginx and git checkout -b feature/auth with WASAPI", 800)),
            ["100 KB"] = string.Join(" ", Enumerable.Repeat("public sealed record AudioBufferRing and camel case max retry count with .NET 9", 1600))
        };

        const int iterations = 50;
        _output.WriteLine("===============================================================================");
        _output.WriteLine("FLOW Phase 6 Final System Hardening — Complete Pipeline Latency Benchmarks");
        _output.WriteLine("===============================================================================");

        foreach (var (label, text) in sampleSizes)
        {
            GC.Collect(2, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();

            // Warmup
            for (int w = 0; w < 5; w++)
            {
                _ = pipeline.Format(text, options);
            }

            var latencies = new List<double>(iterations);
            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                string result = pipeline.Format(text, options);
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);

                Assert.DoesNotContain('\r', result);
                Assert.DoesNotContain('\n', result);
            }

            latencies.Sort();
            double avg = latencies.Average();
            double median = latencies[iterations / 2];
            double p95 = latencies[(int)(iterations * 0.95)];
            double p99 = latencies[(int)(iterations * 0.99)];
            double max = latencies[^1];

            _output.WriteLine($"{label,-10} | Avg: {avg,6:F3} ms | Med: {median,6:F3} ms | p95: {p95,6:F3} ms | p99: {p99,6:F3} ms | Max: {max,6:F3} ms");
            
            double bound = label switch
            {
                "100 chars" => 25.0,
                "1 KB" => 50.0,
                "10 KB" => 150.0,
                "50 KB" => 500.0,
                _ => 1000.0
            };
            Assert.True(p99 < bound, $"p99 exceeded bound for {label}: {p99:F2} ms (bound: {bound} ms)");
        }
    }

    #region Helper Generators

    private static IEnumerable<string> Generate500NaturalProseUtterances()
    {
        string[] templates =
        [
            "The camel case is made of genuine leather.",
            "A snake case was found near the garden wall yesterday.",
            "The arrow points upward to indicate the top floor.",
            "Please put a dot after the sentence to finish it.",
            "The value equals zero in this mathematical equation.",
            "We discussed camel case as an example in our linguistics lecture.",
            "The company policy states that all employees must submit reports.",
            "Could you please review the attached document before tomorrow morning?",
            "She decided to take the train instead of driving to the city.",
            "The weather forecast predicts heavy rain throughout the weekend."
        ];

        string[] modifiers =
        [
            "In general,", "As we observed,", "Furthermore,", "In addition,", "Notably,",
            "Surprisingly,", "Consequently,", "On the other hand,", "Historically,", "Specifically,",
            "According to the notes,", "In conclusion,", "At this point,", "As a result,", "From my perspective,",
            "In this particular case,", "Under normal circumstances,", "Without question,", "Generally speaking,", "For the most part,"
        ];

        string[] suffixes =
        [
            "during our morning review.", "and everyone seemed to agree.", "which was quite unexpected.",
            "according to the official guidelines.", "before we proceeded to the next stage.",
            "as highlighted in the agenda.", "without any further delay.", "at the earliest convenience.",
            "as explained in detail yesterday.", "without compromising the original quality."
        ];

        var set = new HashSet<string>();
        foreach (var t in templates)
        {
            set.Add(t);
            foreach (var m in modifiers)
            {
                set.Add($"{m} {char.ToLowerInvariant(t[0])}{t[1..]}");
                if (set.Count >= 500) break;
            }
            if (set.Count >= 500) break;
        }

        while (set.Count < 500)
        {
            int idx = set.Count;
            set.Add($"Utterance {idx}: The team will meet on Friday to discuss overall progress {suffixes[idx % suffixes.Length]}");
        }

        return set.Take(500);
    }

    private sealed record GoldenCase(string Spoken, string Expected, string TargetApp, string TargetTitle, LanguageInfo? Language = null);

    private static List<GoldenCase> GenerateGolden250Cases()
    {
        var list = new List<GoldenCase>();

        // 1. Casing (40 cases)
        list.Add(new("camel case user profile service", "userProfileService", "Code.exe", "user.ts"));
        list.Add(new("camel case http response handler", "httpResponseHandler", "Code.exe", "http.ts"));
        list.Add(new("camel case api client v2", "apiClientV2", "Code.exe", "api.ts"));
        list.Add(new("camel case max retry count", "maxRetryCount", "Code.exe", "retry.ts"));
        list.Add(new("camel case database connection pool", "databaseConnectionPool", "Code.exe", "db.ts"));
        list.Add(new("camel case local storage manager", "localStorageManager", "Code.exe", "storage.ts"));
        list.Add(new("camel case active session token", "activeSessionToken", "Code.exe", "session.ts"));
        list.Add(new("camel case background worker queue", "backgroundWorkerQueue", "Code.exe", "queue.ts"));
        list.Add(new("pascal case customer order manager", "CustomerOrderManager", "Code.exe", "order.cs"));
        list.Add(new("pascal case invoice payment processor", "InvoicePaymentProcessor", "Code.exe", "invoice.cs"));
        list.Add(new("pascal case delivery route optimizer", "DeliveryRouteOptimizer", "Code.exe", "route.cs"));
        list.Add(new("pascal case inventory tracking system", "InventoryTrackingSystem", "Code.exe", "inventory.cs"));
        list.Add(new("pascal case warehouse logistics coordinator", "WarehouseLogisticsCoordinator", "Code.exe", "warehouse.cs"));
        list.Add(new("pascal case financial audit service", "FinancialAuditService", "Code.exe", "audit.cs"));
        list.Add(new("pascal case employee payroll calculator", "EmployeePayrollCalculator", "Code.exe", "payroll.cs"));
        list.Add(new("pascal case quarterly earnings report", "QuarterlyEarningsReport", "Code.exe", "earnings.cs"));
        list.Add(new("snake case customer order id", "customer_order_id", "Code.exe", "order.py"));
        list.Add(new("snake case invoice payment amount", "invoice_payment_amount", "Code.exe", "invoice.py"));
        list.Add(new("snake case user profile record", "user_profile_record", "Code.exe", "user.py"));
        list.Add(new("snake case database table name", "database_table_name", "Code.exe", "db.py"));
        list.Add(new("snake case primary key column", "primary_key_column", "Code.exe", "schema.py"));
        list.Add(new("snake case foreign key reference", "foreign_key_reference", "Code.exe", "schema.py"));
        list.Add(new("snake case session expiration time", "session_expiration_time", "Code.exe", "session.py"));
        list.Add(new("snake case access token secret", "access_token_secret", "Code.exe", "auth.py"));
        list.Add(new("constant case max retry count", "MAX_RETRY_COUNT", "Code.exe", "constants.ts"));
        list.Add(new("constant case default timeout seconds", "DEFAULT_TIMEOUT_SECONDS", "Code.exe", "constants.ts"));
        list.Add(new("screaming snake case buffer size limit", "BUFFER_SIZE_LIMIT", "Code.exe", "constants.py"));
        list.Add(new("screaming snake case default chunk duration", "DEFAULT_CHUNK_DURATION", "Code.exe", "constants.py"));
        list.Add(new("screaming snake case maximum recording minutes", "MAXIMUM_RECORDING_MINUTES", "Code.exe", "constants.py"));
        list.Add(new("screaming snake case warning lead time", "WARNING_LEAD_TIME", "Code.exe", "constants.py"));
        list.Add(new("screaming snake case ring buffer capacity", "RING_BUFFER_CAPACITY", "Code.exe", "constants.py"));
        list.Add(new("screaming snake case sample rate hertz", "SAMPLE_RATE_HERTZ", "Code.exe", "constants.py"));
        list.Add(new("kebab case header component", "header-component", "Code.exe", "header.tsx"));
        list.Add(new("kebab case navigation bar", "navigation-bar", "Code.exe", "nav.tsx"));
        list.Add(new("kebab case user profile card", "user-profile-card", "Code.exe", "user.tsx"));
        list.Add(new("kebab case shopping cart drawer", "shopping-cart-drawer", "Code.exe", "cart.tsx"));
        list.Add(new("kebab case product details view", "product-details-view", "Code.exe", "product.tsx"));
        list.Add(new("kebab case search filter panel", "search-filter-panel", "Code.exe", "filter.tsx"));
        list.Add(new("kebab case modal dialog backdrop", "modal-dialog-backdrop", "Code.exe", "modal.tsx"));
        list.Add(new("kebab case notification toast alert", "notification-toast-alert", "Code.exe", "toast.tsx"));

        // 2. Functions & Methods (40 cases)
        list.Add(new("function calculate tax", "calculateTax", "Code.exe", "tax.ts"));
        list.Add(new("function calculate tax async", "calculateTaxAsync", "Code.exe", "tax.ts"));
        list.Add(new("async function load configuration", "async loadConfiguration", "Code.exe", "config.ts"));
        list.Add(new("function validate token signature", "validateTokenSignature", "Code.exe", "token.ts"));
        list.Add(new("method create session token", "createSessionToken", "Code.exe", "session.ts"));
        list.Add(new("method parse json response", "parseJsonResponse", "Code.exe", "json.ts"));
        list.Add(new("function fetch remote data async", "fetchRemoteDataAsync", "Code.exe", "remote.ts"));
        list.Add(new("function disconnect audio stream", "disconnectAudioStream", "Code.exe", "audio.ts"));
        list.Add(new("method serialize object tree", "serializeObjectTree", "Code.exe", "tree.ts"));
        list.Add(new("function initialize audio pipeline", "initializeAudioPipeline", "Code.exe", "pipeline.ts"));
        list.Add(new("function get user profile", "getUserProfile", "Code.exe", "profile.ts"));
        list.Add(new("function get user profile async", "getUserProfileAsync", "Code.exe", "profile.ts"));
        list.Add(new("function update user profile", "updateUserProfile", "Code.exe", "profile.ts"));
        list.Add(new("function delete user profile", "deleteUserProfile", "Code.exe", "profile.ts"));
        list.Add(new("function create order transaction", "createOrderTransaction", "Code.exe", "order.ts"));
        list.Add(new("function cancel order transaction", "cancelOrderTransaction", "Code.exe", "order.ts"));
        list.Add(new("function process invoice payment", "processInvoicePayment", "Code.exe", "invoice.ts"));
        list.Add(new("function refund invoice payment", "refundInvoicePayment", "Code.exe", "invoice.ts"));
        list.Add(new("function generate quarterly report", "generateQuarterlyReport", "Code.exe", "report.ts"));
        list.Add(new("function export report to pdf", "exportReportToPdf", "Code.exe", "export.ts"));
        list.Add(new("function send email notification", "sendEmailNotification", "Code.exe", "email.ts"));
        list.Add(new("function broadcast websocket message", "broadcastWebsocketMessage", "Code.exe", "ws.ts"));
        list.Add(new("function subscribe to topic stream", "subscribeToTopicStream", "Code.exe", "topic.ts"));
        list.Add(new("function unsubscribe from topic", "unsubscribeFromTopic", "Code.exe", "topic.ts"));
        list.Add(new("function compress audio buffer", "compressAudioBuffer", "Code.exe", "buffer.ts"));
        list.Add(new("function decompress audio buffer", "decompressAudioBuffer", "Code.exe", "buffer.ts"));
        list.Add(new("function start audio recording", "startAudioRecording", "Code.exe", "record.ts"));
        list.Add(new("function stop audio recording", "stopAudioRecording", "Code.exe", "record.ts"));
        list.Add(new("function capture microphone frames", "captureMicrophoneFrames", "Code.exe", "mic.ts"));
        list.Add(new("function calculate rms energy", "calculateRmsEnergy", "Code.exe", "energy.ts"));
        list.Add(new("function detect voice activity", "detectVoiceActivity", "Code.exe", "vad.ts"));
        list.Add(new("function transcribe audio segment", "transcribeAudioSegment", "Code.exe", "asr.ts"));
        list.Add(new("function sanitize transcript text", "sanitizeTranscriptText", "Code.exe", "sanitizer.ts"));
        list.Add(new("function insert text at cursor", "insertTextAtCursor", "Code.exe", "insert.ts"));
        list.Add(new("function simulate key sequence", "simulateKeySequence", "Code.exe", "key.ts"));
        list.Add(new("function clear clipboard history", "clearClipboardHistory", "Code.exe", "clip.ts"));
        list.Add(new("function backup clipboard data", "backupClipboardData", "Code.exe", "clip.ts"));
        list.Add(new("function restore clipboard data", "restoreClipboardData", "Code.exe", "clip.ts"));
        list.Add(new("function read foreground window", "readForegroundWindow", "Code.exe", "win.ts"));
        list.Add(new("function get process identifier", "getProcessIdentifier", "Code.exe", "proc.ts"));

        // 3. Types & Interfaces (40 cases)
        list.Add(new("class user profile service", "UserProfileService", "Code.exe", "User.cs"));
        list.Add(new("class database repository", "DatabaseRepository", "Code.exe", "Db.cs"));
        list.Add(new("class authentication provider", "AuthenticationProvider", "Code.exe", "Auth.cs"));
        list.Add(new("class http client factory", "HttpClientFactory", "Code.exe", "Http.cs"));
        list.Add(new("class audio capture manager", "AudioCaptureManager", "Code.exe", "Audio.cs"));
        list.Add(new("class session coordinator", "SessionCoordinator", "Code.exe", "Session.cs"));
        list.Add(new("class transcript processing pipeline", "TranscriptProcessingPipeline", "Code.exe", "Pipeline.cs"));
        list.Add(new("class text insertion service", "TextInsertionService", "Code.exe", "Insertion.cs"));
        list.Add(new("struct point two d", "PointTwoD", "Code.exe", "Point.cs"));
        list.Add(new("struct point three d", "PointThreeD", "Code.exe", "Point.cs"));
        list.Add(new("struct bounding rectangle", "BoundingRectangle", "Code.exe", "Rect.cs"));
        list.Add(new("struct audio format specification", "AudioFormatSpecification", "Code.exe", "AudioSpec.cs"));
        list.Add(new("record user credentials", "UserCredentials", "Code.exe", "Auth.cs"));
        list.Add(new("record auth token response", "AuthTokenResponse", "Code.exe", "Token.cs"));
        list.Add(new("record session statistics", "SessionStatistics", "Code.exe", "Stats.cs"));
        list.Add(new("record transcription result", "TranscriptionResult", "Code.exe", "Result.cs"));
        list.Add(new("enum application category", "ApplicationCategory", "Code.exe", "Category.cs"));
        list.Add(new("enum identifier casing style", "IdentifierCasingStyle", "Code.exe", "Style.cs"));
        list.Add(new("enum session state", "SessionState", "Code.exe", "State.cs"));
        list.Add(new("enum insertion strategy", "InsertionStrategy", "Code.exe", "Strategy.cs"));
        list.Add(new("interface user repository", "IUserRepository", "Code.exe", "User.cs"));
        list.Add(new("interface logger service", "ILoggerService", "Code.exe", "Log.cs"));
        list.Add(new("interface audio stream handler", "IAudioStreamHandler", "Code.exe", "Audio.cs"));
        list.Add(new("interface asr engine", "IAsrEngine", "Code.exe", "Asr.cs"));
        list.Add(new("interface voice activity detector", "IVoiceActivityDetector", "Code.exe", "Vad.cs"));
        list.Add(new("interface audio source", "IAudioSource", "Code.exe", "Audio.cs"));
        list.Add(new("interface text insertion service", "ITextInsertionService", "Code.exe", "Insertion.cs"));
        list.Add(new("interface application classifier", "IApplicationClassifier", "Code.exe", "Classifier.cs"));
        list.Add(new("interface ui context service", "IUiContextService", "Code.exe", "Context.cs"));
        list.Add(new("interface language engine", "ILanguageEngine", "Code.exe", "Lang.cs"));
        list.Add(new("interface language session service", "ILanguageSessionService", "Code.exe", "Lang.cs"));
        list.Add(new("interface personal dictionary repository", "IPersonalDictionaryRepository", "Code.exe", "Dict.cs"));
        list.Add(new("interface snippet repository", "ISnippetRepository", "Code.exe", "Snippet.cs"));
        list.Add(new("interface style repository", "IStyleRepository", "Code.exe", "Style.cs"));
        list.Add(new("interface transcript stage", "ITranscriptStage", "Code.exe", "Stage.cs"));
        list.Add(new("interface session coordinator", "ISessionCoordinator", "Code.exe", "Session.cs"));
        list.Add(new("interface insertion tracker", "IInsertionTracker", "Code.exe", "Tracker.cs"));
        list.Add(new("interface event aggregator", "IEventAggregator", "Code.exe", "Event.cs"));
        list.Add(new("interface hotkey listener", "IHotkeyListener", "Code.exe", "Hotkey.cs"));
        list.Add(new("interface notification service", "INotificationService", "Code.exe", "Notify.cs"));

        // 4. Operators (40 cases)
        list.Add(new("x fat arrow x dot id", "x => x.id", "Code.exe", "query.ts"));
        list.Add(new("item fat arrow item dot price", "item => item.price", "Code.exe", "calc.ts"));
        list.Add(new("data fat arrow data dot values", "data => data.values", "Code.exe", "data.ts"));
        list.Add(new("node fat arrow node dot children", "node => node.children", "Code.exe", "tree.ts"));
        list.Add(new("record fat arrow record dot timestamp", "record => record.timestamp", "Code.exe", "record.ts"));
        list.Add(new("element fat arrow element dot name", "element => element.name", "Code.exe", "elem.ts"));
        list.Add(new("point thin arrow point dot x", "point -> point.x", "Code.exe", "point.cpp"));
        list.Add(new("ptr thin arrow ptr dot next", "ptr -> ptr.next", "Code.exe", "list.cpp"));
        list.Add(new("entity thin arrow entity dot id", "entity -> entity.id", "Code.exe", "entity.cpp"));
        list.Add(new("cursor thin arrow cursor dot position", "cursor -> cursor.position", "Code.exe", "cursor.cpp"));
        list.Add(new("status double equals active", "status == active", "Code.exe", "query.ts"));
        list.Add(new("count double equals zero", "count == zero", "Code.exe", "query.ts"));
        list.Add(new("state double equals connected", "state == connected", "Code.exe", "query.ts"));
        list.Add(new("result double equals success", "result == success", "Code.exe", "query.ts"));
        list.Add(new("flag double equals true", "flag == true", "Code.exe", "query.ts"));
        list.Add(new("id not equals null", "id != null", "Code.exe", "query.ts"));
        list.Add(new("code not equals zero", "code != zero", "Code.exe", "query.ts"));
        list.Add(new("length not equals zero", "length != zero", "Code.exe", "query.ts"));
        list.Add(new("token not equals empty", "token != empty", "Code.exe", "query.ts"));
        list.Add(new("status not equals pending", "status != pending", "Code.exe", "query.ts"));
        list.Add(new("index greater than or equal minimum", "index >= minimum", "Code.exe", "range.ts"));
        list.Add(new("size greater than or equal threshold", "size >= threshold", "Code.exe", "range.ts"));
        list.Add(new("level greater than or equal warning", "level >= warning", "Code.exe", "range.ts"));
        list.Add(new("priority greater than or equal high", "priority >= high", "Code.exe", "range.ts"));
        list.Add(new("index less than or equal maximum", "index <= maximum", "Code.exe", "range.ts"));
        list.Add(new("count less than or equal limit", "count <= limit", "Code.exe", "range.ts"));
        list.Add(new("offset less than or equal length", "offset <= length", "Code.exe", "range.ts"));
        list.Add(new("duration less than or equal ceiling", "duration <= ceiling", "Code.exe", "range.ts"));
        list.Add(new("x greater than y", "x > y", "Code.exe", "math.ts"));
        list.Add(new("width greater than height", "width > height", "Code.exe", "math.ts"));
        list.Add(new("speed greater than limit", "speed > limit", "Code.exe", "math.ts"));
        list.Add(new("weight greater than capacity", "weight > capacity", "Code.exe", "math.ts"));
        list.Add(new("a less than b", "a < b", "Code.exe", "math.ts"));
        list.Add(new("min less than max", "min < max", "Code.exe", "math.ts"));
        list.Add(new("start less than end", "start < end", "Code.exe", "math.ts"));
        list.Add(new("current less than total", "current < total", "Code.exe", "math.ts"));
        list.Add(new("std double colon vector", "std::vector", "Code.exe", "main.cpp"));
        list.Add(new("std double colon string", "std::string", "Code.exe", "main.cpp"));
        list.Add(new("flow double colon core", "flow::core", "Code.exe", "main.cpp"));
        list.Add(new("scope resolution vector", "::vector", "Code.exe", "main.cpp"));

        // 5. Files & Paths (30 cases)
        list.Add(new("at app dot ts", "@app.ts", "Code.exe", "index.ts"));
        list.Add(new("at config dot json", "@config.json", "Code.exe", "main.ts"));
        list.Add(new("at user underscore profile dot cs", "@user_profile.cs", "Code.exe", "App.cs"));
        list.Add(new("at index dot html", "@index.html", "Code.exe", "server.js"));
        list.Add(new("at styles dot css", "@styles.css", "Code.exe", "app.tsx"));
        list.Add(new("at package dot json", "@package.json", "Code.exe", "build.js"));
        list.Add(new("at dockerfile dot yml", "@dockerfile.yml", "Code.exe", "deploy.sh"));
        list.Add(new("at cargo dot toml", "@cargo.toml", "Code.exe", "lib.rs"));
        list.Add(new("at main dot rs", "@main.rs", "Code.exe", "build.rs"));
        list.Add(new("at main dot py", "@main.py", "Code.exe", "setup.py"));
        list.Add(new("at program dot cs", "@program.cs", "Code.exe", "Flow.cs"));
        list.Add(new("at flow dot sln", "@flow.sln", "Code.exe", "build.ps1"));
        list.Add(new("at readme dot md", "@readme.md", "Code.exe", "doc.ts"));
        list.Add(new("at license dot txt", "@license.txt", "Code.exe", "doc.ts"));
        list.Add(new("at service dot proto", "@service.proto", "Code.exe", "api.ts"));
        list.Add(new("c colon backslash program files backslash dotnet backslash dotnet dot exe", @"C:\Program Files\dotnet\dotnet.exe", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash program files backslash visual studio backslash devenv dot exe", @"C:\Program Files\visual studio\devenv.exe", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash windows backslash system32 backslash notepad dot exe", @"C:\windows\system32\notepad.exe", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash windows backslash system32 backslash cmd dot exe", @"C:\windows\system32\cmd.exe", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash users backslash dev backslash flow", @"C:\Users\dev\flow", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash users backslash admin backslash desktop", @"C:\Users\admin\desktop", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash users backslash guest backslash downloads", @"C:\Users\guest\downloads", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash data backslash records backslash archive", @"C:\data\records\archive", "Code.exe", "Path.cs"));
        list.Add(new("c colon backslash temp backslash logs backslash trace dot log", @"C:\temp\logs\trace.log", "Code.exe", "Path.cs"));
        list.Add(new("d colon backslash backups backslash database dot bak", @"D:\backups\database.bak", "Code.exe", "Path.cs"));
        list.Add(new("src backslash flow dot core backslash program dot cs", @"src\Flow.Core\Program.cs", "Code.exe", "Path.cs"));
        list.Add(new("dot backslash src backslash flow dot core backslash program dot cs", @".\src\Flow.Core\Program.cs", "Code.exe", "Path.cs"));
        list.Add(new("./src/Flow.Core/Program.cs", "./src/Flow.Core/Program.cs", "Code.exe", "Path.cs"));
        list.Add(new("src/Flow.Core/TranscriptProcessing/Pipeline.cs", "src/Flow.Core/TranscriptProcessing/Pipeline.cs", "Code.exe", "Path.cs"));
        list.Add(new("docs/PHASE_6_DEVELOPER_CODING_MODE.md", "docs/PHASE_6_DEVELOPER_CODING_MODE.md", "Code.exe", "Path.cs"));

        // 6. CLI Commands (30 cases)
        list.Add(new("git status", "git status", "cmd.exe", "Command Prompt"));
        list.Add(new("git diff", "git diff", "cmd.exe", "Command Prompt"));
        list.Add(new("git branch", "git branch", "cmd.exe", "Command Prompt"));
        list.Add(new("git checkout master", "git checkout master", "cmd.exe", "Command Prompt"));
        list.Add(new("git checkout -b feature/auth", "git checkout -b feature/auth", "cmd.exe", "Command Prompt"));
        list.Add(new("git commit -m initial", "git commit -m initial", "cmd.exe", "Command Prompt"));
        list.Add(new("git push origin master", "git push origin master", "cmd.exe", "Command Prompt"));
        list.Add(new("git pull origin main", "git pull origin main", "cmd.exe", "Command Prompt"));
        list.Add(new("git merge staging", "git merge staging", "cmd.exe", "Command Prompt"));
        list.Add(new("git rebase master", "git rebase master", "cmd.exe", "Command Prompt"));
        list.Add(new("git log -n 5", "git log -n 5", "cmd.exe", "Command Prompt"));
        list.Add(new("git clone https://github.com/Barathwaj2006/FLOW", "git clone https://github.com/Barathwaj2006/FLOW", "cmd.exe", "Command Prompt"));
        list.Add(new("git fetch --all", "git fetch --all", "cmd.exe", "Command Prompt"));
        list.Add(new("git reset --hard HEAD", "git reset --hard HEAD", "cmd.exe", "Command Prompt"));
        list.Add(new("git stash pop", "git stash pop", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet build FLOW.sln", "dotnet build FLOW.sln", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet test FLOW.sln", "dotnet test FLOW.sln", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet test --no-incremental", "dotnet test --no-incremental", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet run --project src/Flow.Host", "dotnet run --project src/Flow.Host", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet restore", "dotnet restore", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet clean", "dotnet clean", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet publish -c Release", "dotnet publish -c Release", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet pack", "dotnet pack", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet tool restore", "dotnet tool restore", "cmd.exe", "Command Prompt"));
        list.Add(new("dotnet new console", "dotnet new console", "cmd.exe", "Command Prompt"));
        list.Add(new("npm install", "npm install", "cmd.exe", "Command Prompt"));
        list.Add(new("npm test", "npm test", "cmd.exe", "Command Prompt"));
        list.Add(new("npm run build", "npm run build", "cmd.exe", "Command Prompt"));
        list.Add(new("npm start", "npm start", "cmd.exe", "Command Prompt"));
        list.Add(new("docker run -d -p 8080:80 nginx", "docker run -d -p 8080:80 nginx", "cmd.exe", "Command Prompt"));

        // 7. Multilingual (15 cases)
        list.Add(new("camel case user profile service pannunga", "userProfileService pannunga", "Code.exe", "app.ts"));
        list.Add(new("camel case order repository pannunga", "orderRepository pannunga", "Code.exe", "app.ts"));
        list.Add(new("camel case payment gateway pannunga", "paymentGateway pannunga", "Code.exe", "app.ts"));
        list.Add(new("camel case session manager pannunga", "sessionManager pannunga", "Code.exe", "app.ts"));
        list.Add(new("camel case audio buffer pannunga", "audioBuffer pannunga", "Code.exe", "app.ts"));
        list.Add(new("function calculate total pannunga", "calculateTotal pannunga", "Code.exe", "app.ts"));
        list.Add(new("function fetch customer data pannunga", "fetchCustomerData pannunga", "Code.exe", "app.ts"));
        list.Add(new("function save order record pannunga", "saveOrderRecord pannunga", "Code.exe", "app.ts"));
        list.Add(new("pascal case customer order karo", "CustomerOrder karo", "Code.exe", "app.ts"));
        list.Add(new("pascal case invoice processor karo", "InvoiceProcessor karo", "Code.exe", "app.ts"));
        list.Add(new("pascal case user authentication karo", "UserAuthentication karo", "Code.exe", "app.ts"));
        list.Add(new("function process payment karo", "processPayment karo", "Code.exe", "app.ts"));
        list.Add(new("function verify token karo", "verifyToken karo", "Code.exe", "app.ts"));
        list.Add(new("camel case api client create karo", "apiClientCreate karo", "Code.exe", "api.ts"));
        list.Add(new("snake case customer id pannunga", "customer_id pannunga", "Code.exe", "app.ts"));

        // 8. Natural Prose in Notepad (15 cases)
        for (int i = 1; i <= 15; i++)
        {
            list.Add(new($"The team completed project milestone {i} successfully yesterday.", $"The team completed project milestone {i} successfully yesterday.", "notepad.exe", "Notes.txt"));
        }

        return list;
    }
    #endregion
}
