namespace Flow.Core.Session;

/// <summary>
/// States for the FLOW voice dictation session lifecycle.
/// </summary>
public enum SessionState
{
    /// <summary>Ready to receive hotkey activation.</summary>
    Idle,

    /// <summary>Push-to-talk hotkey is held down; microphone is actively capturing audio.</summary>
    Recording,

    /// <summary>Audio is being transcribed by local ASR and formatted by language engine.</summary>
    Processing,

    /// <summary>Sanitized text is being inserted at cursor position.</summary>
    Inserting,

    /// <summary>Previous insertion is being safely backtracked / undone.</summary>
    Backtracking,

    /// <summary>Session finished successfully.</summary>
    Completed,

    /// <summary>Session was cancelled or rejected due to silence.</summary>
    Cancelled,

    /// <summary>Session failed due to an error.</summary>
    Error
}
