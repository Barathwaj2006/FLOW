using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Audio;

namespace Flow.Core.ASR;

/// <summary>
/// Deterministic mock ASR engine for automated unit testing, CI, and fallback verification.
/// </summary>
public sealed class MockASREngine : IASREngine
{
    private readonly List<AudioBuffer> _transcribedBuffers = new();
    private readonly object _lock = new();

    public ASREngineInfo Info { get; }

    /// <summary>
    /// Default transcript returned when no custom mapper is specified.
    /// </summary>
    public string DefaultTranscript { get; set; } = "Testing local voice dictation.";

    /// <summary>
    /// Optional dynamic transcript generator based on input audio buffer.
    /// </summary>
    public Func<AudioBuffer, string>? TranscriptGenerator { get; set; }

    /// <summary>
    /// Simulated inference delay.
    /// </summary>
    public TimeSpan SimulatedLatency { get; set; } = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// If non-null, this exception will be thrown during TranscribeAsync to simulate backend failure.
    /// </summary>
    public Exception? SimulatedException { get; set; }

    /// <summary>
    /// Number of transcriptions performed.
    /// </summary>
    public int CallCount
    {
        get
        {
            lock (_lock) return _transcribedBuffers.Count;
        }
    }

    /// <summary>
    /// History of audio buffers processed.
    /// </summary>
    public IReadOnlyList<AudioBuffer> TranscribedBuffers
    {
        get
        {
            lock (_lock) return _transcribedBuffers.ToArray();
        }
    }

    public MockASREngine(
        string id = "mock-asr",
        string displayName = "Mock Test Engine",
        string modelName = "test-model-v1",
        bool isAvailable = true)
    {
        Info = new ASREngineInfo(
            Id: id,
            DisplayName: displayName,
            Version: "1.0.0",
            IsAvailable: isAvailable,
            RequiresGpu: false,
            ModelName: modelName
        );
    }

    public Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Info.IsAvailable);
    }

    public async Task<ASRResult> TranscribeAsync(
        AudioBuffer audio,
        ASROptions? options = null,
        IProgress<ASRSegment>? segmentProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);

        var stopwatch = Stopwatch.StartNew();

        if (SimulatedLatency > TimeSpan.Zero)
        {
            await Task.Delay(SimulatedLatency, cancellationToken);
        }

        if (SimulatedException != null)
        {
            throw new ASRException(Info.Id, "Simulated ASR failure.", SimulatedException);
        }

        lock (_lock)
        {
            _transcribedBuffers.Add(audio);
        }

        string text = TranscriptGenerator != null
            ? TranscriptGenerator(audio)
            : DefaultTranscript;

        var segment = new ASRSegment(text, 0.0, audio.DurationSeconds, 0.99f);
        segmentProgress?.Report(segment);

        stopwatch.Stop();

        return new ASRResult(
            Text: text,
            Confidence: 0.99f,
            AudioDuration: TimeSpan.FromSeconds(audio.DurationSeconds),
            InferenceDuration: stopwatch.Elapsed,
            EngineId: Info.Id,
            Segments: new[] { segment }
        );
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
