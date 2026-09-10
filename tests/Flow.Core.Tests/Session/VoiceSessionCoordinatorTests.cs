using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.Session;

public class VoiceSessionCoordinatorTests
{
    private class MockTextInsertionService : ITextInsertionService
    {
        public List<string> InsertedTexts { get; } = new();
        public bool ShouldFail { get; set; }

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            if (ShouldFail)
            {
                return Task.FromResult(InsertionResult.Failed("Insertion error", TimeSpan.FromMilliseconds(10)));
            }

            InsertedTexts.Add(text);
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.UiaDirect, "TestApp", TimeSpan.FromMilliseconds(10)));
        }
    }

    [Fact]
    public async Task HappyPath_TranscribesAndInsertsText()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.01f, minSpeechDurationSeconds: 0.05);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine(id: "primary-asr")
        {
            DefaultTranscript = "hello from flow voice dictation",
            SimulatedLatency = TimeSpan.Zero
        };
        registry.Register(mockAsr, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new MockTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion);

        var stateTransitions = new List<SessionState>();
        coordinator.StateChanged += (state, _) => stateTransitions.Add(state);

        // 1. Press hotkey (Start)
        await coordinator.StartSessionAsync();
        Assert.Equal(SessionState.Recording, coordinator.CurrentState);

        // 2. Stream 600ms of active speech audio
        float[] loudChunk = new float[1600];
        Array.Fill(loudChunk, 0.1f);
        for (int i = 0; i < 6; i++)
        {
            coordinator.ProcessAudioChunk(loudChunk);
        }

        // 3. Release hotkey (End)
        bool success = await coordinator.EndSessionAsync();

        Assert.True(success);
        Assert.Single(mockInsertion.InsertedTexts);
        Assert.Equal("Hello from flow voice dictation.", mockInsertion.InsertedTexts[0]);
        Assert.Contains(SessionState.Recording, stateTransitions);
        Assert.Contains(SessionState.Processing, stateTransitions);
        Assert.Contains(SessionState.Inserting, stateTransitions);
        Assert.Contains(SessionState.Completed, stateTransitions);
    }

    [Fact]
    public async Task SilenceRejection_CancelsWithoutCallingInsertion()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.05f);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine();
        registry.Register(mockAsr, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new MockTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion);

        await coordinator.StartSessionAsync();

        // Feed only 100ms of pure silence
        float[] silence = new float[1600];
        coordinator.ProcessAudioChunk(silence);

        bool success = await coordinator.EndSessionAsync();

        Assert.False(success);
        Assert.Equal(SessionState.Cancelled, coordinator.CurrentState);
        Assert.Empty(mockInsertion.InsertedTexts);
    }

    [Fact]
    public async Task AsrFallback_UsesSecondaryEngineWhenPrimaryFails()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.01f, minSpeechDurationSeconds: 0.05);

        var registry = new ASREngineRegistry();
        var failingPrimary = new MockASREngine(id: "failing-primary")
        {
            SimulatedException = new InvalidOperationException("Hardware error")
        };
        var healthySecondary = new MockASREngine(id: "healthy-secondary")
        {
            DefaultTranscript = "recovered text via fallback engine",
            SimulatedLatency = TimeSpan.Zero
        };

        registry.Register(failingPrimary, isDefault: true, priority: 10);
        registry.Register(healthySecondary, isDefault: false, priority: 5);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new MockTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion);

        await coordinator.StartSessionAsync();

        float[] loudChunk = new float[1600];
        Array.Fill(loudChunk, 0.1f);
        for (int i = 0; i < 6; i++)
        {
            coordinator.ProcessAudioChunk(loudChunk);
        }

        bool success = await coordinator.EndSessionAsync();

        Assert.True(success);
        Assert.Single(mockInsertion.InsertedTexts);
        Assert.Equal("Recovered text via fallback engine.", mockInsertion.InsertedTexts[0]);
    }

    [Fact]
    public async Task ZeroEnter_EnforcesNoNewlinesInserted()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.01f, minSpeechDurationSeconds: 0.05);
        var registry = new ASREngineRegistry();

        // ASR erroneously emits text with newlines and returns
        var asrWithNewlines = new MockASREngine
        {
            DefaultTranscript = "Line 1\r\nLine 2\nLine 3\rLine 4",
            SimulatedLatency = TimeSpan.Zero
        };
        registry.Register(asrWithNewlines, isDefault: true);

        var sanitizer = new DeterministicTextSanitizer();
        var mockInsertion = new MockTextInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, mockInsertion);

        await coordinator.StartSessionAsync();

        float[] loudChunk = new float[1600];
        Array.Fill(loudChunk, 0.1f);
        for (int i = 0; i < 6; i++)
        {
            coordinator.ProcessAudioChunk(loudChunk);
        }

        await coordinator.EndSessionAsync();

        Assert.Single(mockInsertion.InsertedTexts);
        string inserted = mockInsertion.InsertedTexts[0];
        Assert.DoesNotContain("\r", inserted);
        Assert.DoesNotContain("\n", inserted);
        Assert.Equal("Line 1 Line 2 Line 3 Line 4.", inserted);
    }
}
