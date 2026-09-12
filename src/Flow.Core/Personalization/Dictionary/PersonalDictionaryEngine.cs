using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Personalization.Dictionary;

/// <summary>
/// In-memory engine that compiles personal dictionary entries into deterministic regex transformations
/// for low-latency (<1ms) execution in the voice pipeline.
/// </summary>
public sealed class PersonalDictionaryEngine
{
    private readonly IPersonalDictionaryRepository? _repository;
    private readonly object _lock = new();
    private List<DictionaryEntry> _entries = new();

    public PersonalDictionaryEngine(IPersonalDictionaryRepository? repository = null)
    {
        _repository = repository;
    }

    /// <summary>
    /// Synchronously loads or updates the in-memory entry cache.
    /// </summary>
    public void SetEntries(IEnumerable<DictionaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        lock (_lock)
        {
            // Sort priority: Starred first, then by Term length descending (longest phrase match first)
            _entries = entries
                .OrderByDescending(e => e.IsStarred)
                .ThenByDescending(e => e.Term.Length)
                .ToList();
        }
    }

    /// <summary>
    /// Loads all entries from the backing repository asynchronously.
    /// </summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (_repository == null) return;
        var list = await _repository.GetAllAsync(ct);
        SetEntries(list);
    }

    /// <summary>
    /// Applies personal dictionary and correction transformations to the provided text.
    /// </summary>
    public string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        List<DictionaryEntry> entries;
        lock (_lock)
        {
            if (_entries.Count == 0) return text;
            entries = _entries.ToList();
        }

        string result = text;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Term)) continue;

            string target = entry.EffectiveText;
            string pattern = $@"(?<!\w){Regex.Escape(entry.Term)}(?!\w)";
            var options = entry.CaseSensitive
                ? RegexOptions.None
                : RegexOptions.IgnoreCase;

            result = Regex.Replace(result, pattern, target, options);
        }

        return result;
    }
}
