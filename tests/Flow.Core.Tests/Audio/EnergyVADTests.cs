using System;
using Flow.Core.Audio;
using Xunit;

namespace Flow.Core.Tests.Audio;

public class EnergyVADTests
{
    [Fact]
    public void Silence_ReportsSilenceState()
    {
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.02f, silenceThresholdSeconds: 0.3);
        float[] silentChunk = new float[1600]; // 100ms of zeros

        var result = vad.ProcessChunk(silentChunk);

        Assert.Equal(VadState.Silence, result.State);
        Assert.False(result.IsSpeech);
        Assert.Equal(0.0f, result.RmsEnergy);
        Assert.True(result.ConsecutiveSilenceSeconds >= 0.1);
    }

    [Fact]
    public void SustainedHighEnergy_TriggersSpeechOnsetAndSpeaking()
    {
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.02f, minSpeechDurationSeconds: 0.15);

        // Create loud chunk with RMS > 0.02 (e.g. sine wave with amplitude 0.2, RMS ≈ 0.14)
        float[] loudChunk = new float[1600]; // 100ms
        for (int i = 0; i < loudChunk.Length; i++)
        {
            loudChunk[i] = 0.2f * (float)Math.Sin(2 * Math.PI * 440 * i / 16000.0);
        }

        // First 100ms chunk: below minSpeechDurationSeconds (0.15s), so SpeechStarting with IsSpeech = false
        var result1 = vad.ProcessChunk(loudChunk);
        Assert.Equal(VadState.SpeechStarting, result1.State);
        Assert.False(result1.IsSpeech);

        // Second 100ms chunk: total 200ms >= 0.15s, triggers speech onset!
        var result2 = vad.ProcessChunk(loudChunk);
        Assert.True(result2.IsSpeech);
        Assert.True(vad.IsSpeaking);

        // Third 100ms chunk: continues Speaking
        var result3 = vad.ProcessChunk(loudChunk);
        Assert.Equal(VadState.Speaking, result3.State);
        Assert.True(result3.IsSpeech);
    }

    [Fact]
    public void TrailingSilence_TransitionsToSpeechEnding()
    {
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.02f, silenceThresholdSeconds: 0.2, minSpeechDurationSeconds: 0.05);

        float[] loudChunk = new float[1600];
        Array.Fill(loudChunk, 0.1f);

        // Trigger speech
        vad.ProcessChunk(loudChunk);
        vad.ProcessChunk(loudChunk);
        Assert.True(vad.IsSpeaking);

        float[] silentChunk = new float[1600]; // 100ms silence

        // First 100ms silence (under 200ms silence threshold) -> Still speaking
        var res1 = vad.ProcessChunk(silentChunk);
        Assert.Equal(VadState.Speaking, res1.State);

        // Second 100ms silence (200ms >= 200ms threshold) -> SpeechEnding
        var res2 = vad.ProcessChunk(silentChunk);
        Assert.Equal(VadState.SpeechEnding, res2.State);
        Assert.False(vad.IsSpeaking);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.02f, minSpeechDurationSeconds: 0.05);
        float[] loudChunk = new float[1600];
        Array.Fill(loudChunk, 0.1f);
        vad.ProcessChunk(loudChunk);

        vad.Reset();

        Assert.False(vad.IsSpeaking);
        Assert.Equal(0.0, vad.TrailingSilenceSeconds);
    }
}
