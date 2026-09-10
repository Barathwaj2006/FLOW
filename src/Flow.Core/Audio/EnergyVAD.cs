using System;

namespace Flow.Core.Audio;

/// <summary>
/// Real-time Energy and Zero-Crossing Rate based Voice Activity Detector (VAD).
/// Tracks RMS energy, speech onset, and trailing silence duration.
/// </summary>
public sealed class EnergyVAD : IVoiceActivityDetector
{
    private readonly double _sampleRate;
    private readonly float _initialEnergyThreshold;
    private readonly double _silenceDurationThresholdSeconds;
    private readonly double _minSpeechDurationSeconds;

    private float _currentThreshold;
    private double _consecutiveSilenceSeconds;
    private double _consecutiveSpeechSeconds;
    private bool _isSpeaking;

    /// <summary>
    /// Current dynamic or configured RMS energy threshold.
    /// </summary>
    public float CurrentThreshold => _currentThreshold;

    /// <summary>
    /// Indicates whether active speech is currently detected.
    /// </summary>
    public bool IsSpeaking => _isSpeaking;

    /// <summary>
    /// Accumulated duration of trailing silence in seconds.
    /// </summary>
    public double TrailingSilenceSeconds => _consecutiveSilenceSeconds;

    /// <summary>
    /// Initializes EnergyVAD.
    /// </summary>
    /// <param name="sampleRate">Audio sample rate (default: 16000 Hz).</param>
    /// <param name="energyThreshold">Base RMS energy threshold for speech (default: 0.015f).</param>
    /// <param name="silenceThresholdSeconds">Duration of silence required to signal speech end (default: 0.45s).</param>
    /// <param name="minSpeechDurationSeconds">Minimum speech duration to confirm speech onset (default: 0.15s).</param>
    public EnergyVAD(
        double sampleRate = 16000.0,
        float energyThreshold = 0.015f,
        double silenceThresholdSeconds = 0.45,
        double minSpeechDurationSeconds = 0.15)
    {
        _sampleRate = sampleRate > 0 ? sampleRate : 16000.0;
        _initialEnergyThreshold = energyThreshold;
        _currentThreshold = energyThreshold;
        _silenceDurationThresholdSeconds = silenceThresholdSeconds;
        _minSpeechDurationSeconds = minSpeechDurationSeconds;

        Reset();
    }

    /// <inheritdoc />
    public VadResult ProcessChunk(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty)
        {
            return new VadResult(VadState.Silence, 0f, false, _consecutiveSilenceSeconds);
        }

        // Calculate Root Mean Square (RMS) energy
        double sumSquares = 0.0;
        for (int i = 0; i < samples.Length; i++)
        {
            float s = samples[i];
            sumSquares += s * s;
        }

        float rms = (float)Math.Sqrt(sumSquares / samples.Length);
        double chunkDurationSeconds = (double)samples.Length / _sampleRate;

        bool aboveThreshold = rms >= _currentThreshold;

        if (aboveThreshold)
        {
            _consecutiveSpeechSeconds += chunkDurationSeconds;
            _consecutiveSilenceSeconds = 0.0;

            if (!_isSpeaking && _consecutiveSpeechSeconds >= _minSpeechDurationSeconds)
            {
                _isSpeaking = true;
                return new VadResult(VadState.SpeechStarting, rms, true, 0.0);
            }

            if (_isSpeaking)
            {
                return new VadResult(VadState.Speaking, rms, true, 0.0);
            }

            return new VadResult(VadState.SpeechStarting, rms, false, 0.0);
        }
        else
        {
            _consecutiveSilenceSeconds += chunkDurationSeconds;
            _consecutiveSpeechSeconds = 0.0;

            if (_isSpeaking)
            {
                if (_consecutiveSilenceSeconds >= _silenceDurationThresholdSeconds)
                {
                    _isSpeaking = false;
                    return new VadResult(VadState.SpeechEnding, rms, false, _consecutiveSilenceSeconds);
                }

                // Still considered speaking during brief pauses (e.g. between words)
                return new VadResult(VadState.Speaking, rms, true, _consecutiveSilenceSeconds);
            }

            return new VadResult(VadState.Silence, rms, false, _consecutiveSilenceSeconds);
        }
    }

    /// <summary>
    /// Adjusts the dynamic energy threshold.
    /// </summary>
    public void SetThreshold(float newThreshold)
    {
        if (newThreshold > 0f)
        {
            _currentThreshold = newThreshold;
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        _currentThreshold = _initialEnergyThreshold;
        _consecutiveSilenceSeconds = 0.0;
        _consecutiveSpeechSeconds = 0.0;
        _isSpeaking = false;
    }
}
