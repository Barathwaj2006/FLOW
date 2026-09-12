using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Core.TranscriptProcessing;
using Flow.Core.TranscriptProcessing.Stages;
using Xunit;

namespace Flow.Core.Tests.TranscriptProcessing;

/// <summary>
/// Exhaustive verification suite for Phase 2 Transcription & Smart Formatting Parity.
/// Explicitly covers all 35 mandated edge cases from the Phase 2 parity specification.
/// </summary>
public class TranscriptProcessingEdgeCaseTests
{
    private readonly TranscriptProcessingPipeline _pipeline = new();

    #region Pipeline Edge Cases 1-26

    [Fact]
    public void EdgeCase_01_EmptyTranscript_ReturnsEmptyString()
    {
        string result = _pipeline.Format("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EdgeCase_02_WhitespaceOnlyTranscript_ReturnsEmptyString()
    {
        string result = _pipeline.Format("   \t  \r\n   ");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EdgeCase_03_ExistingPunctuation_PreservedWithoutDoubling()
    {
        string result = _pipeline.Format("Hello, world! How are you?");
        Assert.Equal("Hello, world! How are you?", result);
    }

    [Fact]
    public void EdgeCase_04_MultiplePunctuationMarks_HandledNaturally()
    {
        string result = _pipeline.Format("Wait... what? Really!!");
        Assert.Equal("Wait. What? Really!", result);
    }

    [Fact]
    public void EdgeCase_05_SpokenComma_ProducesSymbolWithCorrectSpacing()
    {
        string result = _pipeline.Format("apples comma oranges and bananas");
        Assert.Equal("Apples, oranges and bananas.", result);
    }

    [Fact]
    public void EdgeCase_06_SpokenPeriod_ProducesTerminalSymbolAndCapitalization()
    {
        string result = _pipeline.Format("end of thought period next thought");
        Assert.Equal("End of thought. Next thought.", result);
    }

    [Fact]
    public void EdgeCase_07_SpokenQuestionMark_ProducesQuestionSymbol()
    {
        string result = _pipeline.Format("are you ready question mark");
        Assert.Equal("Are you ready?", result);
    }

    [Fact]
    public void EdgeCase_08_SpokenExclamation_ProducesExclamationSymbol()
    {
        string result = _pipeline.Format("this is great exclamation mark");
        Assert.Equal("This is great!", result);
    }

    [Fact]
    public void EdgeCase_09_SpokenColon_ProducesColonWithCorrectSpacing()
    {
        string result = _pipeline.Format("note colon check the logs");
        Assert.Equal("Note: check the logs.", result);
    }

    [Fact]
    public void EdgeCase_10_SpokenSemicolon_ProducesSemicolonWithCorrectSpacing()
    {
        string result = _pipeline.Format("run build semicolon run tests");
        Assert.Equal("Run build; run tests.", result);
    }

    [Fact]
    public void EdgeCase_11_FillerOnlyTranscript_CollapsesToEmptyString()
    {
        string result = _pipeline.Format("um uh hmm ah");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EdgeCase_12_LegitimateLike_PreservedAsVerbAndComparative()
    {
        string result = _pipeline.Format("I like Python and it feels like summer");
        Assert.Equal("I like Python and it feels like summer.", result);
    }

    [Fact]
    public void EdgeCase_13_TechnicalIdentifier_PreservedExactly()
    {
        string result = _pipeline.Format("invoke GetForegroundWindow in user32");
        Assert.Contains("GetForegroundWindow", result);
    }

    [Fact]
    public void EdgeCase_14_CamelCase_PreservedWithoutDestructiveLowercasing()
    {
        string result = _pipeline.Format("update userProfileData in state");
        Assert.Contains("userProfileData", result);
    }

    [Fact]
    public void EdgeCase_15_PascalCase_PreservedWithoutDestructiveLowercasing()
    {
        string result = _pipeline.Format("instantiate VoiceSessionCoordinator for dictation");
        Assert.Contains("VoiceSessionCoordinator", result);
    }

    [Fact]
    public void EdgeCase_16_SnakeCase_PreservedWithoutSplittingOnUnderscore()
    {
        string result = _pipeline.Format("read user_auth_token from headers");
        Assert.Contains("user_auth_token", result);
    }

    [Fact]
    public void EdgeCase_17_KebabCase_PreservedWithoutSplittingOnHyphen()
    {
        string result = _pipeline.Format("install package react-dom from npm");
        Assert.Contains("react-dom", result);
    }

    [Fact]
    public void EdgeCase_18_URL_PreservedWithoutAppendedPeriod()
    {
        string result = _pipeline.Format("https://github.com/flow/core");
        Assert.Equal("https://github.com/flow/core", result);
        Assert.False(result.EndsWith("."));
    }

    [Fact]
    public void EdgeCase_19_Email_PreservedWithoutCorruption()
    {
        string result = _pipeline.Format("email support@flow.desktop for inquiries");
        Assert.Contains("support@flow.desktop", result);
    }

    [Fact]
    public void EdgeCase_20_WindowsPath_PreservesBackslashesAndExtension()
    {
        string result = _pipeline.Format(@"check C:\Windows\System32\drivers\etc\hosts now");
        Assert.Contains(@"C:\Windows\System32\drivers\etc\hosts", result);
    }

    [Fact]
    public void EdgeCase_21_CliCommand_PreservedExactly()
    {
        string result = _pipeline.Format("run dotnet test and check git status");
        Assert.Contains("dotnet test", result);
        Assert.Contains("git status", result);
    }

    [Fact]
    public void EdgeCase_22_Acronym_PreservedInUppercase()
    {
        string result = _pipeline.Format("use WASAPI with GPU and HTTP REST API");
        Assert.Contains("WASAPI", result);
        Assert.Contains("GPU", result);
        Assert.Contains("HTTP", result);
        Assert.Contains("REST", result);
        Assert.Contains("API", result);
    }

    [Fact]
    public void EdgeCase_23_NumberedList_FormatsSequentialNumbers()
    {
        string result = _pipeline.Format("one open editor two write code three run test");
        Assert.Equal("1. Open editor 2. Write code 3. Run test.", result);
    }

    [Fact]
    public void EdgeCase_24_MixedListAndPunctuation_FormatsCleanly()
    {
        string result = _pipeline.Format("first check logs period second run tests period");
        Assert.Equal("1. Check logs. 2. Run tests.", result);
    }

    [Fact]
    public void EdgeCase_25_CapitalizationAfterPunctuation_AppliedConsistently()
    {
        string result = _pipeline.Format("hello period are you ready question mark yes exclamation mark");
        Assert.Equal("Hello. Are you ready? Yes!", result);
    }

    [Fact]
    public void EdgeCase_26_ExistingUppercase_NotDestructivelyLowercased()
    {
        string result = _pipeline.Format("FLOW is a WINDOWS application");
        Assert.Equal("FLOW is a WINDOWS application.", result);
    }

    [Fact]
    public void EdgeCase_36_SpokenBrackets_ProducesSquareBrackets()
    {
        string result = _pipeline.Format("array open bracket index close bracket");
        Assert.Equal("Array [index].", result);
    }

    [Fact]
    public void EdgeCase_37_SpokenBraces_ProducesCurlyBraces()
    {
        string result = _pipeline.Format("object open brace key colon value close brace");
        Assert.Equal("Object {key: value}.", result);
    }

    #endregion

    #region Session Coordinator Edge Cases 27-35

    private static (VoiceSessionCoordinator coordinator, MockASREngine mockAsr, RecordingInsertionService insertion, AudioRingBuffer ringBuffer) CreateHarness(
        IUIContextService? context = null,
        ILanguageEngine? customPipeline = null)
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 1200.0);
        var vad = new EnergyVAD(sampleRate: 16000, energyThreshold: 0.01f, silenceThresholdSeconds: 0.5);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine("primary-mock", "Default voice text.");
        registry.Register(mockAsr, isDefault: true);

        var pipeline = customPipeline ?? new TranscriptProcessingPipeline();
        var insertion = new RecordingInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            registry,
            pipeline,
            insertion,
            contextService: context
        );

        return (coordinator, mockAsr, insertion, ringBuffer);
    }

    [Fact]
    public async Task EdgeCase_27_EmptyAsrResult_CancelsCleanlyWithoutInsertion()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.DefaultTranscript = "";

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool success = await coordinator.EndSessionAsync();
        Assert.False(success);
        Assert.Empty(insertion.InsertedTexts);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }

    [Fact]
    public async Task EdgeCase_28_AsrCancellation_CancelsCleanly()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.SimulatedLatency = TimeSpan.FromMilliseconds(500);

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        var endTask = coordinator.EndSessionAsync();
        await coordinator.CancelSessionAsync("Cancelled by user");

        await endTask;
        Assert.Empty(insertion.InsertedTexts);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }

    [Fact]
    public async Task EdgeCase_29_AsrFailure_TransitionsToErrorWithoutCrashing()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.SimulatedException = new InvalidOperationException("Simulated ASR failure");

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool success = await coordinator.EndSessionAsync();
        Assert.False(success);
        Assert.Empty(insertion.InsertedTexts);
        Assert.Equal(SessionState.Error, coordinator.CurrentState);
    }

    [Fact]
    public async Task EdgeCase_30_FormattingFailure_CatchesGracefullyAndBlocksInsertion()
    {
        var failingPipeline = new FailingLanguageEngine();
        var (coordinator, mockAsr, insertion, _) = CreateHarness(customPipeline: failingPipeline);
        mockAsr.DefaultTranscript = "Some spoken text";

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool success = await coordinator.EndSessionAsync();
        Assert.False(success);
        Assert.Empty(insertion.InsertedTexts);
        Assert.Equal(SessionState.Error, coordinator.CurrentState);
    }

    [Fact]
    public async Task EdgeCase_31_ZeroEnterViolation_FailsClosedAndBlocksInsertion()
    {
        var newlinePipeline = new NewlineInjectingLanguageEngine();
        var (coordinator, mockAsr, insertion, _) = CreateHarness(customPipeline: newlinePipeline);
        mockAsr.DefaultTranscript = "Trigger text";

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool success = await coordinator.EndSessionAsync();
        Assert.False(success);
        Assert.Empty(insertion.InsertedTexts);
        Assert.Equal(SessionState.Error, coordinator.CurrentState);
    }

    [Fact]
    public async Task EdgeCase_32_BacktrackAfterInsertion_RevertsText()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.DefaultTranscript = "Text to backtrack";

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool insertSuccess = await coordinator.EndSessionAsync();
        Assert.True(insertSuccess);
        Assert.Single(insertion.InsertedTexts);

        bool backtrackSuccess = await coordinator.BacktrackAsync();
        Assert.True(backtrackSuccess);
        Assert.True(insertion.BacktrackCount > 0);
    }

    [Fact]
    public async Task EdgeCase_33_BacktrackAfterAppSwitch_SafelyAbortsWithoutModifyingTarget()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.DefaultTranscript = "Sensitive dictation";
        insertion.SimulateAppSwitchOnBacktrack = true;

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool insertSuccess = await coordinator.EndSessionAsync();
        Assert.True(insertSuccess);

        bool backtrackSuccess = await coordinator.BacktrackAsync();
        Assert.False(backtrackSuccess);
    }

    [Fact]
    public async Task EdgeCase_34_RepeatedBacktrack_PopsLifoHistoryUntilEmpty()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();

        // 1st insertion
        mockAsr.DefaultTranscript = "First message";
        await coordinator.StartSessionAsync();
        coordinator.ProcessAudioChunk(new float[16000]);
        await coordinator.EndSessionAsync();

        // 2nd insertion
        mockAsr.DefaultTranscript = "Second message";
        await coordinator.StartSessionAsync();
        coordinator.ProcessAudioChunk(new float[16000]);
        await coordinator.EndSessionAsync();

        Assert.Equal(2, insertion.InsertedTexts.Count);

        // First backtrack: pops 2nd insertion
        bool bt1 = await coordinator.BacktrackAsync();
        Assert.True(bt1);

        // Second backtrack: pops 1st insertion
        bool bt2 = await coordinator.BacktrackAsync();
        Assert.True(bt2);

        // Third backtrack: history is empty, returns false
        bool bt3 = await coordinator.BacktrackAsync();
        Assert.False(bt3);
    }

    [Fact]
    public async Task EdgeCase_35_InsertionFailure_DoesNotCorruptHistoryOrState()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.DefaultTranscript = "Failed insertion test";
        insertion.FailInsertion = true;

        await coordinator.StartSessionAsync();
        float[] speech = new float[16000];
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        bool success = await coordinator.EndSessionAsync();
        Assert.False(success);
        Assert.Equal(SessionState.Error, coordinator.CurrentState);

        // Backtrack should find nothing in history
        bool bt = await coordinator.BacktrackAsync();
        Assert.False(bt);
    }

    #endregion

    #region Helper Test Doubles

    private sealed class FailingLanguageEngine : ILanguageEngine
    {
        public string Format(string rawText, FormattingOptions? options = null)
        {
            throw new InvalidOperationException("Simulated formatting pipeline failure.");
        }
    }

    private sealed class NewlineInjectingLanguageEngine : ILanguageEngine
    {
        public string Format(string rawText, FormattingOptions? options = null)
        {
            return "Corrupted text\r\nWith newline";
        }
    }

    private sealed class RecordingInsertionService : ITextInsertionService
    {
        public List<string> InsertedTexts { get; } = new();
        public int BacktrackCount { get; private set; }
        public bool SimulateAppSwitchOnBacktrack { get; set; }
        public bool FailInsertion { get; set; }

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            if (FailInsertion)
            {
                return Task.FromResult(InsertionResult.Failed("Simulated insertion failure", TimeSpan.Zero));
            }

            InsertedTexts.Add(text);
            return Task.FromResult(new InsertionResult(
                Success: true,
                StrategyUsed: InsertionStrategy.UiaDirect,
                TargetApplicationName: "TestApp",
                Latency: TimeSpan.FromMilliseconds(5),
                ErrorMessage: null,
                TargetHwnd: new IntPtr(5555),
                TargetProcessId: 4444,
                InsertedLength: text.Length
            ));
        }

        public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default)
        {
            if (SimulateAppSwitchOnBacktrack)
            {
                return Task.FromResult(false);
            }

            BacktrackCount++;
            return Task.FromResult(true);
        }
    }

    #endregion
}
