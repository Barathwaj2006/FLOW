using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.Session;

public class VoiceSessionCoordinatorEdgeCaseTests
{
    private static (VoiceSessionCoordinator coordinator, MockASREngine mockAsr, RecordingInsertionService mockInsertion, AudioRingBuffer ringBuffer) CreateHarness(
        IUIContextService? context = null,
        double maxRecordingSeconds = 1200.0,
        double warningThresholdSeconds = 1140.0)
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 1200.0);
        var vad = new EnergyVAD(sampleRate: 16000, energyThreshold: 0.01f, silenceThresholdSeconds: 0.5);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine("primary-mock", "Hello from local voice dictation.");
        registry.Register(mockAsr, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var insertion = new RecordingInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            registry,
            sanitizer,
            insertion,
            maxRecordingSeconds: maxRecordingSeconds,
            warningThresholdSeconds: warningThresholdSeconds,
            contextService: context
        );

        return (coordinator, mockAsr, insertion, ringBuffer);
    }

    [Fact]
    public async Task RapidStartStopSpam_DoesNotThrowOrCorruptState()
    {
        var (coordinator, _, _, _) = CreateHarness();

        for (int i = 0; i < 20; i++)
        {
            await coordinator.StartSessionAsync();
            coordinator.ProcessAudioChunk(new float[160]);
            await coordinator.EndSessionAsync();
        }

        Assert.True(coordinator.CurrentState is SessionState.Idle or SessionState.Cancelled or SessionState.Completed);
    }

    [Fact]
    public async Task ConcurrentStart_DuringProcessing_SafelyRejected()
    {
        var (coordinator, mockAsr, _, ringBuffer) = CreateHarness();
        mockAsr.SimulatedLatency = TimeSpan.FromMilliseconds(200);

        await coordinator.StartSessionAsync();

        // Feed speech chunk so it triggers transcription
        float[] speech = new float[16000]; // 1 second
        Array.Fill(speech, 0.2f);
        coordinator.ProcessAudioChunk(speech);

        // End session starts async transcription
        var endTask = coordinator.EndSessionAsync();

        // While in-flight processing, attempt to start a new session
        var startAttemptTask = coordinator.StartSessionAsync();
        await startAttemptTask;

        // Verify that in-flight transcription was NOT cancelled or corrupted
        bool endResult = await endTask;
        Assert.True(endResult);
    }

    [Fact]
    public async Task ActiveTarget_CapturedAtSessionStart()
    {
        var targetInfo = new ForegroundTargetInfo(new IntPtr(12345), 9999, "Code", "main.cs - Visual Studio Code");
        var context = new NullUIContextService(targetInfo: targetInfo);
        var (coordinator, _, _, _) = CreateHarness(context);

        await coordinator.StartSessionAsync();

        Assert.NotNull(coordinator.ActiveTarget);
        Assert.Equal(new IntPtr(12345), coordinator.ActiveTarget.Hwnd);
        Assert.Equal(9999u, coordinator.ActiveTarget.ProcessId);
        Assert.Equal("Code", coordinator.ActiveTarget.ProcessName);
        Assert.Equal("main.cs - Visual Studio Code", coordinator.ActiveTarget.WindowTitle);
    }

    [Fact]
    public async Task TwentyMinuteCeiling_TriggersWarningAndAutoStops()
    {
        // Configure short threshold: warning at 0.1s, max at 0.25s
        var (coordinator, _, _, _) = CreateHarness(maxRecordingSeconds: 0.25, warningThresholdSeconds: 0.1);

        string? receivedWarning = null;
        coordinator.SessionWarning += w => receivedWarning = w;

        await coordinator.StartSessionAsync();

        // Simulate audio chunk feed over 300ms
        float[] chunk = new float[1600];
        Array.Fill(chunk, 0.15f);

        coordinator.ProcessAudioChunk(chunk);
        await Task.Delay(120);

        coordinator.ProcessAudioChunk(chunk);
        Assert.NotNull(receivedWarning);
        Assert.Contains("20-minute", receivedWarning);

        await Task.Delay(180);
        coordinator.ProcessAudioChunk(chunk);

        // Wait for auto-stop task to complete
        await Task.Delay(150);
        Assert.True(coordinator.CurrentState is SessionState.Processing or SessionState.Completed or SessionState.Cancelled or SessionState.Idle);
    }

    [Fact]
    public async Task EmptyAudio_CancelsCleanlyWithZeroInsertion()
    {
        var (coordinator, _, insertion, _) = CreateHarness();

        await coordinator.StartSessionAsync();
        // Zero samples fed
        bool result = await coordinator.EndSessionAsync();

        Assert.False(result);
        Assert.Empty(insertion.InsertedTexts);
        Assert.True(coordinator.CurrentState is SessionState.Cancelled or SessionState.Idle);
    }

    [Fact]
    public async Task EscapeKeyCancellation_AbortsSessionAndClearsBuffer()
    {
        var (coordinator, _, insertion, ringBuffer) = CreateHarness();

        await coordinator.StartSessionAsync();
        float[] chunk = new float[8000];
        Array.Fill(chunk, 0.2f);
        coordinator.ProcessAudioChunk(chunk);

        Assert.True(ringBuffer.AvailableSamples > 0);

        await coordinator.CancelSessionAsync("User pressed Escape");

        Assert.Equal(0, ringBuffer.AvailableSamples);
        Assert.Empty(insertion.InsertedTexts);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }

    [Fact]
    public async Task ZeroEnterInvariant_StrictlyEnforcedOnCompletedDictation()
    {
        var (coordinator, mockAsr, insertion, _) = CreateHarness();
        mockAsr.DefaultTranscript = "Line 1\r\nLine 2\rLine 3\nLine 4";

        await coordinator.StartSessionAsync();
        float[] chunk = new float[16000];
        Array.Fill(chunk, 0.2f);
        coordinator.ProcessAudioChunk(chunk);

        bool success = await coordinator.EndSessionAsync();
        Assert.True(success);

        Assert.Single(insertion.InsertedTexts);
        string inserted = insertion.InsertedTexts[0];

        Assert.DoesNotContain("\r", inserted);
        Assert.DoesNotContain("\n", inserted);
    }

    [Fact]
    public async Task PasswordField_RefusesDictationAndCommandMode()
    {
        var passwordContext = new NullUIContextService(isPasswordField: true);
        var (coordinator, _, insertion, _) = CreateHarness(passwordContext);

        await coordinator.StartSessionAsync();
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);

        await coordinator.StartCommandSessionAsync();
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.Empty(insertion.InsertedTexts);
    }

    private sealed class RecordingInsertionService : ITextInsertionService
    {
        public System.Collections.Generic.List<string> InsertTexts { get; } = new();
        public System.Collections.Generic.List<string> InsertedTexts => InsertTexts;

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            InsertTexts.Add(text);
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.UiaDirect, "MockApp", TimeSpan.FromMilliseconds(5), null, IntPtr.Zero, 0, text.Length));
        }

        public Task<bool> BacktrackAsync(Flow.Core.Backtrack.InsertionRecord record, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }
}
