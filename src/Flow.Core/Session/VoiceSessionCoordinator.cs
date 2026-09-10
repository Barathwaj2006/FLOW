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
/// Hotkey Press / Double-Tap -> WASAPI Audio Stream -> Ring Buffer -> VAD -> Local ASR -> Language Sanitizer -> Safe Insertion.
/// Enforces the desktop 20-minute continuous recording ceiling with 19-minute warning.
/// </summary>
public sealed class VoiceSessionCoordinator
{
    private readonly AudioRingBuffer _ringBuffer;
    private readonly IVoiceActivityDetector _vad;
    private readonly ASREngineRegistry _asrRegistry;
    private readonly ILanguageEngine _languageEngine;
    private readonly ITextInsertionService _insertionService;
    private readonly ILogger<VoiceSessionCoordinator>? _logger;

    private readonly double _maxRecordingDurationSeconds;
    private readonly double _warningDurationSeconds;

    private readonly object _stateLock = new();
    private SessionState _currentState = SessionState.Idle;
    private CancellationTokenSource? _sessionCts;
    private bool _hasDetectedSpeechInSession;
    private long _sessionStartTimestamp;
    private bool _warningFired;
    private bool _limitExceededFired;
    private bool _isHandsFree;

    public SessionState CurrentState
    {
        get
        {
            lock (_stateLock) return _currentState;
        }
    }

    public bool IsHandsFree
    {
        get
        {
            lock (_stateLock) return _isHandsFree;
        }
    }

    public event Action<SessionState, string?>? StateChanged;
    public event Action<float>? AudioLevelChanged;
    public event Action<string>? PartialTranscriptReceived;
    public event Action<string>? FinalTextInserted;
    public event Action<string>? SessionWarning;

    public VoiceSessionCoordinator(
        AudioRingBuffer ringBuffer,
        IVoiceActivityDetector vad,
        ASREngineRegistry asrRegistry,
        ILanguageEngine languageEngine,
        ITextInsertionService insertionService,
        ILogger<VoiceSessionCoordinator>? logger = null,
        double maxRecordingSeconds = 1200.0, // 20 minutes
        double warningThresholdSeconds = 1140.0) // 19 minutes
    {
        _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
        _vad = vad ?? throw new ArgumentNullException(nameof(vad));
        _asrRegistry = asrRegistry ?? throw new ArgumentNullException(nameof(asrRegistry));
        _languageEngine = languageEngine ?? throw new ArgumentNullException(nameof(languageEngine));
        _insertionService = insertionService ?? throw new ArgumentNullException(nameof(insertionService));
        _logger = logger;
        _maxRecordingDurationSeconds = maxRecordingSeconds;
        _warningDurationSeconds = warningThresholdSeconds;
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
    /// Starts a new recording session.
    /// </summary>
    /// <param name="isHandsFree">True if triggered via hands-free double-tap.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StartSessionAsync(bool isHandsFree = false, CancellationToken cancellationToken = default)
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
            _sessionStartTimestamp = Stopwatch.GetTimestamp();
            _warningFired = false;
            _limitExceededFired = false;
            _isHandsFree = isHandsFree;

            string detail = isHandsFree ? "Hands-Free Listening" : "Listening";
            SetState(SessionState.Recording, detail);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Feeds incoming real-time audio chunk from WASAPI capture stream.
    /// Monitors recording duration against the 20-minute desktop ceiling.
    /// </summary>
    public void ProcessAudioChunk(ReadOnlySpan<float> samples)
    {
        if (CurrentState != SessionState.Recording || samples.IsEmpty)
        {
            return;
        }

        // 1. Duration & limit monitoring
        long now = Stopwatch.GetTimestamp();
        double elapsedSeconds = (double)(now - _sessionStartTimestamp) / Stopwatch.Frequency;

        if (elapsedSeconds >= _warningDurationSeconds && !_warningFired)
        {
            _warningFired = true;
            _logger?.LogWarning("Approaching 20-minute recording limit ({ElapsedSeconds:F1}s elapsed).", elapsedSeconds);
            SessionWarning?.Invoke("Approaching 20-minute limit (1 minute remaining)");
        }

        if (elapsedSeconds >= _maxRecordingDurationSeconds && !_limitExceededFired)
        {
            _limitExceededFired = true;
            _logger?.LogWarning("20-minute recording limit reached. Automatically concluding session.");
            _ = Task.Run(async () => await EndSessionAsync());
            return;
        }

        // 2. Process VAD
        var vadResult = _vad.ProcessChunk(samples);
        if (vadResult.IsSpeech)
        {
            _hasDetectedSpeechInSession = true;
        }

        // 3. Report RMS audio energy level for HUD animation
        AudioLevelChanged?.Invoke(vadResult.RmsEnergy);

        // 4. Store into ring buffer
        _ringBuffer.Write(samples);
    }

    /// <summary>
    /// Ends the current recording session and executes the transcription/insertion pipeline.
    /// </summary>
    public async Task<bool> EndSessionAsync(CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_currentState != SessionState.Recording)
            {
                return false;
            }
            _isHandsFree = false;
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

            // 1. Transcribe via ASR engine registry (executing real local Whisper backend)
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
        lock (_stateLock)
        {
            _isHandsFree = false;
        }

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
