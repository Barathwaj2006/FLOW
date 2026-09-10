using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Audio;

namespace Flow.Core.ASR;

/// <summary>
/// Local Whisper ASR engine coordinator supporting DirectML GPU acceleration and CPU fallback.
/// Bridges between the application core and native inference backends.
/// </summary>
public sealed class LocalWhisperEngine : IASREngine
{
    private readonly IASREngine? _nativeBackend;
    private readonly string? _modelPath;
    private bool _isInitialized;

    public ASREngineInfo Info { get; }

    public LocalWhisperEngine(IASREngine? nativeBackend = null, string? modelPath = null)
    {
        _nativeBackend = nativeBackend;
        _modelPath = modelPath;

        Info = new ASREngineInfo(
            Id: "local-whisper",
            DisplayName: "Local Whisper Native Engine",
            Version: "1.0.0",
            IsAvailable: nativeBackend?.Info.IsAvailable ?? false,
            RequiresGpu: false,
            ModelName: !string.IsNullOrEmpty(modelPath) ? Path.GetFileName(modelPath) : "ggml-tiny.en.bin"
        );
    }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_nativeBackend != null)
        {
            _isInitialized = await _nativeBackend.InitializeAsync(cancellationToken);
            return _isInitialized;
        }

        _isInitialized = false;
        return false;
    }

    public async Task<ASRResult> TranscribeAsync(
        AudioBuffer audio,
        ASROptions? options = null,
        IProgress<ASRSegment>? segmentProgress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audio);

        if (audio.Samples.Length == 0)
        {
            return ASRResult.Empty(Info.Id);
        }

        if (_nativeBackend != null)
        {
            return await _nativeBackend.TranscribeAsync(audio, options, segmentProgress, cancellationToken);
        }

        throw new ASRException(Info.Id, "Local Whisper native inference backend is not loaded. Model weights must be installed.");
    }

    public async ValueTask DisposeAsync()
    {
        _isInitialized = false;
        if (_nativeBackend != null)
        {
            await _nativeBackend.DisposeAsync();
        }
    }
}
