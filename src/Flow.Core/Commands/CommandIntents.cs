using System;

namespace Flow.Core.Commands;

/// <summary>
/// Abstract base class for all parsed voice command intents.
/// </summary>
public abstract record CommandIntent(
    string RawTranscript,
    CommandType Type,
    float Confidence = 1.0f
);

/// <summary>
/// Command intent to perform a text transformation on currently selected text.
/// </summary>
public sealed record TransformCommandIntent(
    string RawTranscript,
    TransformType Transform,
    string? TargetPhrase = null,
    float Confidence = 1.0f
) : CommandIntent(RawTranscript, CommandType.TransformSelection, Confidence);

/// <summary>
/// Command intent to perform a safe, reversible in-editor action (e.g., undo, copy, select all).
/// </summary>
public sealed record EditorCommandIntent(
    string RawTranscript,
    string ActionName,
    float Confidence = 1.0f
) : CommandIntent(RawTranscript, CommandType.EditorAction, Confidence);

/// <summary>
/// Unrecognized or ambiguous command intent. Fails closed.
/// </summary>
public sealed record UnknownCommandIntent(
    string RawTranscript,
    string FailureReason,
    float Confidence = 0.0f
) : CommandIntent(RawTranscript, CommandType.Unknown, Confidence);
