using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Personalization.Styles;

/// <summary>
/// Engine for applying writing styles, tone formality adjustments, and contraction policies
/// to voice transcripts based on the active or application-mapped style profile.
/// </summary>
public sealed class StyleFormattingEngine
{
    private readonly IStyleRepository? _repository;
    private readonly object _lock = new();

    private Dictionary<string, StyleProfile> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _appMappings = new(StringComparer.OrdinalIgnoreCase);
    private StyleProfile _activeProfile;

    // Contraction expansion map: "don't" -> "do not"
    private static readonly (string Contracted, string Expanded)[] Contractions = new[]
    {
        ("don't", "do not"),
        ("doesn't", "does not"),
        ("didn't", "did not"),
        ("can't", "cannot"),
        ("won't", "will not"),
        ("couldn't", "could not"),
        ("shouldn't", "should not"),
        ("wouldn't", "would not"),
        ("isn't", "is not"),
        ("aren't", "are not"),
        ("wasn't", "was not"),
        ("weren't", "were not"),
        ("haven't", "have not"),
        ("hasn't", "has not"),
        ("hadn't", "had not"),
        ("i'm", "I am"),
        ("you're", "you are"),
        ("he's", "he is"),
        ("she's", "she is"),
        ("it's", "it is"),
        ("we're", "we are"),
        ("they're", "they are"),
        ("i've", "I have"),
        ("you've", "you have"),
        ("we've", "we have"),
        ("they've", "they have"),
        ("i'll", "I will"),
        ("you'll", "you will"),
        ("he'll", "he will"),
        ("she'll", "she will"),
        ("we'll", "we will"),
        ("they'll", "they will"),
        ("let's", "let us")
    };

    // Formality normalization: casual spoken terms -> formal written equivalents
    private static readonly (string Casual, string Formal)[] FormalitySubstitutions = new[]
    {
        ("gonna", "going to"),
        ("wanna", "want to"),
        ("gotta", "need to"),
        ("kinda", "rather"),
        ("sorta", "somewhat"),
        ("yeah", "yes"),
        ("yep", "yes"),
        ("nope", "no")
    };

    public StyleProfile ActiveProfile
    {
        get { lock (_lock) return _activeProfile; }
        set { lock (_lock) _activeProfile = value ?? throw new ArgumentNullException(nameof(value)); }
    }

    public StyleFormattingEngine(IStyleRepository? repository = null, StyleProfile? defaultProfile = null)
    {
        _repository = repository;
        _activeProfile = defaultProfile ?? new StyleProfile
        {
            Id = "style_default",
            Name = "Default",
            ContractionPolicy = ContractionPolicy.Preserve,
            FormalityLevel = FormalityLevel.Balanced
        };
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        if (_repository == null) return;

        var profiles = await _repository.GetAllProfilesAsync(ct);
        var mappings = await _repository.GetAllAppMappingsAsync(ct);

        lock (_lock)
        {
            _profiles = profiles.ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
            _appMappings = mappings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            if (_profiles.TryGetValue(_activeProfile.Id, out var current))
            {
                _activeProfile = current;
            }
        }
    }

    /// <summary>
    /// Resolves the effective style profile for a target application or returns the active profile.
    /// </summary>
    public StyleProfile ResolveProfile(string? targetApplication)
    {
        lock (_lock)
        {
            if (!string.IsNullOrWhiteSpace(targetApplication))
            {
                // Normalize app name (e.g., "code.exe" -> "code")
                string cleanApp = targetApplication.Trim();
                if (cleanApp.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    cleanApp = cleanApp[..^4];
                }

                if (_appMappings.TryGetValue(cleanApp, out var styleId) &&
                    _profiles.TryGetValue(styleId, out var mappedProfile))
                {
                    return mappedProfile;
                }
            }

            return _activeProfile;
        }
    }

    /// <summary>
    /// Formats the input text according to the specified or resolved style profile.
    /// </summary>
    public string Format(string text, StyleProfile? profile = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var effective = profile ?? ActiveProfile;
        string result = text;

        // 1. Contraction policy
        if (effective.ContractionPolicy == ContractionPolicy.Expand)
        {
            foreach (var (contracted, expanded) in Contractions)
            {
                // Word-boundary case-preserving replacement
                string pattern = $@"(?<!\w){Regex.Escape(contracted)}(?!\w)";
                result = Regex.Replace(result, pattern, m =>
                {
                    bool isUpper = char.IsUpper(m.Value[0]);
                    if (isUpper && expanded.Length > 0)
                    {
                        return char.ToUpper(expanded[0]) + expanded[1..];
                    }
                    return expanded;
                }, RegexOptions.IgnoreCase);
            }
        }
        else if (effective.ContractionPolicy == ContractionPolicy.Contract)
        {
            foreach (var (contracted, expanded) in Contractions)
            {
                string pattern = $@"(?<!\w){Regex.Escape(expanded)}(?!\w)";
                result = Regex.Replace(result, pattern, m =>
                {
                    bool isUpper = char.IsUpper(m.Value[0]);
                    if (isUpper && contracted.Length > 0)
                    {
                        return char.ToUpper(contracted[0]) + contracted[1..];
                    }
                    return contracted;
                }, RegexOptions.IgnoreCase);
            }
        }

        // 2. Formality level substitutions
        if (effective.FormalityLevel == FormalityLevel.Formal)
        {
            foreach (var (casual, formal) in FormalitySubstitutions)
            {
                string pattern = $@"(?<!\w){Regex.Escape(casual)}(?!\w)";
                result = Regex.Replace(result, pattern, m =>
                {
                    bool isUpper = char.IsUpper(m.Value[0]);
                    if (isUpper && formal.Length > 0)
                    {
                        return char.ToUpper(formal[0]) + formal[1..];
                    }
                    return formal;
                }, RegexOptions.IgnoreCase);
            }
        }

        return result;
    }
}
