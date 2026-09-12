using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Backtrack;

namespace Flow.Core.TextInsertion;

/// <summary>
/// Strategy used to insert text into the active cursor position.
/// </summary>
public enum InsertionStrategy
{
    None,
    UiaDirect,
    SendInputClipboardFallback
}

/// <summary>
/// Result of a text insertion attempt.
/// </summary>
public sealed record InsertionResult(
    bool Success,
    InsertionStrategy StrategyUsed,
    string? TargetApplicationName,
    TimeSpan Latency,
    string? ErrorMessage = null,
    IntPtr TargetHwnd = default,
    uint TargetProcessId = 0,
    int InsertedLength = 0
)
{
    public static InsertionResult Failed(string error, TimeSpan latency, string? appName = null) =>
        new(false, InsertionStrategy.None, appName, latency, error);
}

/// <summary>
/// Service interface for inserting text at the active Windows cursor position.
/// </summary>
public interface ITextInsertionService
{
    /// <summary>
    /// Inserts sanitized text at the current cursor position.
    /// Inviolable invariant: MUST NEVER simulate Enter (VK_RETURN) or click submit.
    /// </summary>
    Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Safely backtracks (deletes) previously inserted text after verifying foreground window ownership.
    /// Inviolable invariant: MUST NEVER simulate Enter (VK_RETURN) or modify wrong target application.
    /// </summary>
    Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default);
}
