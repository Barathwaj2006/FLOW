using System;
using Flow.Core.Commands;

namespace Flow.Host.Windows.Commands;

/// <summary>
/// Manages pending command confirmations on the Windows host.
/// Enforces single-active-token, automatic timeout expiration, and target binding.
/// </summary>
public sealed class WindowsCommandConfirmation
{
    private readonly object _lock = new();
    private readonly ICommandConfirmationService _confirmationService;
    private CommandConfirmationToken? _activeToken;
    private CommandIntent? _pendingIntent;

    public WindowsCommandConfirmation(ICommandConfirmationService? confirmationService = null)
    {
        _confirmationService = confirmationService ?? new CommandConfirmationService();
    }

    public bool IsAwaitingConfirmation
    {
        get
        {
            lock (_lock)
            {
                if (_activeToken == null) return false;
                if (_activeToken.IsExpired)
                {
                    _activeToken = null;
                    _pendingIntent = null;
                    return false;
                }
                return true;
            }
        }
    }

    public CommandConfirmationToken? ActiveToken
    {
        get
        {
            lock (_lock)
            {
                return _activeToken;
            }
        }
    }

    public CommandIntent? PendingIntent
    {
        get
        {
            lock (_lock)
            {
                return _pendingIntent;
            }
        }
    }

    public CommandConfirmationToken IssueConfirmation(CommandExecutionContext context, CommandIntent intent, TimeSpan? lifetime = null)
    {
        lock (_lock)
        {
            var token = _confirmationService.CreateToken(context, intent.GetType().Name, lifetime);
            _activeToken = token;
            _pendingIntent = intent;
            return token;
        }
    }

    public bool TryConfirm(CommandExecutionContext currentContext, string spokenPhrase, out CommandIntent? intent, out string? failureReason)
    {
        lock (_lock)
        {
            intent = null;
            if (_activeToken == null)
            {
                failureReason = "No confirmation is currently pending.";
                return false;
            }

            bool valid = _confirmationService.ValidateConfirmation(_activeToken, currentContext, spokenPhrase, out failureReason);
            if (valid)
            {
                intent = _pendingIntent;
                _activeToken = null;
                _pendingIntent = null;
                return true;
            }

            // Invalidate on failed confirmation
            _activeToken = null;
            _pendingIntent = null;
            return false;
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _activeToken = null;
            _pendingIntent = null;
        }
    }
}
