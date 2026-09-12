using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.ASR;

public class MultilingualOptionsTests
{
    private sealed class OptionTrackingASREngine : IASREngine
    {
        public ASROptions? LastReceivedOptions { get; private set; }

        public ASREngineInfo Info => new("option-tracker", "Option Tracking Engine", "1.0", true, false, "mock");

        public Task<bool> InitializeAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<ASRResult> TranscribeAsync(
            AudioBuffer audio,
            ASROptions? options = null,
            IProgress<ASRSegment>? segmentProgress = null,
            CancellationToken cancellationToken = default)
        {
            LastReceivedOptions = options;
            return Task.FromResult(new ASRResult(
                Text: "recognized speech",
                Confidence: 0.95f,
                AudioDuration: TimeSpan.FromSeconds(1),
                InferenceDuration: TimeSpan.FromMilliseconds(50),
                EngineId: Info.Id,
                Segments: Array.Empty<ASRSegment>(),
                DetectedLanguage: options?.Language == "auto" ? "en" : options?.Language,
                LanguageConfidence: 0.98f
            ));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class DummyInsertionService : ITextInsertionService
    {
        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(new InsertionResult(true, InsertionStrategy.SendInputClipboardFallback, "test", TimeSpan.Zero, null, IntPtr.Zero, 1, text.Length));

        public Task<bool> BacktrackAsync(Flow.Core.Backtrack.InsertionRecord record, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    [Fact]
    public async Task Coordinator_SelectedLanguage_PropagatesToASROptions()
    {
        var engine = new OptionTrackingASREngine();
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new DeterministicTextSanitizer();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);
        coordinator.SelectedLanguage = "ta"; // Tamil manual selection (WF-021)

        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();

        Assert.NotNull(engine.LastReceivedOptions);
        Assert.Equal("ta", engine.LastReceivedOptions.Language);
    }

    [Fact]
    public async Task Coordinator_AutoLanguageDetection_PropagatesAutoToASROptions()
    {
        var engine = new OptionTrackingASREngine();
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new DeterministicTextSanitizer();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);
        coordinator.SelectedLanguage = "auto"; // Automatic detection (WF-022)

        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();

        Assert.NotNull(engine.LastReceivedOptions);
        Assert.Equal("auto", engine.LastReceivedOptions.Language);
    }

    [Fact]
    public async Task Coordinator_CodeSwitchingPrompt_PropagatesPromptBiasing()
    {
        var engine = new OptionTrackingASREngine();
        var asr = new ASREngineRegistry();
        asr.Register(engine, isDefault: true);

        var ring = new AudioRingBuffer(5.0, 16000.0);
        var vad = new EnergyVAD(16000.0);
        var lang = new DeterministicTextSanitizer();
        var insertion = new DummyInsertionService();

        var coordinator = new VoiceSessionCoordinator(ring, vad, asr, lang, insertion);
        coordinator.SelectedLanguage = "ta";
        coordinator.CodeSwitchingBiasingPrompt = "meeting-ku, PR review, deployment"; // Vocabulary biasing (WF-023)

        await coordinator.StartSessionAsync();
        float[] samples = new float[16000];
        Array.Fill(samples, 0.2f);
        coordinator.ProcessAudioChunk(samples);
        await coordinator.EndSessionAsync();

        Assert.NotNull(engine.LastReceivedOptions);
        Assert.Equal("meeting-ku, PR review, deployment", engine.LastReceivedOptions.Prompt);
    }
}
