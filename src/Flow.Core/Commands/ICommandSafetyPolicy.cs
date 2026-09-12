namespace Flow.Core.Commands;

/// <summary>
/// Result of evaluating command safety against security policies.
/// </summary>
public sealed record CommandSafetyResult(
    CommandSafetyVerdict Verdict,
    string Reason,
    string? ProhibitedToken = null
);

/// <summary>
/// Safety evaluation policy for voice commands.
/// Strictly enforces the Zero-Destructive Execution Invariant (WF-038).
/// Prohibits shell execution, process spawning, and irreversible system commands.
/// </summary>
public interface ICommandSafetyPolicy
{
    /// <summary>
    /// Evaluates whether a raw command transcript is safe to process.
    /// </summary>
    CommandSafetyResult EvaluateTranscript(string transcript);

    /// <summary>
    /// Evaluates whether a parsed command intent is safe to execute.
    /// </summary>
    CommandSafetyResult EvaluateIntent(CommandIntent intent);
}
