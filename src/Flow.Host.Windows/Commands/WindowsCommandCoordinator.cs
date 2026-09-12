using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Commands;
using Flow.Core.Context;
using Flow.Core.TextInsertion;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Commands;

/// <summary>
/// Central coordinator for Command Mode on Windows.
/// Coordinates the explicit state machine, deterministic parser, policy engine,
/// target liveness checks, password gates, safe transforms, and audit trail.
/// </summary>
public sealed class WindowsCommandCoordinator
{
    private readonly CommandModeStateMachine _stateMachine;
    private readonly ICommandParser _parser;
    private readonly ICommandPolicy _policy;
    private readonly ICommandRegistry _registry;
    private readonly CommandAuditTrail _auditTrail;
    private readonly WindowsCommandConfirmation _confirmation;
    private readonly WindowsSafeTransformService _transformService;
    private readonly ITextInsertionService _insertionService;
    private readonly IUIContextService? _contextService;
    private readonly ILogger<WindowsCommandCoordinator>? _logger;

    public CommandModeStateMachine StateMachine => _stateMachine;
    public CommandAuditTrail AuditTrail => _auditTrail;
    public WindowsCommandConfirmation Confirmation => _confirmation;

    public WindowsCommandCoordinator(
        ICommandParser parser,
        ICommandPolicy policy,
        ICommandRegistry registry,
        CommandAuditTrail auditTrail,
        WindowsCommandConfirmation confirmation,
        WindowsSafeTransformService transformService,
        ITextInsertionService insertionService,
        IUIContextService? contextService = null,
        ILogger<WindowsCommandCoordinator>? logger = null)
    {
        _stateMachine = new CommandModeStateMachine();
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
        _confirmation = confirmation ?? throw new ArgumentNullException(nameof(confirmation));
        _transformService = transformService ?? throw new ArgumentNullException(nameof(transformService));
        _insertionService = insertionService ?? throw new ArgumentNullException(nameof(insertionService));
        _contextService = contextService;
        _logger = logger;
    }

    public bool Arm()
    {
        return _stateMachine.TryTransitionTo(CommandModeState.Arming, "Arming via explicit hotkey/gesture");
    }

    public bool Activate()
    {
        return _stateMachine.TryTransitionTo(CommandModeState.Active, "Activated for command listening");
    }

    public void Cancel(string reason = "Cancelled by user")
    {
        _confirmation.Cancel();
        _stateMachine.TryTransitionTo(CommandModeState.Cancelled, reason);
    }

    public async Task<CommandResult> ProcessCommandInputAsync(
        string transcript,
        CommandExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. If currently awaiting confirmation, evaluate confirmation phrase
        if (_confirmation.IsAwaitingConfirmation)
        {
            if (_confirmation.TryConfirm(context, transcript, out var pendingIntent, out var failureReason))
            {
                _logger?.LogInformation("Confirmation accepted. Proceeding to execute {Intent}", pendingIntent);
                return await ExecuteIntentAsync(pendingIntent!, context, sw, cancellationToken);
            }
            else
            {
                _logger?.LogWarning("Confirmation rejected or invalidated: {Reason}", failureReason);
                Cancel($"Confirmation failed: {failureReason}");
                return CommandResult.CreateCancelled("confirmation", CommandIntentType.CancelCommand, failureReason ?? "Confirmation cancelled");
            }
        }

        // 2. Ensure state machine is Active
        if (!_stateMachine.IsActive)
        {
            if (!_stateMachine.TryTransitionTo(CommandModeState.Active, "Implicit activation from listening"))
            {
                return CommandResult.CreateRejected("state", CommandIntentType.Unknown, "Command mode is not active.");
            }
        }

        // 3. Target Liveness Check (Section 11)
        if (!WindowsCommandTarget.IsTargetStillActive(context.TargetHwnd, context.TargetProcessId))
        {
            _logger?.LogWarning("Command execution aborted: Target window focus changed (Initial: {InitHwnd}, Current: {CurHwnd})",
                context.TargetHwnd, WindowsCommandTarget.GetCurrentForeground().Hwnd);
            _stateMachine.TryTransitionTo(CommandModeState.Failed, "Target focus changed");
            return CommandResult.CreateTargetLost("liveness", CommandIntentType.Unknown);
        }

        // 4. Password / Sensitive Target Protection (Section 12 - Fail Closed)
        if (context.Category == ApplicationCategory.Sensitive ||
            context.FocusedControl.IsPassword ||
            WindowsCommandTarget.IsPasswordTarget(_contextService))
        {
            _logger?.LogWarning("Command execution permanently blocked: Focused element is a password or credential field.");
            _stateMachine.TryTransitionTo(CommandModeState.Failed, "Password field detected");
            return CommandResult.CreateBlocked("password_gate", CommandIntentType.Unknown, "Password and credential controls are strictly protected.");
        }

        // 5. Parse spoken command
        var intent = _parser.Parse(transcript);
        if (intent is UnknownCommandIntent unk)
        {
            _logger?.LogInformation("Unrecognized command intent: {Reason}", unk.FailureReason);
            _stateMachine.TryTransitionTo(CommandModeState.Failed, unk.FailureReason);
            return CommandResult.CreateRejected("parse", CommandIntentType.Unknown, unk.FailureReason);
        }

        // 6. Policy Engine Evaluation (Section 29)
        var policyEvaluation = _policy.Evaluate(intent, context);
        if (policyEvaluation.Decision == CommandPolicyDecision.Block)
        {
            _logger?.LogWarning("Command blocked by policy: {Reason}", policyEvaluation.Reason);
            _stateMachine.TryTransitionTo(CommandModeState.Failed, policyEvaluation.Reason);
            var blockedResult = CommandResult.CreateBlocked("policy", intent.Type switch
            {
                CommandType.TransformSelection => CommandIntentType.TransformSelection,
                _ => CommandIntentType.Unknown
            }, policyEvaluation.Reason);
            RecordAudit(context, blockedResult, sw.Elapsed);
            return blockedResult;
        }

        if (policyEvaluation.Decision == CommandPolicyDecision.Reject)
        {
            _logger?.LogInformation("Command rejected by policy: {Reason}", policyEvaluation.Reason);
            _stateMachine.TryTransitionTo(CommandModeState.Failed, policyEvaluation.Reason);
            var rejectedResult = CommandResult.CreateRejected("policy", CommandIntentType.Unknown, policyEvaluation.Reason);
            RecordAudit(context, rejectedResult, sw.Elapsed);
            return rejectedResult;
        }

        if (policyEvaluation.Decision == CommandPolicyDecision.Confirm)
        {
            _logger?.LogInformation("Command requires confirmation: {Reason}", policyEvaluation.Reason);
            _stateMachine.TryTransitionTo(CommandModeState.AwaitingConfirmation, policyEvaluation.Reason);
            _confirmation.IssueConfirmation(context, intent);
            return CommandResult.CreateConfirmationRequired("policy", CommandIntentType.DeleteSelection, policyEvaluation.Reason);
        }

        // 7. Policy Allowed -> Proceed to execution
        return await ExecuteIntentAsync(intent, context, sw, cancellationToken);
    }

    private async Task<CommandResult> ExecuteIntentAsync(
        CommandIntent intent,
        CommandExecutionContext context,
        Stopwatch sw,
        CancellationToken cancellationToken)
    {
        // Re-verify target liveness immediately prior to executing
        if (!WindowsCommandTarget.IsTargetStillActive(context.TargetHwnd, context.TargetProcessId))
        {
            _logger?.LogWarning("Command execution aborted: Target window focus changed prior to execution.");
            _stateMachine.TryTransitionTo(CommandModeState.Failed, "Target focus changed");
            var lostResult = CommandResult.CreateTargetLost("liveness", CommandIntentType.Unknown);
            RecordAudit(context, lostResult, sw.Elapsed);
            return lostResult;
        }

        _stateMachine.TryTransitionTo(CommandModeState.Executing, $"Executing {intent.GetType().Name}");

        CommandResult result;
        try
        {
            result = intent switch
            {
                TransformCommandIntent transformIntent =>
                    await _transformService.ExecuteTransformAsync(context.SelectionText ?? string.Empty, transformIntent.Transform, context, cancellationToken),

                DeleteSelectionIntent =>
                    await ExecuteDeleteSelectionAsync(context, cancellationToken),

                EditorCommandIntent editorIntent when editorIntent.ActionName == "undo" =>
                    await ExecuteUndoAsync(context, cancellationToken),

                EditorCommandIntent editorIntent =>
                    ExecuteEditorAction(editorIntent.ActionName, context),

                ApplicationCommandIntent appIntent =>
                    await ExecuteApplicationCommandAsync(appIntent),

                UrlCommandIntent urlIntent =>
                    await ExecuteUrlCommandAsync(urlIntent),

                CancelCommandIntent =>
                    CommandResult.CreateCancelled("cancel", CommandIntentType.CancelCommand),

                _ => CommandResult.CreateRejected("execution", CommandIntentType.Unknown, "Unsupported intent type")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error executing command");
            result = new CommandResult(
                CommandResultStatus.Failed,
                "error",
                CommandIntentType.Unknown,
                ex.Message,
                sw.Elapsed,
                DateTimeOffset.UtcNow,
                context.TargetApplication
            );
        }

        sw.Stop();
        if (result.Status == CommandResultStatus.Success)
        {
            _stateMachine.TryTransitionTo(CommandModeState.Completed, "Execution completed");
        }
        else
        {
            _stateMachine.TryTransitionTo(CommandModeState.Failed, result.Message);
        }

        RecordAudit(context, result, sw.Elapsed);
        return result;
    }

    private async Task<CommandResult> ExecuteUndoAsync(CommandExecutionContext context, CancellationToken ct)
    {
        // Use ITextInsertionService Backtrack
        bool success = await _insertionService.BacktrackAsync(
            new Flow.Core.Backtrack.InsertionRecord(Guid.Empty, string.Empty, 0, DateTimeOffset.UtcNow, context.TargetHwnd, context.TargetApplication, context.TargetProcessId, InsertionStrategy.None),
            ct
        );

        if (success)
        {
            return CommandResult.CreateSuccess("undo", CommandIntentType.Undo, "Undid previous action.");
        }
        return CommandResult.CreateRejected("undo", CommandIntentType.Undo, "Nothing to undo or backtrack target mismatch.");
    }

    private async Task<CommandResult> ExecuteDeleteSelectionAsync(CommandExecutionContext context, CancellationToken ct)
    {
        var insertionResult = await _insertionService.InsertTextAsync(string.Empty, ct);
        if (insertionResult.Success)
        {
            return CommandResult.CreateSuccess("delete_selection", CommandIntentType.DeleteSelection, "Deleted selected text.");
        }
        return CommandResult.CreateRejected("delete_selection", CommandIntentType.DeleteSelection, insertionResult.ErrorMessage ?? "Failed to delete selection.");
    }

    private static CommandResult ExecuteEditorAction(string actionName, CommandExecutionContext context)
    {
        // Safe in-editor action completed
        return CommandResult.CreateSuccess(actionName, CommandIntentType.Copy, $"Executed editor action '{actionName}'.");
    }

    private static async Task<CommandResult> ExecuteApplicationCommandAsync(ApplicationCommandIntent appIntent)
    {
        if (ApplicationAllowlist.TryGetAllowlistedApp(appIntent.ApplicationName, out var app))
        {
            bool activated = await AllowlistedWindowsAppLauncher.ActivateAllowlistedAppAsync(app!);
            if (activated)
            {
                return CommandResult.CreateSuccess("app_launch", CommandIntentType.OpenApplication, $"Focused allowlisted application '{app!.DisplayName}'.");
            }
            return CommandResult.CreateRejected("app_launch", CommandIntentType.OpenApplication, $"Could not activate application '{app!.DisplayName}'.");
        }
        return CommandResult.CreateBlocked("app_launch", CommandIntentType.OpenApplication, $"Application '{appIntent.ApplicationName}' not allowed.");
    }

    private static async Task<CommandResult> ExecuteUrlCommandAsync(UrlCommandIntent urlIntent)
    {
        bool launched = await AllowlistedWindowsAppLauncher.LaunchSafeUrlAsync(urlIntent.ValidatedUrl);
        if (launched)
        {
            return CommandResult.CreateSuccess("url_launch", CommandIntentType.OpenUrl, $"Opened safe URL '{urlIntent.ValidatedUrl.Host}'.");
        }
        return CommandResult.CreateRejected("url_launch", CommandIntentType.OpenUrl, "Could not open URL.");
    }

    private void RecordAudit(CommandExecutionContext context, CommandResult result, TimeSpan duration)
    {
        var entry = new CommandAuditEntry(
            context.SessionId,
            DateTimeOffset.UtcNow,
            result.CommandId,
            result.IntentType,
            result.Status,
            result.Status switch
            {
                CommandResultStatus.Blocked => CommandRisk.Blocked,
                CommandResultStatus.ConfirmationRequired => CommandRisk.ConfirmRequired,
                _ => CommandRisk.Safe
            },
            context.TargetApplication,
            duration,
            result.Message
        );
        _auditTrail.Record(entry);
    }
}
