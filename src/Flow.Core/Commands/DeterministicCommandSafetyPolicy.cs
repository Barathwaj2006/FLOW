using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Flow.Core.Commands;

/// <summary>
/// Deterministic implementation of <see cref="ICommandSafetyPolicy"/>.
/// Enforces inviolable system execution safeguards (WF-038):
/// 1. Prohibits shell execution, process launching, and OS command interpreters.
/// 2. Prohibits destructive filesystem actions (file deletion, drive formatting).
/// 3. Prohibits system shutdown, reboot, or process killing.
/// 4. Distinguishes benign text formatting terms (e.g. "format as bullets") from destructive system calls (e.g. "format C:").
/// </summary>
public sealed class DeterministicCommandSafetyPolicy : ICommandSafetyPolicy
{
    private static readonly List<(Regex Pattern, string Description)> BlockedPatterns = new()
    {
        // System control & power operations
        (new Regex(@"\bshutdown\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System shutdown commands are strictly blocked."),
        (new Regex(@"\breboot\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System reboot commands are strictly blocked."),
        (new Regex(@"\b(restart|restart-computer)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System restart commands are strictly blocked."),
        (new Regex(@"\bpoweroff\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System poweroff commands are strictly blocked."),
        (new Regex(@"\blogoff\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System logoff commands are strictly blocked."),

        // Shell interpreters & CLI processes
        (new Regex(@"\bcmd(\.exe)?\s*(/[ck])?", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Command Prompt shell execution is strictly blocked."),
        (new Regex(@"\bpowershell(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PowerShell process execution is strictly blocked."),
        (new Regex(@"\bpwsh(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PowerShell Core process execution is strictly blocked."),
        (new Regex(@"\bbash(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Bash shell execution is strictly blocked."),
        (new Regex(@"\bsh\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Unix shell execution is strictly blocked."),
        (new Regex(@"\bwscript(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Windows Script Host execution is strictly blocked."),
        (new Regex(@"\bcscript(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Console Script Host execution is strictly blocked."),
        (new Regex(@"\bcurl\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Curl network download execution is strictly blocked."),
        (new Regex(@"\bwget\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Wget network download execution is strictly blocked."),

        // Windows execution primitives & API calls
        (new Regex(@"\bcreateprocess\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Direct process creation primitives are strictly blocked."),
        (new Regex(@"\bshellexecute(ex)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "ShellExecute primitives are strictly blocked."),
        (new Regex(@"\bprocess\.start\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process.Start execution is strictly blocked."),

        // Filesystem destruction & mass deletion
        (new Regex(@"\brm\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Filesystem removal commands are strictly blocked."),
        (new Regex(@"\bdel\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "File deletion commands are strictly blocked."),
        (new Regex(@"\berase\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "File erase commands are strictly blocked."),
        (new Regex(@"\bremove-item\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PowerShell Remove-Item commands are strictly blocked."),
        (new Regex(@"\brmdir\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Directory removal commands are strictly blocked."),
        (new Regex(@"\brd\s+/[sqSQ]", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Directory tree removal commands are strictly blocked."),
        (new Regex(@"\bformat(?!\s+(as\s+)?(bullets?|bullet\s+list|numbered|a\s+numbered))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Drive format commands are strictly blocked."),
        (new Regex(@"\bdiskpart\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Disk partitioning commands are strictly blocked."),

        // Registry manipulation
        (new Regex(@"\bregistry\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Registry operations are strictly blocked."),
        (new Regex(@"\breg\s+(add|delete|query|import|export)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Reg CLI operations are strictly blocked."),

        // Process termination & task killing
        (new Regex(@"\btaskkill\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process termination commands are strictly blocked."),
        (new Regex(@"\bstop-process\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process termination commands are strictly blocked."),
        (new Regex(@"\bkill\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process kill commands are strictly blocked."),

        // Database destruction
        (new Regex(@"\bdrop\s+(table|database|schema)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Database drop commands are strictly blocked."),
        (new Regex(@"\btruncate\s+table\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Database truncate commands are strictly blocked."),

        // Security elevation & privilege escalation
        (new Regex(@"\bsudo\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Privilege escalation commands are strictly blocked."),
        (new Regex(@"\brunas\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "RunAs elevation commands are strictly blocked."),
        (new Regex(@"\bset-executionpolicy\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Execution policy modification is strictly blocked.")
    };

    /// <inheritdoc />
    public CommandSafetyResult EvaluateTranscript(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return new CommandSafetyResult(CommandSafetyVerdict.Unknown, "Empty transcript provided.");
        }

        string normalized = transcript.Trim();

        foreach (var (pattern, description) in BlockedPatterns)
        {
            var match = pattern.Match(normalized);
            if (match.Success)
            {
                return new CommandSafetyResult(
                    CommandSafetyVerdict.Blocked,
                    description,
                    match.Value
                );
            }
        }

        return new CommandSafetyResult(CommandSafetyVerdict.Safe, "Transcript passes all safety policies.");
    }

    /// <inheritdoc />
    public CommandSafetyResult EvaluateIntent(CommandIntent intent)
    {
        if (intent == null)
        {
            return new CommandSafetyResult(CommandSafetyVerdict.Unknown, "Null intent provided.");
        }

        // First evaluate the raw transcript
        var transcriptVerdict = EvaluateTranscript(intent.RawTranscript);
        if (transcriptVerdict.Verdict == CommandSafetyVerdict.Blocked)
        {
            return transcriptVerdict;
        }

        return intent switch
        {
            TransformCommandIntent => new CommandSafetyResult(CommandSafetyVerdict.Safe, "Text transformation intent is safe."),
            EditorCommandIntent editor => EvaluateEditorAction(editor.ActionName),
            CancelCommandIntent => new CommandSafetyResult(CommandSafetyVerdict.Safe, "Cancellation intent is safe."),
            DeleteSelectionIntent => new CommandSafetyResult(CommandSafetyVerdict.Safe, "Delete selection intent is safe when confirmed."),
            ApplicationCommandIntent app => ApplicationAllowlist.IsAllowed(app.ApplicationName)
                ? new CommandSafetyResult(CommandSafetyVerdict.Safe, $"Allowlisted app '{app.ApplicationName}' is safe.")
                : new CommandSafetyResult(CommandSafetyVerdict.Blocked, $"App '{app.ApplicationName}' is not allowlisted."),
            UrlCommandIntent url => UrlSafetyValidator.TryValidateUrl(url.ValidatedUrl.ToString(), out _, out _)
                ? new CommandSafetyResult(CommandSafetyVerdict.Safe, "Validated URL is safe.")
                : new CommandSafetyResult(CommandSafetyVerdict.Blocked, "Prohibited URL scheme."),
            UnknownCommandIntent unk => new CommandSafetyResult(CommandSafetyVerdict.Unknown, unk.FailureReason),
            _ => new CommandSafetyResult(CommandSafetyVerdict.Unknown, "Unrecognized intent type.")
        };
    }

    private static CommandSafetyResult EvaluateEditorAction(string actionName)
    {
        var normalized = actionName.Trim().ToLowerInvariant();
        return normalized switch
        {
            "undo" or "redo" or "copy" or "cut" or "paste" or "select_all" or "deselect" or "clear_selection"
                => new CommandSafetyResult(CommandSafetyVerdict.Safe, $"Editor action '{actionName}' is safe and reversible."),
            _ => new CommandSafetyResult(CommandSafetyVerdict.Unknown, $"Unrecognized editor action '{actionName}'. Fails closed.")
        };
    }
}
