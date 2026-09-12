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
        (new Regex(@"\brestart-computer\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System restart commands are strictly blocked."),
        (new Regex(@"\bpoweroff\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System poweroff commands are strictly blocked."),
        (new Regex(@"\blogoff\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System logoff commands are strictly blocked."),

        // Shell interpreters & CLI processes
        (new Regex(@"\bcmd(\.exe)?\s*(/[ck])?", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Command Prompt shell execution is strictly blocked."),
        (new Regex(@"\bpowershell(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PowerShell process execution is strictly blocked."),
        (new Regex(@"\bpwsh(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PowerShell Core process execution is strictly blocked."),
        (new Regex(@"\bbash(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Bash shell execution is strictly blocked."),
        (new Regex(@"\bwscript(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Windows Script Host execution is strictly blocked."),
        (new Regex(@"\bcscript(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Console Script Host execution is strictly blocked."),

        // Filesystem destruction & mass deletion
        (new Regex(@"\brm\s+(-[rfRF]+|\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Filesystem removal commands are strictly blocked."),
        (new Regex(@"\bdel\s+([a-zA-Z0-9_\-\*\\/.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "File deletion commands are strictly blocked."),
        (new Regex(@"\berase\s+([a-zA-Z0-9_\-\*\\/.]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "File erase commands are strictly blocked."),
        (new Regex(@"\bremove-item\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PowerShell Remove-Item commands are strictly blocked."),
        (new Regex(@"\brmdir\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Directory removal commands are strictly blocked."),
        (new Regex(@"\brd\s+/[sqSQ]", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Directory tree removal commands are strictly blocked."),
        (new Regex(@"\bformat\s+([a-zA-Z]:|drive|disk)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Drive format commands are strictly blocked."),
        (new Regex(@"\bdiskpart\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Disk partitioning commands are strictly blocked."),

        // Process termination & task killing
        (new Regex(@"\btaskkill\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process termination commands are strictly blocked."),
        (new Regex(@"\bstop-process\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process termination commands are strictly blocked."),
        (new Regex(@"\bkill\s+(-9|\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Process kill commands are strictly blocked."),

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
