using System;

namespace Flow.Core.Audio;

/// <summary>
/// Status emitted by the voice activity detector.
/// </summary>
public enum VadState
{
    Silence,
    SpeechStarting,
    Speaking,
    SpeechEnding
}

/// <summary>
/// Result of analyzing an audio chunk for voice activity.
/// </summary>
public readonly record struct VadResult(
    VadState State,
    float RmsEnergy,
    bool IsSpeech,
    double ConsecutiveSilenceSeconds
);

/// <summary>
/// Pluggable interface for Voice Activity Detection (VAD).
/// </summary>
public interface IVoiceActivityDetector
{
    /// <summary>
    /// Processes a single chunk of audio samples and updates internal speech state.
    /// </summary>
    /// <param name="samples">Span of 32-bit float audio samples (16kHz mono).</param>
    /// <returns>VadResult indicating current speech state and metrics.</returns>
    VadResult ProcessChunk(ReadOnlySpan<float> samples);

    /// <summary>
    /// Resets internal state (energy trackers, trailing silence counters).
    /// </summary>
    void Reset();
}
