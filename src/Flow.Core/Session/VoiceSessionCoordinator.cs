using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Language;
using Flow.Core.TextInsertion;
using Microsoft.Extensions.Logging;

namespace Flow.Core.Session;

/// <summary>
/// Central coordinator managing the end-to-end voice dictation session lifecycle:
/// Hotkey Press -> WASAPI Audio Stream -> Ring Buffer -> VAD -> Local ASR -> Language Sanitizer -> Safe Insertion.
/// </summary>
public sealed class VoiceSessionCoordinator
{
    private readonly AudioRingBuffer _ringBuffer;
    private readonly IVoiceActivityDetector _vad;
    private readonly ASREngineRegistry _asrRegistry;
    private readonly ILanguageEngine _languageEngine;
    private readonly ITextInsertionService _insertionService;
    private readonly ILogger<VoiceSessionCoordinator>? _logger;

    private readonly object _stateLock = new();
    private SessionState _currentState = SessionState.Idle;
    private CancellationTokenSource? _sessionCts;
    private bool _hasDetectedSpeechInSession;

    public SessionState CurrentState
    {
        get
        {
            lock (_stateLock) return _currentState;
        }
    }

    public event Action<SessionState, string?>? StateChanged;
    public event Action<float>? AudioLevelChanged;
    public event Action<string>? PartialTranscriptReceived;
    public event Action<string>? FinalTextInserted;

    public VoiceSessionCoordinator(
        AudioRingBuffer ringBuffer,
        IVoiceActivityDetector vad,
        ASREngineRegistry asrRegistry,
        ILanguageEngine languageEngine,
        ITextInsertionService insertionService,
        ILogger<VoiceSessionCoordinator>? logger = null)
    {
        _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
        _vad = vad ?? throw new ArgumentNullException(nameof(vad));
        _asrRegistry = asrRegistry ?? throw new ArgumentNullException(nameof(asrRegistry));
        _languageEngine = languageEngine ?? throw new ArgumentNullException(nameof(languageEngine));
        _insertionService = insertionService ?? throw new ArgumentNullException(nameof(insertionService));
        _logger = logger;
    }

    private void SetState(SessionState newState, string? detail = null)
    {
        lock (_stateLock)
        {
            _currentState = newState;
        }
        _logger?.LogDebug("Session state transitioned to {NewState} ({Detail})", newState, detail ?? "none");
        StateChanged?.Invoke(newState, detail);
    }

    /// <summary>
    /// Starts a new recording session (typically triggered on push-to-talk KeyDown).
    /// </summary>
    public Task StartSessionAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_currentState == SessionState.Recording)
            {
                _logger?.LogWarning("StartSessionAsync called while already recording. Ignoring.");
                return Task.CompletedTask;
            }

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ringBuffer.Clear();
            _vad.Reset();
            _hasDetectedSpeechInSession = false;

            SetState(SessionState.Recording, "Listening");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Feeds incoming real-time audio chunk from WASAPI capture stream.
    /// </summary>
    public void ProcessAudioChunk(ReadOnlySpan<float> samples)
    {
        if (CurrentState != SessionState.Recording || samples.IsEmpty)
        {
            return;
        }

        // 1. Process VAD
        var vadResult = _vad.ProcessChunk(samples);
        if (vadResult.IsSpeech)
        {
            _hasDetectedSpeechInSession = true;
        }

        // 2. Report RMS audio energy level for HUD animation
        AudioLevelChanged?.Invoke(vadResult.RmsEnergy);

        // 3. Store into ring buffer
        _ringBuffer.Write(samples);
    }

    /// <summary>
    /// Ends the current recording session and executes the transcription/insertion pipeline
    /// (typically triggered on push-to-talk KeyUp).
    /// </summary>
    public async Task<bool> EndSessionAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_currentState != SessionState.Recording)
            {
                return false;
            }
        }

        var ct = _sessionCts?.Token ?? cancellationToken;

        try
        {
            double duration = _ringBuffer.BufferedDurationSeconds;
            if (duration < 0.2 || (!_hasDetectedSpeechInSession && duration < 0.5))
            {
                SetState(SessionState.Cancelled, "No speech detected");
                _ringBuffer.Clear();
                return false;
            }

            SetState(SessionState.Processing, "Transcribing");

            // Extract audio buffer from ring buffer
            var audioBuffer = _ringBuffer.ToAudioBuffer();
            _ringBuffer.Clear();

            var progress = new Progress<ASRSegment>(seg =>
            {
                PartialTranscriptReceived?.Invoke(seg.Text);
            });

            // 1. Transcribe via ASR engine registry
            var asrResult = await _asrRegistry.TranscribeWithFallbackAsync(audioBuffer, progress: progress, cancellationToken: ct);

            if (string.IsNullOrWhiteSpace(asrResult.Text))
            {
                SetState(SessionState.Cancelled, "Empty transcription");
                return false;
            }

            // 2. Deterministic sanitization & Zero-Enter guarantee
            string cleanText = _languageEngine.Format(asrResult.Text);

            if (string.IsNullOrWhiteSpace(cleanText))
            {
                SetState(SessionState.Cancelled, "Cleaned text empty");
                return false;
            }

            SetState(SessionState.Inserting, "Inserting");

            // 3. Safe cursor text insertion
            var insertionResult = await _insertionService.InsertTextAsync(cleanText, ct);

            if (insertionResult.Success)
            {
                FinalTextInserted?.Invoke(cleanText);
                SetState(SessionState.Completed, "Success");
                return true;
            }
            else
            {
                SetState(SessionState.Error, insertionResult.ErrorMessage ?? "Insertion failed");
                return false;
            }
        }
        catch (OperationCanceledException)
        {
            SetState(SessionState.Cancelled, "Operation cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Voice session processing failed.");
            SetState(SessionState.Error, ex.Message);
            return false;
        }
        finally
        {
            // Auto-return to Idle state after short interval
            _ = Task.Run(async () =>
            {
                await Task.Delay(1200);
                lock (_stateLock)
                {
                    if (_currentState is SessionState.Completed or SessionState.Cancelled or SessionState.Error)
                    {
                        SetState(SessionState.Idle);
                    }
                }
            });
        }
    }

    /// <summary>
    /// Cancels active recording or processing immediately.
    /// </summary>
    public Task CancelSessionAsync(string reason = "Cancelled")
    {
        _sessionCts?.Cancel();
        _ringBuffer.Clear();
        _vad.Reset();
        SetState(SessionState.Cancelled, reason);

        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            lock (_stateLock)
            {
                if (_currentState == SessionState.Cancelled)
                {
                    SetState(SessionState.Idle);
                }
            }
        });

        return Task.CompletedTask;
    }
}
