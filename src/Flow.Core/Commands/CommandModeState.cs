using System;

namespace Flow.Core.Commands;

/// <summary>
/// States for the explicit Command Mode state machine.
/// Hidden or unmonitored state transitions are strictly prohibited.
/// </summary>
public enum CommandModeState
{
    /// <summary>Command Mode is disabled; normal dictation rules apply.</summary>
    Disabled,

    /// <summary>Explicit command gesture initiated; arming command subsystem.</summary>
    Arming,

    /// <summary>Command mode actively listening for spoken commands.</summary>
    Active,

    /// <summary>A potentially risky or destructive command was recognized; awaiting explicit user confirmation.</summary>
    AwaitingConfirmation,

    /// <summary>Approved command is actively executing within safe boundaries.</summary>
    Executing,

    /// <summary>Command execution completed successfully.</summary>
    Completed,

    /// <summary>Command was cancelled by user gesture, escape key, timeout, or target change.</summary>
    Cancelled,

    /// <summary>Command failed or was rejected due to safety policy, target loss, or execution error.</summary>
    Failed
}

/// <summary>
/// Thread-safe explicit state machine governing Command Mode lifecycle.
/// Enforces inviolable transition boundaries: speech alone cannot enter Arming or Active.
/// </summary>
public sealed class CommandModeStateMachine
{
    private readonly object _lock = new();
    private CommandModeState _currentState = CommandModeState.Disabled;
    private string? _lastReason;

    public event Action<CommandModeState, CommandModeState, string?>? StateChanged;

    public CommandModeState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    public string? LastReason
    {
        get
        {
            lock (_lock)
            {
                return _lastReason;
            }
        }
    }

    public bool IsActive => CurrentState is CommandModeState.Active or CommandModeState.AwaitingConfirmation or CommandModeState.Executing;

    public bool CanTransitionTo(CommandModeState nextState)
    {
        lock (_lock)
        {
            return IsValidTransition(_currentState, nextState);
        }
    }

    public bool TryTransitionTo(CommandModeState nextState, string? reason = null)
    {
        CommandModeState previous;
        lock (_lock)
        {
            if (!IsValidTransition(_currentState, nextState))
            {
                return false;
            }

            previous = _currentState;
            _currentState = nextState;
            _lastReason = reason;
        }

        StateChanged?.Invoke(previous, nextState, reason);
        return true;
    }

    public void ForceReset(string? reason = "Reset to disabled")
    {
        CommandModeState previous;
        lock (_lock)
        {
            previous = _currentState;
            _currentState = CommandModeState.Disabled;
            _lastReason = reason;
        }

        if (previous != CommandModeState.Disabled)
        {
            StateChanged?.Invoke(previous, CommandModeState.Disabled, reason);
        }
    }

    private static bool IsValidTransition(CommandModeState current, CommandModeState next)
    {
        if (current == next)
        {
            return true;
        }

        return (current, next) switch
        {
            // From Disabled, can only enter Arming via explicit gesture
            (CommandModeState.Disabled, CommandModeState.Arming) => true,

            // From Arming, can become Active or be Cancelled/Failed
            (CommandModeState.Arming, CommandModeState.Active) => true,
            (CommandModeState.Arming, CommandModeState.Cancelled) => true,
            (CommandModeState.Arming, CommandModeState.Failed) => true,
            (CommandModeState.Arming, CommandModeState.Disabled) => true,

            // From Active, can go to AwaitingConfirmation, Executing, Cancelled, Failed, or Completed
            (CommandModeState.Active, CommandModeState.AwaitingConfirmation) => true,
            (CommandModeState.Active, CommandModeState.Executing) => true,
            (CommandModeState.Active, CommandModeState.Completed) => true,
            (CommandModeState.Active, CommandModeState.Cancelled) => true,
            (CommandModeState.Active, CommandModeState.Failed) => true,
            (CommandModeState.Active, CommandModeState.Disabled) => true,

            // From AwaitingConfirmation, can go to Executing (confirmed), Cancelled (timeout/esc), or Failed
            (CommandModeState.AwaitingConfirmation, CommandModeState.Executing) => true,
            (CommandModeState.AwaitingConfirmation, CommandModeState.Cancelled) => true,
            (CommandModeState.AwaitingConfirmation, CommandModeState.Failed) => true,
            (CommandModeState.AwaitingConfirmation, CommandModeState.Disabled) => true,

            // From Executing, can go to Completed, Failed, or Cancelled
            (CommandModeState.Executing, CommandModeState.Completed) => true,
            (CommandModeState.Executing, CommandModeState.Failed) => true,
            (CommandModeState.Executing, CommandModeState.Cancelled) => true,

            // Terminal states can reset to Disabled or Arming for next session
            (CommandModeState.Completed, CommandModeState.Disabled) => true,
            (CommandModeState.Completed, CommandModeState.Arming) => true,
            (CommandModeState.Cancelled, CommandModeState.Disabled) => true,
            (CommandModeState.Cancelled, CommandModeState.Arming) => true,
            (CommandModeState.Failed, CommandModeState.Disabled) => true,
            (CommandModeState.Failed, CommandModeState.Arming) => true,

            _ => false
        };
    }
}
