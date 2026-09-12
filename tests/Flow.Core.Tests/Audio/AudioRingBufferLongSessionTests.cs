using System;
using Flow.Core.Audio;
using Xunit;

namespace Flow.Core.Tests.Audio;

public class AudioRingBufferLongSessionTests
{
    [Fact]
    public void DefaultCapacity_IsTwentyMinutes_OneThousandTwoHundredSeconds()
    {
        var ringBuffer = new AudioRingBuffer();

        Assert.Equal(16000.0, ringBuffer.SampleRate);
        Assert.Equal(1200 * 16000, ringBuffer.Capacity);
        Assert.Equal(0, ringBuffer.AvailableSamples);
        Assert.Equal(0.0, ringBuffer.BufferedDurationSeconds);
        Assert.Equal(0, ringBuffer.TotalSamplesDropped);
        Assert.Equal(0, ringBuffer.TotalSamplesWritten);
    }

    [Fact]
    public void Write_FiveMinutesOfAudio_ZeroDroppedSamples()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 1200.0, sampleRate: 16000.0);
        int sampleRate = 16000;
        int chunkDurationMs = 100;
        int samplesPerChunk = sampleRate * chunkDurationMs / 1000; // 1,600 samples per 100ms chunk
        float[] chunk = new float[samplesPerChunk];
        for (int i = 0; i < chunk.Length; i++)
        {
            chunk[i] = 0.25f * (float)Math.Sin(2 * Math.PI * 440 * i / sampleRate);
        }

        // Simulate 5 minutes (300 seconds = 3,000 chunks of 100ms)
        int chunks = 3000;
        for (int i = 0; i < chunks; i++)
        {
            ringBuffer.Write(chunk);
        }

        long expectedTotalSamples = (long)chunks * samplesPerChunk; // 4,800,000 samples
        Assert.Equal(expectedTotalSamples, ringBuffer.TotalSamplesWritten);
        Assert.Equal(0, ringBuffer.TotalSamplesDropped);
        Assert.Equal(expectedTotalSamples, ringBuffer.AvailableSamples);
        Assert.Equal(300.0, ringBuffer.BufferedDurationSeconds, precision: 1);
        Assert.True(ringBuffer.PeakAmplitude > 0.2f);
    }

    [Fact]
    public void Write_ExceedingCapacity_RolloverIntegrityAndDropAccounting()
    {
        // Small buffer: 2 seconds = 32,000 samples
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 2.0, sampleRate: 16000.0);
        Assert.Equal(32000, ringBuffer.Capacity);

        // Write 3 seconds = 48,000 samples
        float[] threeSeconds = new float[48000];
        for (int i = 0; i < threeSeconds.Length; i++)
        {
            threeSeconds[i] = (float)i / threeSeconds.Length;
        }

        ringBuffer.Write(threeSeconds);

        Assert.Equal(48000, ringBuffer.TotalSamplesWritten);
        Assert.Equal(16000, ringBuffer.TotalSamplesDropped);
        Assert.Equal(32000, ringBuffer.AvailableSamples);
        Assert.Equal(2.0, ringBuffer.BufferedDurationSeconds, precision: 2);

        // Verify buffer contains the latest 32,000 samples
        var exported = ringBuffer.ToAudioBuffer();
        Assert.Equal(32000, exported.Samples.Length);
        Assert.Equal(threeSeconds[16000], exported.Samples[0]);
        Assert.Equal(threeSeconds[^1], exported.Samples[^1]);
    }

    [Fact]
    public void Clear_ResetsCountersAndRetainsZeroAllocation()
    {
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
        float[] samples = new float[16000];
        Array.Fill(samples, 0.5f);

        ringBuffer.Write(samples);
        Assert.Equal(16000, ringBuffer.AvailableSamples);

        ringBuffer.Clear();

        Assert.Equal(0, ringBuffer.AvailableSamples);
        Assert.Equal(0.0, ringBuffer.BufferedDurationSeconds);
        var exported = ringBuffer.ToAudioBuffer();
        Assert.True(exported.IsEmpty);
    }
}
