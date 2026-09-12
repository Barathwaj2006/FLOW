using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Flow.Core.Context;

namespace Flow.Core.Commands;

/// <summary>
/// Verdict emitted by the command policy engine.
/// </summary>
public enum CommandPolicyDecision
{
    /// <summary>Permitted to execute immediately within target context.</summary>
    Allow,

    /// <summary>Requires explicit secondary user confirmation before execution.</summary>
    Confirm,

    /// <summary>Command cannot be fulfilled or target is invalid; fails closed without side-effects.</summary>
    Reject,

    /// <summary>Strictly prohibited dangerous action (shell execution, destructive operations, system alteration).</summary>
    Block
}

/// <summary>
/// Evaluation result from <see cref="ICommandPolicy"/>.
/// </summary>
public sealed record CommandPolicyEvaluation(
    CommandPolicyDecision Decision,
    string Reason,
    CommandRisk Risk,
    string? ProhibitedToken = null
)
{
    public static CommandPolicyEvaluation Allow(string reason = "Command permitted by policy") =>
        new(CommandPolicyDecision.Allow, reason, CommandRisk.Safe);

    public static CommandPolicyEvaluation Confirm(string reason, CommandRisk risk = CommandRisk.ConfirmRequired) =>
        new(CommandPolicyDecision.Confirm, reason, risk);

    public static CommandPolicyEvaluation Reject(string reason) =>
        new(CommandPolicyDecision.Reject, reason, CommandRisk.LowRisk);

    public static CommandPolicyEvaluation Block(string reason, string? token = null) =>
        new(CommandPolicyDecision.Block, reason, CommandRisk.Blocked, token);
}

/// <summary>
/// Policy engine evaluating command intent against target context, permissions, and risk (Section 29).
/// </summary>
public interface ICommandPolicy
{
    CommandPolicyEvaluation Evaluate(CommandIntent intent, CommandExecutionContext context);
}

/// <summary>
/// Deterministic implementation of <see cref="ICommandPolicy"/>.
/// Pure logic, zero network, zero LLM, 100% testable.
/// </summary>
public sealed class DeterministicCommandPolicy : ICommandPolicy
{
    private static readonly List<(Regex Pattern, string Description)> BlockedPatterns = new()
    {
        // System control & power
        (new Regex(@"\bshutdown\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System shutdown commands are strictly blocked."),
        (new Regex(@"\breboot\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System reboot commands are strictly blocked."),
        (new Regex(@"\brestart-computer\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System restart commands are strictly blocked."),
        (new Regex(@"\bpoweroff\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System poweroff commands are strictly blocked."),
        (new Regex(@"\blogoff\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "System logoff commands are strictly blocked."),

        // Shell interpreters & CLI processes
        (new Regex(@"\bcmd(\.exe)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Command Prompt shell execution is strictly blocked."),
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

        // Process termination
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

    public CommandPolicyEvaluation Evaluate(CommandIntent intent, CommandExecutionContext context)
    {
        if (intent == null)
        {
            return CommandPolicyEvaluation.Reject("Null command intent.");
        }

        // 1. Evaluate raw transcript against blocked shell/destructive patterns
        if (!string.IsNullOrWhiteSpace(intent.RawTranscript))
        {
            foreach (var (pattern, description) in BlockedPatterns)
            {
                var match = pattern.Match(intent.RawTranscript);
                if (match.Success)
                {
                    return CommandPolicyEvaluation.Block(description, match.Value);
                }
            }
        }

        // 2. WF-030: Sensitive / Password Field Gate (Fail Closed)
        if (context.Category == ApplicationCategory.Sensitive || context.FocusedControl.IsPassword)
        {
            return CommandPolicyEvaluation.Block("Password and credential controls strictly forbid command interaction.");
        }

        // 3. Evaluate by intent type
        switch (intent)
        {
            case DeleteSelectionIntent:
                if (string.IsNullOrEmpty(context.SelectionText))
                {
                    return CommandPolicyEvaluation.Reject("No text is selected to delete.");
                }
                return CommandPolicyEvaluation.Confirm("Deleting selected text requires explicit user confirmation.", CommandRisk.ConfirmRequired);

            case TransformCommandIntent transformIntent:
                if (string.IsNullOrEmpty(context.SelectionText))
                {
                    return CommandPolicyEvaluation.Reject("No text is selected to transform.");
                }

                var risk = TransformSafetyValidator.EvaluateTransformRisk(transformIntent.Transform, context.SelectionText);
                if (risk == CommandRisk.Blocked)
                {
                    return CommandPolicyEvaluation.Block($"Selection length ({context.SelectionText.Length} chars) exceeds maximum allowable transform bounds.");
                }
                if (risk == CommandRisk.ConfirmRequired)
                {
                    return CommandPolicyEvaluation.Confirm($"Transforming large selection ({context.SelectionText.Length} chars) requires explicit confirmation.", CommandRisk.ConfirmRequired);
                }
                return CommandPolicyEvaluation.Allow($"Safe text transform '{transformIntent.Transform}' permitted.");

            case ApplicationCommandIntent appIntent:
                if (!ApplicationAllowlist.IsAllowed(appIntent.ApplicationName))
                {
                    return CommandPolicyEvaluation.Block($"Application '{appIntent.ApplicationName}' is not in the approved application allowlist.");
                }
                return CommandPolicyEvaluation.Allow($"Allowlisted application '{appIntent.ApplicationName}' permitted.");

            case UrlCommandIntent urlIntent:
                if (urlIntent.ValidatedUrl == null ||
                    (!urlIntent.ValidatedUrl.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
                     !urlIntent.ValidatedUrl.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)))
                {
                    return CommandPolicyEvaluation.Block("Prohibited URL scheme. Only HTTPS and HTTP are permitted.");
                }
                return CommandPolicyEvaluation.Allow($"Safe URL navigation to '{urlIntent.ValidatedUrl.Host}' permitted.");

            case EditorCommandIntent editorIntent:
                return EvaluateEditorAction(editorIntent.ActionName, context);

            case CancelCommandIntent:
                return CommandPolicyEvaluation.Allow("Cancellation is always permitted.");

            case UnknownCommandIntent unk:
                return CommandPolicyEvaluation.Reject(unk.FailureReason);

            default:
                return CommandPolicyEvaluation.Reject("Unrecognized command intent type.");
        }
    }

    private static CommandPolicyEvaluation EvaluateEditorAction(string actionName, CommandExecutionContext context)
    {
        string normalized = actionName.Trim().ToLowerInvariant();

        return normalized switch
        {
            "copy" => CommandPolicyEvaluation.Allow("Copy action permitted."),
            "paste" => CommandPolicyEvaluation.Allow("Paste action permitted."),
            "undo" => CommandPolicyEvaluation.Allow("Undo action permitted."),
            "redo" => CommandPolicyEvaluation.Allow("Redo action permitted."),
            "select_all" or "highlight_all" => CommandPolicyEvaluation.Allow("Select all action permitted."),
            "deselect" or "clear_selection" => CommandPolicyEvaluation.Allow("Deselect action permitted."),
            _ => CommandPolicyEvaluation.Reject($"Unknown editor action '{actionName}'.")
        };
    }
}
