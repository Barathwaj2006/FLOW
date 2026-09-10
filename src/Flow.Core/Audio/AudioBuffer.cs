using System;

namespace Flow.Core.Audio;

/// <summary>
/// Standardized 16kHz mono audio buffer for local VAD and ASR inference.
/// </summary>
public sealed class AudioBuffer
{
    public double SampleRate { get; }
    public int ChannelCount { get; }
    public float[] Samples { get; }

    public double DurationSeconds => SampleRate > 0 ? (double)Samples.Length / SampleRate : 0.0;

    public AudioBuffer(float[] samples, double sampleRate = 16000.0, int channelCount = 1)
    {
        Samples = samples ?? throw new ArgumentNullException(nameof(samples));
        SampleRate = sampleRate;
        ChannelCount = channelCount;
    }

    public static AudioBuffer Empty => new(Array.Empty<float>(), 16000.0, 1);
}
