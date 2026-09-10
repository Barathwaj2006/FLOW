using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Audio;

namespace Flow.Core.ASR;

/// <summary>
/// Abstraction for pluggable speech-to-text inference engines.
/// </summary>
public interface IASREngine : IAsyncDisposable
{
    /// <summary>
    /// Metadata describing this ASR engine.
    /// </summary>
    ASREngineInfo Info { get; }

    /// <summary>
    /// Initializes models, weights, or unmanaged inference contexts.
    /// </summary>
    Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Transcribes the provided audio buffer to text.
    /// </summary>
    /// <param name="audio">16kHz mono audio buffer.</param>
    /// <param name="options">Transcription options (language, prompt, etc.).</param>
    /// <param name="segmentProgress">Optional progress reporting for streaming segments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transcription result containing full text and segment details.</returns>
    Task<ASRResult> TranscribeAsync(
        AudioBuffer audio,
        ASROptions? options = null,
        IProgress<ASRSegment>? segmentProgress = null,
        CancellationToken cancellationToken = default);
}
