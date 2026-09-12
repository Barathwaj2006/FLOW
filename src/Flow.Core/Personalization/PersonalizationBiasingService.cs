using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Flow.Core.Personalization.Dictionary;

namespace Flow.Core.Personalization;

/// <summary>
/// Production implementation of IASRBiasingService.
/// Constructs bounded, deterministic, and injection-safe initial prompts for Whisper ASR.
/// </summary>
public sealed class PersonalizationBiasingService : IASRBiasingService
{
    private readonly IPersonalDictionaryRepository? _repository;
    private readonly PersonalDictionaryEngine? _engine;
    private readonly int _maxPromptCharacters;
    private readonly int _maxTerms;

    public PersonalizationBiasingService(
        PersonalDictionaryEngine? engine = null,
        IPersonalDictionaryRepository? repository = null,
        int maxPromptCharacters = 200,
        int maxTerms = 15)
    {
        _engine = engine;
        _repository = repository;
        _maxPromptCharacters = maxPromptCharacters > 0 ? maxPromptCharacters : 200;
        _maxTerms = maxTerms > 0 ? maxTerms : 15;
    }

    /// <inheritdoc />
    public string? BuildPrompt(string? language, string? targetApplication, string? additionalPrompt = null)
    {
        // 1. Retrieve candidates from engine cache or repository
        IReadOnlyList<DictionaryEntry> candidates = _engine?.GetEntries() ?? Array.Empty<DictionaryEntry>();

        string? cleanApp = null;
        if (!string.IsNullOrWhiteSpace(targetApplication))
        {
            cleanApp = targetApplication.Trim();
            if (cleanApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                cleanApp = cleanApp[..^4];
            }
        }

        // 2. Filter by enabled, language, and application scope
        var filtered = candidates.Where(e =>
        {
            if (!e.IsEnabled || string.IsNullOrWhiteSpace(e.EffectiveText)) return false;

            // Language check
            if (!string.IsNullOrWhiteSpace(e.Language) && !string.IsNullOrWhiteSpace(language))
            {
                if (!string.Equals(e.Language, language, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Application check
            if (!string.IsNullOrWhiteSpace(e.ApplicationScope))
            {
                if (string.IsNullOrWhiteSpace(cleanApp)) return false;
                string entryApp = e.ApplicationScope.Trim();
                if (entryApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    entryApp = entryApp[..^4];
                }
                if (!string.Equals(entryApp, cleanApp, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        })
        .OrderByDescending(e => !string.IsNullOrWhiteSpace(e.ApplicationScope))
        .ThenByDescending(e => e.IsStarred)
        .ThenBy(e => e.EffectiveText, StringComparer.OrdinalIgnoreCase)
        .ToList();

        var sb = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 3. Include sanitized additional prompt if present
        if (!string.IsNullOrWhiteSpace(additionalPrompt))
        {
            string sanitizedBase = SanitizeToken(additionalPrompt);
            if (!string.IsNullOrWhiteSpace(sanitizedBase))
            {
                if (sanitizedBase.Length <= _maxPromptCharacters)
                {
                    sb.Append(sanitizedBase);
                    foreach (var part in sanitizedBase.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        seen.Add(part);
                    }
                }
            }
        }

        // 4. Bounded term packing
        int termCount = 0;
        foreach (var entry in filtered)
        {
            if (termCount >= _maxTerms) break;

            string token = SanitizeToken(entry.EffectiveText);
            if (token.Length < 2 || token.Length > 40) continue;
            if (!seen.Add(token)) continue;

            int additionLength = (sb.Length > 0 ? 2 : 0) + token.Length;
            if (sb.Length + additionLength > _maxPromptCharacters)
            {
                break;
            }

            if (sb.Length > 0)
            {
                sb.Append(", ");
            }
            sb.Append(token);
            termCount++;
        }

        string result = sb.ToString().Trim();
        return string.IsNullOrEmpty(result) ? null : result;
    }

    private static string SanitizeToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // Remove newlines, carriage returns, tabs, quotes, and control characters to prevent prompt injection
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (c is '\r' or '\n' or '\t' or '"' or '\'' or '`' or char.MinValue)
            {
                sb.Append(' ');
            }
            else if (!char.IsControl(c))
            {
                sb.Append(c);
            }
        }

        // Collapse multiple spaces
        string cleaned = sb.ToString();
        while (cleaned.Contains("  "))
        {
            cleaned = cleaned.Replace("  ", " ");
        }

        return cleaned.Trim();
    }
}
