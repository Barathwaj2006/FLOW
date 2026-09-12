using System;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Commands;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandModeSessionCoordinatorTests
{
    private class TestInsertionService : ITextInsertionService
    {
        public string? LastInsertedText { get; private set; }
        public int InsertCallCount { get; private set; }

        public Task<InsertionResult> InsertTextAsync(string text, System.Threading.CancellationToken cancellationToken = default)
        {
            LastInsertedText = text;
            InsertCallCount++;
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.UiaDirect, "TestApp", TimeSpan.FromMilliseconds(1), null, new IntPtr(1234), 999, text.Length));
        }

        public Task<bool> BacktrackAsync(Flow.Core.Backtrack.InsertionRecord record, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    private static (VoiceSessionCoordinator coordinator, MockASREngine mockAsr, TestInsertionService insertion, AudioRingBuffer ringBuffer)
        CreateTestHarness(IUIContextService? contextService = null)
    {
        var ringBuffer = new AudioRingBuffer(10.0);
        var vad = new EnergyVAD(16000.0, energyThreshold: 0.01f, minSpeechDurationSeconds: 0.05);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine();
        registry.Register(mockAsr, isDefault: true);
        var sanitizer = new DeterministicTextSanitizer();
        var insertion = new TestInsertionService();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer,
            vad,
            registry,
            sanitizer,
            insertion,
            contextService: contextService
        );

        return (coordinator, mockAsr, insertion, ringBuffer);
    }

    private static void FeedSpeech(VoiceSessionCoordinator coordinator)
    {
        float[] chunk = new float[1600];
        Array.Fill(chunk, 0.2f);
        coordinator.ProcessAudioChunk(chunk);
        coordinator.ProcessAudioChunk(chunk);
    }

    [Fact]
    public async Task NormalDictation_DangerousShellPhrase_InsertedAsInertText_NeverExecuted()
    {
        // WF-038 Invariant: In normal dictation, speech is permanently inert text
        var (coordinator, mockAsr, insertion, _) = CreateTestHarness();
        mockAsr.DefaultTranscript = "shutdown /s /t 0";

        await coordinator.StartSessionAsync();
        Assert.Equal(SessionMode.Dictation, coordinator.CurrentMode);

        FeedSpeech(coordinator);

        bool success = await coordinator.EndSessionAsync();

        Assert.True(success);
        Assert.Equal(1, insertion.InsertCallCount);
        // Cleaned text inserted as plain text
        Assert.Contains("shutdown", insertion.LastInsertedText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommandMode_TransformSelection_ReplacesSelectedTextWithBullets()
    {
        // WF-036 & WF-037A: Highlight text + speak command -> replace with bullets
        string highlightedText = "apples, bananas, oranges";
        var context = new NullUIContextService(isPasswordField: false, nearbyContext: "", selectedText: highlightedText);
        var (coordinator, mockAsr, insertion, _) = CreateTestHarness(context);

        mockAsr.DefaultTranscript = "make bullet points";

        await coordinator.StartCommandSessionAsync();
        Assert.Equal(SessionMode.Command, coordinator.CurrentMode);
        Assert.Equal(highlightedText, coordinator.ActiveSelectedText);

        FeedSpeech(coordinator);

        bool success = await coordinator.EndSessionAsync();

        Assert.True(success);
        Assert.Equal(1, insertion.InsertCallCount);
        Assert.Equal("• Apples • Bananas • Oranges", insertion.LastInsertedText);
    }

    [Fact]
    public async Task CommandMode_DangerousShellPhrase_BlockedBySafetyPolicy()
    {
        // WF-038: In Command Mode, shell commands are blocked and never executed
        var context = new NullUIContextService(isPasswordField: false, nearbyContext: "", selectedText: "some selected text");
        var (coordinator, mockAsr, insertion, _) = CreateTestHarness(context);

        mockAsr.DefaultTranscript = "shutdown /s /t 0";

        await coordinator.StartCommandSessionAsync();
        FeedSpeech(coordinator);

        bool success = await coordinator.EndSessionAsync();

        Assert.False(success);
        Assert.Equal(0, insertion.InsertCallCount);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }

    [Fact]
    public async Task CommandMode_NoTextSelected_WarnsAndDoesNotModify()
    {
        // When no text is selected for transform, reports safe status without failing destructively
        var context = new NullUIContextService(isPasswordField: false, nearbyContext: "", selectedText: "");
        var (coordinator, mockAsr, insertion, _) = CreateTestHarness(context);

        mockAsr.DefaultTranscript = "make bullet points";

        await coordinator.StartCommandSessionAsync();
        FeedSpeech(coordinator);

        bool success = await coordinator.EndSessionAsync();

        Assert.True(success);
        Assert.Equal(0, insertion.InsertCallCount); // Nothing inserted because no text was selected
    }

    [Fact]
    public async Task CommandMode_PasswordField_RefusesOnsetImmediately()
    {
        // WF-030 fail-closed protection applies to Command Mode
        var context = new NullUIContextService(isPasswordField: true, nearbyContext: "", selectedText: "secret");
        var (coordinator, _, insertion, _) = CreateTestHarness(context);

        await coordinator.StartCommandSessionAsync();

        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.Equal(0, insertion.InsertCallCount);
    }

    [Fact]
    public async Task CommandMode_EscapeCancellation_ResetsToIdle()
    {
        var context = new NullUIContextService(isPasswordField: false, nearbyContext: "", selectedText: "text");
        var (coordinator, _, _, _) = CreateTestHarness(context);

        await coordinator.StartCommandSessionAsync();
        Assert.Equal(SessionState.Recording, coordinator.CurrentState);

        await coordinator.CancelSessionAsync("User pressed Escape");

        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
    }
}
