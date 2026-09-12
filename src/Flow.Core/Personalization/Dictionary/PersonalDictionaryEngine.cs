using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Language;

namespace Flow.Core.Personalization.Dictionary;

/// <summary>
/// In-memory engine that compiles personal dictionary entries into deterministic regex transformations
/// for low-latency (<1ms) execution in the voice pipeline.
/// Supports language-scoping, application-scoping, and Unicode-safe token boundary matching.
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

    public PersonalDictionaryEngine(IEnumerable<DictionaryEntry> entries) : this()
    {
        SetEntries(entries);
    }

    /// <summary>
    /// Gets a snapshot of the current in-memory entries.
    /// </summary>
    public IReadOnlyList<DictionaryEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    /// <summary>
    /// Synchronously loads or updates the in-memory entry cache.
    /// </summary>
    public void SetEntries(IEnumerable<DictionaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        lock (_lock)
        {
            // Sort priority: App-specific first, Language-specific next, Starred first, then by Term length descending
            _entries = entries
                .OrderByDescending(e => !string.IsNullOrWhiteSpace(e.ApplicationScope))
                .ThenByDescending(e => !string.IsNullOrWhiteSpace(e.Language))
                .ThenByDescending(e => e.IsStarred)
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
    public string Apply(string text) => Apply(text, targetApplication: (string?)null, language: (LanguageInfo?)null);

    /// <summary>
    /// Applies personal dictionary and correction transformations with application and string language code scoping.
    /// </summary>
    public string Apply(string text, string? targetApplication = null, string? language = null)
    {
        var langInfo = !string.IsNullOrWhiteSpace(language) ? Flow.Core.Language.LanguageCatalog.GetLanguageOrDefault(language) : null;
        return Apply(text, targetApplication, langInfo);
    }

    /// <summary>
    /// Applies personal dictionary and correction transformations with application and language scoping.
    /// </summary>
    public string Apply(string text, string? targetApplication, LanguageInfo? language)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        List<DictionaryEntry> entries;
        lock (_lock)
        {
            if (_entries.Count == 0) return text;
            entries = _entries.ToList();
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

        foreach (var entry in entries)
        {
            if (!entry.IsEnabled || string.IsNullOrWhiteSpace(entry.Term)) continue;

            // 1. Language Scoping check: if entry has language specified, it must match session language
            if (!string.IsNullOrWhiteSpace(entry.Language))
            {
                if (language == null) continue;
                bool langMatches = string.Equals(entry.Language, language.Code.Value, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(entry.Language, language.WhisperCode, StringComparison.OrdinalIgnoreCase);
                if (!langMatches) continue;
            }

            // 2. Application Scoping check: if entry has app specified, it must match target application
            if (!string.IsNullOrWhiteSpace(entry.ApplicationScope))
            {
                if (string.IsNullOrWhiteSpace(cleanApp)) continue;
                string entryApp = entry.ApplicationScope.Trim();
                if (entryApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    entryApp = entryApp[..^4];
                }
                if (!string.Equals(entryApp, cleanApp, StringComparison.OrdinalIgnoreCase)) continue;
            }

            // Inviolable Zero-Enter safety invariant: flatten newlines to spaces for voice dictation
            string target = entry.EffectiveText.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
            // Unicode-safe word boundaries across Latin, Devanagari, Tamil, numbers, and symbols
            string pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(entry.Term)}(?![\p{{L}}\p{{N}}_])";
            var options = entry.CaseSensitive
                ? RegexOptions.None
                : RegexOptions.IgnoreCase;

            result = Regex.Replace(result, pattern, target, options);
        }

        return result;
    }
}
