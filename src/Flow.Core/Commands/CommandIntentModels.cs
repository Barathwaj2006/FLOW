using System;
using Flow.Core.Context;

namespace Flow.Core.Commands;

/// <summary>
/// Granular classification of recognized voice command intents.
/// </summary>
public enum CommandIntentType
{
    Unknown,
    FocusApplication,
    OpenApplication,
    OpenUrl,
    OpenFile,
    OpenFolder,
    SwitchWindow,
    Copy,
    Paste,
    Undo,
    Redo,
    SelectAll,
    Deselect,
    DeleteSelection,
    TransformSelection,
    FormatSelection,
    EditorAction,
    CancelCommand
}

/// <summary>
/// Deterministic risk tier assigned to every command intent.
/// </summary>
public enum CommandRisk
{
    /// <summary>Non-destructive, in-editor reversible action or safe inspection.</summary>
    Safe,

    /// <summary>Low risk: activating allowlisted applications or safe web links.</summary>
    LowRisk,

    /// <summary>Potentially destructive or large-scope modification; requires explicit user confirmation.</summary>
    ConfirmRequired,

    /// <summary>Permanently blocked: shell execution, process killing, system shutdown, registry manipulation.</summary>
    Blocked
}

/// <summary>
/// Fine-grained permission flags required to execute a command.
/// Normal dictation is granted zero permissions.
/// </summary>
[Flags]
public enum CommandPermission
{
    None = 0,
    TextReadSelection = 1 << 0,
    TextModifySelection = 1 << 1,
    ClipboardAccess = 1 << 2,
    OpenApplication = 1 << 3,
    OpenUrl = 1 << 4,
    WindowNavigation = 1 << 5
}

/// <summary>
/// Immutable target execution context captured when a command is evaluated.
/// Bound strictly to foreground HWND, PID, and focused control to prevent cross-window execution.
/// </summary>
public sealed record CommandExecutionContext(
    Guid SessionId,
    DateTimeOffset Timestamp,
    IntPtr TargetHwnd,
    uint TargetProcessId,
    string TargetApplication,
    FocusedControlInfo FocusedControl,
    string? SelectionText,
    ApplicationCategory Category,
    string? NearbyText = null
)
{
    public static CommandExecutionContext FromSnapshot(ContextSnapshot snapshot) => new(
        snapshot.SessionId,
        snapshot.Timestamp,
        snapshot.TargetInfo.Hwnd,
        snapshot.TargetInfo.ProcessId,
        snapshot.TargetInfo.ProcessName,
        snapshot.FocusedControl,
        snapshot.SelectionText,
        snapshot.Category,
        snapshot.NearbyText
    );

    public static CommandExecutionContext Empty => new(
        Guid.Empty,
        DateTimeOffset.UtcNow,
        IntPtr.Zero,
        0,
        "Unknown",
        FocusedControlInfo.Empty,
        null,
        ApplicationCategory.Unknown
    );
}

/// <summary>
/// Status codes returned by command policy evaluations and execution attempts.
/// </summary>
public enum CommandResultStatus
{
    Success,
    Rejected,
    Blocked,
    Cancelled,
    ConfirmationRequired,
    TargetLost,
    Unsupported,
    Failed
}

/// <summary>
/// Structured result of a command execution or policy decision.
/// </summary>
public sealed record CommandResult(
    CommandResultStatus Status,
    string CommandId,
    CommandIntentType IntentType,
    string? Message = null,
    TimeSpan Duration = default,
    DateTimeOffset Timestamp = default,
    string? TargetApplication = null
)
{
    public static CommandResult CreateSuccess(string commandId, CommandIntentType type, string? msg = null, TimeSpan duration = default) =>
        new(CommandResultStatus.Success, commandId, type, msg, duration, DateTimeOffset.UtcNow);

    public static CommandResult CreateBlocked(string commandId, CommandIntentType type, string reason) =>
        new(CommandResultStatus.Blocked, commandId, type, reason, TimeSpan.Zero, DateTimeOffset.UtcNow);

    public static CommandResult CreateRejected(string commandId, CommandIntentType type, string reason) =>
        new(CommandResultStatus.Rejected, commandId, type, reason, TimeSpan.Zero, DateTimeOffset.UtcNow);

    public static CommandResult CreateTargetLost(string commandId, CommandIntentType type, string reason = "Target window focus changed") =>
        new(CommandResultStatus.TargetLost, commandId, type, reason, TimeSpan.Zero, DateTimeOffset.UtcNow);

    public static CommandResult CreateConfirmationRequired(string commandId, CommandIntentType type, string prompt) =>
        new(CommandResultStatus.ConfirmationRequired, commandId, type, prompt, TimeSpan.Zero, DateTimeOffset.UtcNow);

    public static CommandResult CreateCancelled(string commandId, CommandIntentType type, string reason = "Cancelled by user") =>
        new(CommandResultStatus.Cancelled, commandId, type, reason, TimeSpan.Zero, DateTimeOffset.UtcNow);
}

/// <summary>
/// Command intent to launch or focus an allowlisted application.
/// </summary>
public sealed record ApplicationCommandIntent(
    string RawTranscript,
    string ApplicationName,
    CommandRisk Risk = CommandRisk.LowRisk,
    float Confidence = 1.0f
) : CommandIntent(RawTranscript, CommandType.EditorAction, Confidence);

/// <summary>
/// Command intent to open a validated web URL (https:// or http:// only).
/// </summary>
public sealed record UrlCommandIntent(
    string RawTranscript,
    Uri ValidatedUrl,
    CommandRisk Risk = CommandRisk.LowRisk,
    float Confidence = 1.0f
) : CommandIntent(RawTranscript, CommandType.EditorAction, Confidence);

/// <summary>
/// Command intent to delete or remove currently selected text (requires explicit confirmation).
/// </summary>
public sealed record DeleteSelectionIntent(
    string RawTranscript,
    string TargetDescription = "selected text",
    float Confidence = 1.0f
) : CommandIntent(RawTranscript, CommandType.EditorAction, Confidence);

/// <summary>
/// Command intent to cancel the current session or dismiss prompt.
/// </summary>
public sealed record CancelCommandIntent(
    string RawTranscript,
    string Reason = "User spoken cancel",
    float Confidence = 1.0f
) : CommandIntent(RawTranscript, CommandType.EditorAction, Confidence);
