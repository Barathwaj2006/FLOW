using System;
using System.Collections.Generic;

namespace Flow.Core.Backtrack;

/// <summary>
/// Thread-safe bounded history tracker for session text insertions.
/// Supports LIFO retrieval for single and chained backtrack operations.
/// </summary>
public sealed class InsertionHistoryTracker
{
    private readonly object _lock = new();
    private readonly List<InsertionRecord> _history = new();
    private readonly int _maxCapacity;

    public int Count
    {
        get
        {
            lock (_lock) return _history.Count;
        }
    }

    public InsertionHistoryTracker(int maxCapacity = 50)
    {
        _maxCapacity = maxCapacity > 0 ? maxCapacity : 50;
    }

    public void RecordInsertion(InsertionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_lock)
        {
            _history.Add(record);
            if (_history.Count > _maxCapacity)
            {
                _history.RemoveAt(0);
            }
        }
    }

    public InsertionRecord? PeekLastInsertion()
    {
        lock (_lock)
        {
            if (_history.Count == 0) return null;
            return _history[^1];
        }
    }

    public InsertionRecord? PopLastInsertion()
    {
        lock (_lock)
        {
            if (_history.Count == 0) return null;
            var item = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            return item;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _history.Clear();
        }
    }
}
