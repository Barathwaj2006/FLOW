using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Core.Commands;

/// <summary>
/// Audit trail entry recording command execution metadata (Section 24).
/// STRICT PRIVACY POLICY: Never stores raw passwords, credentials, or private document contents.
/// </summary>
public sealed record CommandAuditEntry(
    Guid SessionId,
    DateTimeOffset Timestamp,
    string CommandId,
    CommandIntentType IntentType,
    CommandResultStatus Result,
    CommandRisk Risk,
    string? Application,
    TimeSpan Duration,
    string? FailureReason = null
);

/// <summary>
/// Thread-safe in-memory audit trail for command execution monitoring.
/// Uses a bounded ring-buffer to prevent memory growth.
/// </summary>
public sealed class CommandAuditTrail
{
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly Queue<CommandAuditEntry> _entries;

    public CommandAuditTrail(int capacity = 1000)
    {
        _capacity = Math.Max(10, capacity);
        _entries = new Queue<CommandAuditEntry>(_capacity);
    }

    public void Record(CommandAuditEntry entry)
    {
        if (entry == null) return;

        lock (_lock)
        {
            if (_entries.Count >= _capacity)
            {
                _entries.Dequeue();
            }
            _entries.Enqueue(entry);
        }
    }

    public IReadOnlyList<CommandAuditEntry> GetRecentEntries(int maxCount = 100)
    {
        lock (_lock)
        {
            return _entries.TakeLast(Math.Min(maxCount, _entries.Count)).ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }
    }
}
