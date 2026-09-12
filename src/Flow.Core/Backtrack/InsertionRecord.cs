using System;
using Flow.Core.TextInsertion;

namespace Flow.Core.Backtrack;

/// <summary>
/// Immutable record of a text insertion event within a dictation session.
/// Retains window handle and process identity to enforce strict ownership verification before backtrack.
/// </summary>
public sealed record InsertionRecord(
    Guid Id,
    string InsertedText,
    int CharacterCount,
    DateTimeOffset Timestamp,
    IntPtr TargetHwnd,
    string TargetProcessName,
    uint TargetProcessId,
    InsertionStrategy StrategyUsed
);
