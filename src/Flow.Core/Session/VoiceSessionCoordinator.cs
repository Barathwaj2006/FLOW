using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Commands;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.TextInsertion;
using Microsoft.Extensions.Logging;

namespace Flow.Core.Session;

/// <summary>
/// Operational mode of the voice session.
/// </summary>
public enum SessionMode
{
    /// <summary>Standard voice dictation: text-only insertion with zero execution risk.</summary>
    Dictation,

    /// <summary>Dedicated voice command mode: selection-aware transforms and safe editor actions.</summary>
    Command
}

/// <summary>
/// Central coordinator managing the end-to-end voice dictation session lifecycle:
/// Hotkey Press / Double-Tap -> WASAPI Audio Stream -> Ring Buffer -> VAD -> Local ASR -> Language Sanitizer -> Safe Insertion.
/// Enforces the desktop 20-minute continuous recording ceiling with 19-minute warning.
/// Supports both standard inert text dictation and dedicated Safe Voice Command Mode (WF-036, WF-037A, WF-038).
/// </summary>
public sealed class VoiceSessionCoordinator
{
    private readonly AudioRingBuffer _ringBuffer;
    private readonly IVoiceActivityDetector _vad;
    private readonly ASREngineRegistry _asrRegistry;
    private readonly ILanguageEngine _languageEngine;
    private readonly ITextInsertionService _insertionService;
    private readonly IUIContextService _contextService;
    private readonly InsertionHistoryTracker _historyTracker;
    private readonly ICommandParser _commandParser;
    private readonly ICommandSafetyPolicy _safetyPolicy;
    private readonly ITextTransformEngine _transformEngine;
    private readonly ILogger<VoiceSessionCoordinator>? _logger;

    private readonly double _maxRecordingDurationSeconds;
    private readonly double _warningDurationSeconds;

    private readonly object _stateLock = new();
    private SessionState _currentState = SessionState.Idle;
    private SessionMode _sessionMode = SessionMode.Dictation;
    private string? _activeSelectedText;
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

    public SessionMode CurrentMode
    {
        get
        {
            lock (_stateLock) return _sessionMode;
        }
    }

    public bool IsHandsFree
    {
        get
        {
            lock (_stateLock) return _isHandsFree;
        }
    }

    public InsertionHistoryTracker HistoryTracker => _historyTracker;

    /// <summary>
    /// Active text selection extracted when Command Mode was engaged (WF-037A).
    /// </summary>
    public string? ActiveSelectedText
    {
        get
        {
            lock (_stateLock) return _activeSelectedText;
        }
    }

    /// <summary>
    /// Bounded surrounding text context extracted before recording started (WF-031A).
    /// Up to 200 characters preceding the caret in the active editable control.
    /// </summary>
    public string? ActiveNearbyContext { get; private set; }

    /// <summary>
    /// Selected transcription language (WF-021, WF-022).
    /// Default is "auto" for Whisper automatic language detection; or explicit code like "en", "ta".
    /// </summary>
    public string SelectedLanguage { get; set; } = "auto";

    /// <summary>
    /// Optional vocabulary adaptation biasing prompt for code-switching and bilingual audio (WF-023).
    /// </summary>
    public string? CodeSwitchingBiasingPrompt { get; set; }

    public event Action<SessionState, string?>? StateChanged;
    public event Action<float>? AudioLevelChanged;
    public event Action<string>? PartialTranscriptReceived;
    public event Action<string>? FinalTextInserted;
    public event Action<string>? SessionWarning;
    public event Action<CommandIntent, CommandSafetyResult>? CommandProcessed;

    public VoiceSessionCoordinator(
        AudioRingBuffer ringBuffer,
        IVoiceActivityDetector vad,
        ASREngineRegistry asrRegistry,
        ILanguageEngine languageEngine,
        ITextInsertionService insertionService,
        ILogger<VoiceSessionCoordinator>? logger = null,
        double maxRecordingSeconds = 1200.0, // 20 minutes
        double warningThresholdSeconds = 1140.0, // 19 minutes
        InsertionHistoryTracker? historyTracker = null,
        IUIContextService? contextService = null,
        ICommandParser? commandParser = null,
        ICommandSafetyPolicy? safetyPolicy = null,
        ITextTransformEngine? transformEngine = null)
    {
        _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
        _vad = vad ?? throw new ArgumentNullException(nameof(vad));
        _asrRegistry = asrRegistry ?? throw new ArgumentNullException(nameof(asrRegistry));
        _languageEngine = languageEngine ?? throw new ArgumentNullException(nameof(languageEngine));
        _insertionService = insertionService ?? throw new ArgumentNullException(nameof(insertionService));
        _contextService = contextService ?? new NullUIContextService();
        _historyTracker = historyTracker ?? new InsertionHistoryTracker();
        _commandParser = commandParser ?? new DeterministicCommandParser();
        _safetyPolicy = safetyPolicy ?? new DeterministicCommandSafetyPolicy();
        _transformEngine = transformEngine ?? new DeterministicTextTransformEngine();
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

            // WF-030: Inviolable Password Field Exclusion Check
            if (_contextService.IsFocusInPasswordField())
            {
                _logger?.LogWarning("StartSessionAsync blocked: Focused UI element is a password or credential field.");
                SetState(SessionState.Cancelled, "Password field detected — recording blocked");
                SessionWarning?.Invoke("Password field detected. Voice recording is disabled for your protection.");
                return Task.CompletedTask;
            }

            // WF-031A: Bounded nearby context extraction (pre-session capture, max 200 chars)
            ActiveNearbyContext = _contextService.GetNearbyContext(200);

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ringBuffer.Clear();
            _vad.Reset();
            _hasDetectedSpeechInSession = false;
            _sessionStartTimestamp = Stopwatch.GetTimestamp();
            _warningFired = false;
            _limitExceededFired = false;
            _sessionMode = SessionMode.Dictation;
            _activeSelectedText = null;
            _isHandsFree = isHandsFree;

            string detail = isHandsFree ? "Hands-Free Listening" : "Listening";
            SetState(SessionState.Recording, detail);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts a dedicated command mode recording session (WF-036).
    /// Extracts active text selection for voice transformation (WF-037A).
    /// Inviolable rule: Refuses to start if focus is in a password field (WF-030).
    /// </summary>
    /// <param name="isHandsFree">True if triggered in hands-free mode.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task StartCommandSessionAsync(bool isHandsFree = false, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_currentState == SessionState.Recording)
            {
                _logger?.LogWarning("StartCommandSessionAsync called while already recording. Ignoring.");
                return Task.CompletedTask;
            }

            // WF-030: Inviolable Password Field Exclusion Check
            if (_contextService.IsFocusInPasswordField())
            {
                _logger?.LogWarning("StartCommandSessionAsync blocked: Focused UI element is a password or credential field.");
                SetState(SessionState.Cancelled, "Password field detected — command mode blocked");
                SessionWarning?.Invoke("Password field detected. Command mode is disabled for your protection.");
                return Task.CompletedTask;
            }

            // WF-037A: Query active text selection
            _activeSelectedText = _contextService.GetSelectedText(10000);
            ActiveNearbyContext = _contextService.GetNearbyContext(200);

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _ringBuffer.Clear();
            _vad.Reset();
            _hasDetectedSpeechInSession = false;
            _sessionStartTimestamp = Stopwatch.GetTimestamp();
            _warningFired = false;
            _limitExceededFired = false;
            _sessionMode = SessionMode.Command;
            _isHandsFree = isHandsFree;

            string detail = isHandsFree ? "🪄 Command: Hands-Free" : "🪄 Command: Listening";
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
            var asrOptions = new ASROptions(
                Language: SelectedLanguage,
                Prompt: CodeSwitchingBiasingPrompt
            );
            var asrResult = await _asrRegistry.TranscribeWithFallbackAsync(audioBuffer, options: asrOptions, progress: progress, cancellationToken: ct);

            if (string.IsNullOrWhiteSpace(asrResult.Text))
            {
                SetState(SessionState.Cancelled, "Empty transcription");
                return false;
            }

            // Command Mode Processing Branch (WF-036, WF-037A, WF-038)
            if (_sessionMode == SessionMode.Command)
            {
                // WF-038: Zero-Destructive Command Safety Check
                var safetyResult = _safetyPolicy.EvaluateTranscript(asrResult.Text);
                if (safetyResult.Verdict == CommandSafetyVerdict.Blocked)
                {
                    _logger?.LogWarning("Command execution blocked by safety policy: {Reason} (Token: {Token})", safetyResult.Reason, safetyResult.ProhibitedToken);
                    SetState(SessionState.Cancelled, $"Blocked: {safetyResult.Reason}");
                    SessionWarning?.Invoke($"Command blocked: {safetyResult.Reason}");
                    return false;
                }

                // Parse command into typed intent
                var intent = _commandParser.Parse(asrResult.Text);
                var intentSafety = _safetyPolicy.EvaluateIntent(intent);

                if (intentSafety.Verdict == CommandSafetyVerdict.Blocked)
                {
                    _logger?.LogWarning("Command intent blocked by safety policy: {Reason}", intentSafety.Reason);
                    SetState(SessionState.Cancelled, $"Blocked: {intentSafety.Reason}");
                    SessionWarning?.Invoke($"Command blocked: {intentSafety.Reason}");
                    return false;
                }

                if (intentSafety.Verdict == CommandSafetyVerdict.Unknown || intent is UnknownCommandIntent)
                {
                    _logger?.LogInformation("Command not recognized: {Transcript}. Failing closed.", asrResult.Text);
                    SetState(SessionState.Cancelled, $"Unknown: {asrResult.Text}");
                    SessionWarning?.Invoke($"Unrecognized command: \"{asrResult.Text}\"");
                    return false;
                }

                // Execute validated command
                if (intent is TransformCommandIntent transformIntent)
                {
                    if (string.IsNullOrWhiteSpace(_activeSelectedText))
                    {
                        _logger?.LogInformation("Transform command requested but no text was selected.");
                        SetState(SessionState.Completed, "No text selected");
                        SessionWarning?.Invoke("No text selected to transform. Highlight text first.");
                        return true;
                    }

                    string transformed = _transformEngine.Transform(_activeSelectedText, transformIntent.Transform);

                    // WF-030: Secondary defense-in-depth password check before insertion
                    if (_contextService.IsFocusInPasswordField())
                    {
                        _logger?.LogWarning("EndSessionAsync insertion blocked: Focused UI element is a password or credential field.");
                        SetState(SessionState.Cancelled, "Password field detected — insertion blocked");
                        return false;
                    }

                    SetState(SessionState.Inserting, $"Transform: {transformIntent.Transform}");

                    var transformResult = await _insertionService.InsertTextAsync(transformed, ct);
                    if (transformResult.Success)
                    {
                        var record = new InsertionRecord(
                            Guid.NewGuid(),
                            transformed,
                            transformResult.InsertedLength > 0 ? transformResult.InsertedLength : transformed.Length,
                            DateTimeOffset.UtcNow,
                            transformResult.TargetHwnd,
                            transformResult.TargetApplicationName ?? "Unknown",
                            transformResult.TargetProcessId,
                            transformResult.StrategyUsed
                        );
                        _historyTracker.RecordInsertion(record);

                        FinalTextInserted?.Invoke(transformed);
                        CommandProcessed?.Invoke(transformIntent, intentSafety);
                        SetState(SessionState.Completed, $"Transformed: {transformIntent.Transform}");
                        return true;
                    }
                    else
                    {
                        SetState(SessionState.Error, transformResult.ErrorMessage ?? "Transform insertion failed");
                        return false;
                    }
                }
                else if (intent is EditorCommandIntent editorIntent)
                {
                    if (editorIntent.ActionName == "undo")
                    {
                        return await BacktrackAsync(ct);
                    }

                    CommandProcessed?.Invoke(editorIntent, intentSafety);
                    SetState(SessionState.Completed, $"Executed: {editorIntent.ActionName}");
                    return true;
                }
            }

            // Standard Voice Dictation Processing Branch (Permanently inert text-only)
            // 2. Deterministic sanitization & Zero-Enter guarantee
            string cleanText = _languageEngine.Format(asrResult.Text);

            if (string.IsNullOrWhiteSpace(cleanText))
            {
                SetState(SessionState.Cancelled, "Cleaned text empty");
                return false;
            }

            // WF-030: Secondary defense-in-depth password check before insertion
            if (_contextService.IsFocusInPasswordField())
            {
                _logger?.LogWarning("EndSessionAsync insertion blocked: Focused UI element is a password or credential field.");
                SetState(SessionState.Cancelled, "Password field detected — insertion blocked");
                return false;
            }

            SetState(SessionState.Inserting, "Inserting");

            // 3. Safe cursor text insertion
            var insertionResult = await _insertionService.InsertTextAsync(cleanText, ct);

            if (insertionResult.Success)
            {
                var record = new InsertionRecord(
                    Guid.NewGuid(),
                    cleanText,
                    insertionResult.InsertedLength > 0 ? insertionResult.InsertedLength : cleanText.Length,
                    DateTimeOffset.UtcNow,
                    insertionResult.TargetHwnd,
                    insertionResult.TargetApplicationName ?? "Unknown",
                    insertionResult.TargetProcessId,
                    insertionResult.StrategyUsed
                );
                _historyTracker.RecordInsertion(record);

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
                        _sessionMode = SessionMode.Dictation;
                        _activeSelectedText = null;
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
            _sessionMode = SessionMode.Dictation;
            _activeSelectedText = null;
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

    /// <summary>
    /// Safely backtracks the most recent text insertion in the current session if the target window is still focused.
    /// Inviolable invariant: Zero destructive deletion if target window changed, zero Enter simulated.
    /// </summary>
    public async Task<bool> BacktrackAsync(CancellationToken cancellationToken = default)
    {
        var lastRecord = _historyTracker.PeekLastInsertion();
        if (lastRecord == null)
        {
            _logger?.LogInformation("Backtrack requested but no insertion history is available.");
            return false;
        }

        SetState(SessionState.Backtracking, "Backtracking");
        try
        {
            bool success = await _insertionService.BacktrackAsync(lastRecord, cancellationToken);
            if (success)
            {
                _historyTracker.PopLastInsertion();
                _logger?.LogInformation("Successfully backtracked {Count} characters.", lastRecord.CharacterCount);
                SetState(SessionState.Completed, "Backtracked");
                return true;
            }
            else
            {
                _logger?.LogWarning("Backtrack was aborted or failed.");
                SetState(SessionState.Idle);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Backtrack operation threw an unhandled exception.");
            SetState(SessionState.Error, ex.Message);
            return false;
        }
        finally
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                lock (_stateLock)
                {
                    if (_currentState is SessionState.Completed or SessionState.Error)
                    {
                        SetState(SessionState.Idle);
                    }
                }
            });
        }
    }
}
