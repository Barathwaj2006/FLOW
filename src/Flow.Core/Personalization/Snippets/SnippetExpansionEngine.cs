using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Language;

namespace Flow.Core.Personalization.Snippets;

/// <summary>
/// In-memory engine that matches spoken trigger phrases and expands them to boilerplate snippets.
/// Enforces longest-match priority, conflict resolution, application/language scoping,
/// and the inviolable Zero-Enter safety invariant during normal voice dictation.
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

    public SnippetExpansionEngine(IEnumerable<SnippetEntry> snippets) : this()
    {
        SetSnippets(snippets);
    }

    /// <summary>
    /// Gets a snapshot of the current in-memory snippets.
    /// </summary>
    public IReadOnlyList<SnippetEntry> GetSnippets()
    {
        lock (_lock)
        {
            return _snippets.ToList();
        }
    }

    /// <summary>
    /// Synchronously sets the active snippets collection.
    /// </summary>
    public void SetSnippets(IEnumerable<SnippetEntry> snippets)
    {
        ArgumentNullException.ThrowIfNull(snippets);
        lock (_lock)
        {
            // Sort priority: App-specific first, Language-specific next, then trigger length descending
            _snippets = snippets
                .Where(s => s.IsEnabled && !string.IsNullOrWhiteSpace(s.TriggerPhrase))
                .OrderByDescending(s => !string.IsNullOrWhiteSpace(s.ApplicationScope))
                .ThenByDescending(s => !string.IsNullOrWhiteSpace(s.Language))
                .ThenByDescending(s => s.TriggerPhrase.Trim().Length)
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
    /// Expands any matched spoken trigger phrases in the input text for normal dictation.
    /// Inviolably strips newlines from expansion text to preserve the Zero-Enter invariant.
    /// </summary>
    public string Expand(string text) => Expand(text, targetApplication: (string?)null, language: (LanguageInfo?)null);

    /// <summary>
    /// Expands matched spoken trigger phrases with application and string language code scoping.
    /// Converts any newlines to spaces to strictly uphold the Zero-Enter safety invariant.
    /// </summary>
    public string Expand(string text, string? targetApplication = null, string? language = null)
    {
        var langInfo = !string.IsNullOrWhiteSpace(language) ? Flow.Core.Language.LanguageCatalog.GetLanguageOrDefault(language) : null;
        return Expand(text, targetApplication, langInfo);
    }

    /// <summary>
    /// Expands matched spoken trigger phrases with application and language scoping.
    /// Converts any newlines to spaces to strictly uphold the Zero-Enter safety invariant.
    /// </summary>
    public string Expand(string text, string? targetApplication, LanguageInfo? language)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        List<SnippetEntry> snippets;
        lock (_lock)
        {
            if (_snippets.Count == 0) return text;
            snippets = _snippets.ToList();
        }

        string? cleanApp = null;
        if (!string.IsNullOrWhiteSpace(targetApplication))
        {
            cleanApp = targetApplication.Trim();
            if (cleanApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                cleanApp = cleanApp[..^4];
            }
        }

        string result = text;

        foreach (var snippet in snippets)
        {
            if (!snippet.IsEnabled) continue;

            // 1. Language Scoping check
            if (!string.IsNullOrWhiteSpace(snippet.Language))
            {
                if (language == null) continue;
                bool langMatches = string.Equals(snippet.Language, language.Code.Value, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(snippet.Language, language.WhisperCode, StringComparison.OrdinalIgnoreCase);
                if (!langMatches) continue;
            }

            // 2. Application Scoping check
            if (!string.IsNullOrWhiteSpace(snippet.ApplicationScope))
            {
                if (string.IsNullOrWhiteSpace(cleanApp)) continue;
                string entryApp = snippet.ApplicationScope.Trim();
                if (entryApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    entryApp = entryApp[..^4];
                }
                if (!string.Equals(entryApp, cleanApp, StringComparison.OrdinalIgnoreCase)) continue;
            }

            string trigger = snippet.TriggerPhrase.Trim();
            if (string.IsNullOrEmpty(trigger)) continue;

            // Inviolable Zero-Enter guarantee: convert any newlines to spaces for normal dictation
            string safeExpansion = snippet.ExpansionText
                .Replace("\r\n", " ")
                .Replace("\r", " ")
                .Replace("\n", " ");

            // Match full word boundary for the trigger phrase across Unicode letters and numbers
            string pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(trigger)}(?![\p{{L}}\p{{N}}_])";
            result = Regex.Replace(result, pattern, safeExpansion, RegexOptions.IgnoreCase);
        }

        return result;
    }

    /// <summary>
    /// Explicit snippet expansion mechanism separate from normal dictation with string language code.
    /// Preserves original multiline formatting when explicit insertion is commanded.
    /// </summary>
    public string? ExpandForExplicitSnippetInsertion(string trigger, string? targetApplication = null, string? language = null)
    {
        var langInfo = !string.IsNullOrWhiteSpace(language) ? Flow.Core.Language.LanguageCatalog.GetLanguageOrDefault(language) : null;
        return ExpandForExplicitSnippetInsertion(trigger, targetApplication, langInfo);
    }

    /// <summary>
    /// Explicit snippet expansion mechanism separate from normal dictation.
    /// Preserves original multiline formatting when explicit insertion is commanded.
    /// </summary>
    public string? ExpandForExplicitSnippetInsertion(string trigger, string? targetApplication, LanguageInfo? language)
    {
        if (string.IsNullOrWhiteSpace(trigger)) return null;

        List<SnippetEntry> snippets;
        lock (_lock)
        {
            snippets = _snippets.ToList();
        }

        string? cleanApp = null;
        if (!string.IsNullOrWhiteSpace(targetApplication))
        {
            cleanApp = targetApplication.Trim();
            if (cleanApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                cleanApp = cleanApp[..^4];
            }
        }

        foreach (var snippet in snippets)
        {
            if (!snippet.IsEnabled) continue;

            if (!string.IsNullOrWhiteSpace(snippet.Language))
            {
                if (language == null) continue;
                bool langMatches = string.Equals(snippet.Language, language.Code.Value, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(snippet.Language, language.WhisperCode, StringComparison.OrdinalIgnoreCase);
                if (!langMatches) continue;
            }

            if (!string.IsNullOrWhiteSpace(snippet.ApplicationScope))
            {
                if (string.IsNullOrWhiteSpace(cleanApp)) continue;
                string entryApp = snippet.ApplicationScope.Trim();
                if (entryApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    entryApp = entryApp[..^4];
                }
                if (!string.Equals(entryApp, cleanApp, StringComparison.OrdinalIgnoreCase)) continue;
            }

            if (snippet.TriggerPhrase.Trim().Equals(trigger.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return snippet.ExpansionText;
            }
        }

        return null;
    }
}
