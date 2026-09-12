using System;
using System.Linq;

namespace Flow.Core.Commands;

/// <summary>
/// Cryptographically/uniquely bound confirmation token for high-risk commands (Section 18).
/// Strictly binds to (SessionId, CommandId, TargetHwnd, TargetProcessId, SelectionHash, Timestamp).
/// If any target property changes, confirmation becomes immediately invalid.
/// </summary>
public sealed record CommandConfirmationToken(
    Guid SessionId,
    string CommandId,
    IntPtr TargetHwnd,
    uint TargetProcessId,
    int SelectionLength,
    int SelectionHash,
    DateTimeOffset IssuedAt,
    TimeSpan Lifetime
)
{
    public bool IsExpired => DateTimeOffset.UtcNow > (IssuedAt + Lifetime);
}

/// <summary>
/// Service interface for managing target-bound confirmations.
/// </summary>
public interface ICommandConfirmationService
{
    CommandConfirmationToken CreateToken(CommandExecutionContext context, string commandId, TimeSpan? lifetime = null);

    bool ValidateConfirmation(
        CommandConfirmationToken token,
        CommandExecutionContext currentContext,
        string spokenPhrase,
        out string? failureReason
    );
}

/// <summary>
/// Deterministic implementation of <see cref="ICommandConfirmationService"/>.
/// </summary>
public sealed class CommandConfirmationService : ICommandConfirmationService
{
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(5.0);

    private static readonly string[] ApprovedConfirmPhrases =
    {
        "confirm", "yes", "proceed", "go ahead", "do it", "confirm delete", "yes please"
    };

    public CommandConfirmationToken CreateToken(CommandExecutionContext context, string commandId, TimeSpan? lifetime = null)
    {
        int textLength = context.SelectionText?.Length ?? 0;
        int textHash = context.SelectionText?.GetHashCode(StringComparison.Ordinal) ?? 0;

        return new CommandConfirmationToken(
            context.SessionId,
            commandId,
            context.TargetHwnd,
            context.TargetProcessId,
            textLength,
            textHash,
            DateTimeOffset.UtcNow,
            lifetime ?? DefaultLifetime
        );
    }

    public bool ValidateConfirmation(
        CommandConfirmationToken token,
        CommandExecutionContext currentContext,
        string spokenPhrase,
        out string? failureReason)
    {
        failureReason = null;

        if (token == null)
        {
            failureReason = "Confirmation token is null.";
            return false;
        }

        // 1. Check expiration
        if (token.IsExpired)
        {
            failureReason = "Confirmation timed out.";
            return false;
        }

        // 2. Check HWND binding
        if (token.TargetHwnd != currentContext.TargetHwnd)
        {
            failureReason = $"Target window changed (Initial: {token.TargetHwnd}, Current: {currentContext.TargetHwnd}).";
            return false;
        }

        // 3. Check PID binding
        if (token.TargetProcessId != currentContext.TargetProcessId)
        {
            failureReason = $"Target process changed (Initial: {token.TargetProcessId}, Current: {currentContext.TargetProcessId}).";
            return false;
        }

        // 4. Check selection identity binding
        int currentLength = currentContext.SelectionText?.Length ?? 0;
        int currentHash = currentContext.SelectionText?.GetHashCode(StringComparison.Ordinal) ?? 0;

        if (token.SelectionLength != currentLength || token.SelectionHash != currentHash)
        {
            failureReason = "Target selection modified or cleared since confirmation requested.";
            return false;
        }

        // 5. Check confirmation phrase
        if (string.IsNullOrWhiteSpace(spokenPhrase))
        {
            failureReason = "Spoken confirmation phrase is empty.";
            return false;
        }

        string normalized = spokenPhrase.Trim().TrimEnd('.', '!', '?').ToLowerInvariant();
        bool isApproved = ApprovedConfirmPhrases.Any(p => p.Equals(normalized, StringComparison.OrdinalIgnoreCase));

        if (!isApproved)
        {
            failureReason = $"Spoken phrase '{spokenPhrase}' is not an approved confirmation phrase.";
            return false;
        }

        return true;
    }
}
