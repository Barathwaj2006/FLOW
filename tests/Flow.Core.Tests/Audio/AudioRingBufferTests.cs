using System;
using Flow.Core.Audio;
using Xunit;

namespace Flow.Core.Tests.Audio;

public class AudioRingBufferTests
{
    [Fact]
    public void Capacity_InitializesCorrectly()
    {
        var buffer = new AudioRingBuffer(capacitySeconds: 10.0, sampleRate: 16000.0);
        Assert.Equal(160000, buffer.Capacity);
        Assert.Equal(0, buffer.AvailableSamples);
        Assert.Equal(0.0, buffer.BufferedDurationSeconds);
    }

    [Fact]
    public void WriteAndRead_MaintainsDataIntegrity()
    {
        var buffer = new AudioRingBuffer(capacitySeconds: 1.0, sampleRate: 1000.0); // 1000 samples
        float[] inputSamples = new float[200];
        for (int i = 0; i < inputSamples.Length; i++)
        {
            inputSamples[i] = (float)i / 1000f;
        }

        buffer.Write(inputSamples);

        Assert.Equal(200, buffer.AvailableSamples);
        Assert.Equal(0.2, buffer.BufferedDurationSeconds, 3);

        float[] outputSamples = new float[200];
        int readCount = buffer.Read(outputSamples);

        Assert.Equal(200, readCount);
        Assert.Equal(0, buffer.AvailableSamples);
        Assert.Equal(inputSamples, outputSamples);
    }

    [Fact]
    public void Overflow_DiscardsOldestSamples()
    {
        var buffer = new AudioRingBuffer(capacitySeconds: 1.0, sampleRate: 10.0); // Capacity = 10 samples
        float[] firstBatch = new float[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        buffer.Write(firstBatch);

        // Write 5 more, exceeding capacity 10
        float[] secondBatch = new float[] { 9, 10, 11, 12, 13 };
        buffer.Write(secondBatch);

        Assert.Equal(10, buffer.AvailableSamples);

        float[] readData = new float[10];
        int read = buffer.Read(readData);

        Assert.Equal(10, read);
        // The last 10 samples should be: 4, 5, 6, 7, 8, 9, 10, 11, 12, 13
        float[] expected = new float[] { 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
        Assert.Equal(expected, readData);
    }

    [Fact]
    public void Clear_ResetsBufferState()
    {
        var buffer = new AudioRingBuffer(capacitySeconds: 5.0, sampleRate: 16000.0);
        buffer.Write(new float[] { 0.1f, 0.2f, 0.3f });

        Assert.Equal(3, buffer.AvailableSamples);
        buffer.Clear();
        Assert.Equal(0, buffer.AvailableSamples);
        Assert.Equal(0.0, buffer.BufferedDurationSeconds);
    }

    [Fact]
    public void ToAudioBuffer_ReturnsValidSnapshot()
    {
        var buffer = new AudioRingBuffer(capacitySeconds: 2.0, sampleRate: 16000.0);
        float[] testSamples = new float[] { 0.5f, -0.5f, 0.25f, -0.25f };
        buffer.Write(testSamples);

        var audioBuffer = buffer.ToAudioBuffer();

        Assert.NotNull(audioBuffer);
        Assert.Equal(testSamples.Length, audioBuffer.Samples.Length);
        Assert.Equal(testSamples, audioBuffer.Samples);
        Assert.Equal(16000.0, audioBuffer.SampleRate);
    }
}
