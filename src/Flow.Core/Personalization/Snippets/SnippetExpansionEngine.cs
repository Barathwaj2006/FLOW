using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Personalization.Snippets;

/// <summary>
/// In-memory engine that matches spoken trigger phrases and expands them to boilerplate snippets.
/// Enforces longest-match priority, conflict resolution, and the inviolable Zero-Enter safety invariant.
/// </summary>
public sealed class SnippetExpansionEngine
{
    private readonly ISnippetRepository? _repository;
    private readonly object _lock = new();
    private List<SnippetEntry> _snippets = new();

    public SnippetExpansionEngine(ISnippetRepository? repository = null)
    {
        _repository = repository;
    }

    /// <summary>
    /// Synchronously sets the active snippets collection.
    /// </summary>
    public void SetSnippets(IEnumerable<SnippetEntry> snippets)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        lock (_lock)
        {
            // Only enabled snippets, sorted by trigger length descending (longest match first)
            _snippets = snippets
                .Where(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.TriggerPhrase))
                .OrderByDescending(s => s.TriggerPhrase.Trim().Length)
                .ToList();
        }
    }

    /// <summary>
    /// Loads all snippets from the backing repository asynchronously.
    /// </summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (_repository == null) return;
        var list = await _repository.GetAllAsync(ct);
        SetSnippets(list);
    }

    /// <summary>
    /// Detects potential collision/ambiguity between trigger phrases.
    /// </summary>
    public IReadOnlyList<(string Shorter, string Longer)> GetConflicts()
    {
        var conflicts = new List<(string Shorter, string Longer)>();
        List<SnippetEntry> active;
        lock (_lock)
        {
            active = _snippets.ToList();
        }

        for (int i = 0; i < active.Count; i++)
        {
            for (int j = i + 1; j < active.Count; j++)
            {
                var longer = active[i].TriggerPhrase.Trim();
                var shorter = active[j].TriggerPhrase.Trim();

                if (longer.Equals(shorter, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add((shorter, longer));
                }
                else if (longer.Contains(shorter, StringComparison.OrdinalIgnoreCase))
                {
                    conflicts.Add((shorter, longer));
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Expands any matched spoken trigger phrases in the input text.
    /// Inviolably strips newlines from expansion text to preserve the Zero-Enter invariant.
    /// </summary>
    public string Expand(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        List<SnippetEntry> snippets;
        lock (_lock)
        {
            if (_snippets.Count == 0) return text;
            snippets = _snippets.ToList();
        }

        string result = text;

        foreach (var snippet in snippets)
        {
            string trigger = snippet.TriggerPhrase.Trim();
            if (string.IsNullOrEmpty(trigger)) continue;

            // Safe expansion text with Zero-Enter guarantee: convert any newlines to spaces
            string safeExpansion = snippet.ExpansionText
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");

            // Match full word boundary for the trigger phrase
            string pattern = $@"(?<!\w){Regex.Escape(trigger)}(?!\w)";
            result = Regex.Replace(result, pattern, safeExpansion, RegexOptions.IgnoreCase);
        }

        return result;
    }
}
