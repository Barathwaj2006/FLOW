using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.Session;

public class VoiceSessionCoordinatorSpeculativeTests
{
    private class TrackingInsertionService : ITextInsertionService
    {
        public List<string> InsertedTexts { get; } = new();
        public List<(string Prev, string Next)> SpeculativeUpdates { get; } = new();
        public List<string> ClearedSpeculatives { get; } = new();

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            InsertedTexts.Add(text);
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.UiaDirect, "TestApp", TimeSpan.FromMilliseconds(5), TargetHwnd: (IntPtr)1234, TargetProcessId: 5678, InsertedLength: text.Length));
        }

        public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> UpdateSpeculativeTextAsync(string previousSpeculative, string newSpeculative, CancellationToken cancellationToken = default)
        {
            SpeculativeUpdates.Add((previousSpeculative, newSpeculative));
            return Task.FromResult(true);
        }

        public Task<bool> ClearSpeculativeTextAsync(string currentSpeculative, CancellationToken cancellationToken = default)
        {
            ClearedSpeculatives.Add(currentSpeculative);
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task SpeculativeInference_FiresPartialTranscriptReceived_DuringRecording()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.005f, minSpeechDurationSeconds: 0.05);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine(id: "speculative-asr")
        {
            DefaultTranscript = "streaming partial text",
            SimulatedLatency = TimeSpan.Zero
        };
        registry.Register(mockAsr, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new TrackingInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion)
        {
            SpeculativeInferenceIntervalMs = 40
        };

        var partials = new List<string>();
        coordinator.PartialTranscriptReceived += t => partials.Add(t);

        bool started = await coordinator.StartSessionAsync();
        Assert.True(started);
        Assert.Equal(SessionState.Recording, coordinator.CurrentState);

        // Feed > 0.8s of speech samples (16,000 samples = 1.0s)
        float[] speechChunk = new float[16000];
        for (int i = 0; i < speechChunk.Length; i++) speechChunk[i] = 0.25f;
        coordinator.ProcessAudioChunk(speechChunk);

        // Allow speculative loop to trigger at 40ms interval
        await Task.Delay(140);

        Assert.NotEmpty(partials);
        Assert.Equal("streaming partial text", partials[^1]);
        Assert.Equal("streaming partial text", coordinator.LastSpeculativeTranscript);

        bool ended = await coordinator.EndSessionAsync();
        Assert.True(ended);
    }

    [Fact]
    public async Task SpeculativeInference_DoesNotFire_IfNoSpeechDetected()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.05f, minSpeechDurationSeconds: 0.05);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine(id: "speculative-asr")
        {
            DefaultTranscript = "should not fire",
            SimulatedLatency = TimeSpan.Zero
        };
        registry.Register(mockAsr, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new TrackingInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion)
        {
            SpeculativeInferenceIntervalMs = 40
        };

        var partials = new List<string>();
        coordinator.PartialTranscriptReceived += t => partials.Add(t);

        bool started = await coordinator.StartSessionAsync();
        Assert.True(started);

        // Feed silence (amplitude 0.0f)
        float[] silenceChunk = new float[16000];
        coordinator.ProcessAudioChunk(silenceChunk);

        await Task.Delay(100);

        Assert.Empty(partials);
        Assert.Empty(coordinator.LastSpeculativeTranscript);

        await coordinator.CancelSessionAsync();
    }

    [Fact]
    public async Task SpeculativeInference_CleansUpOnCancelSession()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.005f);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine(id: "spec-asr")
        {
            DefaultTranscript = "speculative draft",
            SimulatedLatency = TimeSpan.Zero
        };
        registry.Register(mockAsr, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new TrackingInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion)
        {
            SpeculativeInferenceIntervalMs = 30,
            EnableSpeculativeInjection = true
        };

        await coordinator.StartSessionAsync();

        float[] speechChunk = new float[16000];
        for (int i = 0; i < speechChunk.Length; i++) speechChunk[i] = 0.3f;
        coordinator.ProcessAudioChunk(speechChunk);

        await Task.Delay(100);

        await coordinator.CancelSessionAsync("Cancelled by user");

        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.Empty(coordinator.LastSpeculativeTranscript);
    }

    [Fact]
    public void SpeculativeDiffing_CalculatesPrefixAndSuffixCorrectly()
    {
        string prev = "Hello world";
        string next = "Hello wonderful world";

        int minLen = Math.Min(prev.Length, next.Length);
        int commonPrefixLen = 0;
        while (commonPrefixLen < minLen && prev[commonPrefixLen] == next[commonPrefixLen])
        {
            commonPrefixLen++;
        }

        int deleteCount = prev.Length - commonPrefixLen;
        string appendText = next.Substring(commonPrefixLen);

        Assert.Equal(8, commonPrefixLen); // "Hello wo"
        Assert.Equal(3, deleteCount);      // "rld" (3 chars to delete)
        Assert.Equal("nderful world", appendText);
    }

    [Theory]
    [InlineData("hello\rworld", "hello world")]
    [InlineData("test\nnewline", "test newline")]
    [InlineData("line1\r\nline2", "line1  line2")]
    public void SpeculativeInjection_StrictZeroEnterGuaranteed(string raw, string expectedSafe)
    {
        string safe = raw.Replace("\r", " ").Replace("\n", " ");
        Assert.DoesNotContain("\r", safe);
        Assert.DoesNotContain("\n", safe);
        Assert.Equal(expectedSafe, safe);
    }
}
