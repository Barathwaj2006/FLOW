using System;

namespace Flow.Core.Personalization.Dictionary;

/// <summary>
/// Represents an entry in the user's personal dictionary:
/// custom vocabulary terms, jargon, acronyms, or misrecognition replacement rules.
/// </summary>
public sealed class DictionaryEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Term { get; set; } = string.Empty;
    public string? Replacement { get; set; }
    public bool IsStarred { get; set; }
    public string? Category { get; set; }
    public bool CaseSensitive { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Effective replacement text to inject. If Replacement is not specified, uses Term.
    /// </summary>
    public string EffectiveText => string.IsNullOrWhiteSpace(Replacement) ? Term : Replacement;

    /// <summary>
    /// True if this entry represents a phonetic/misrecognition correction (Term -> Replacement).
    /// </summary>
    public bool IsCorrection => !string.IsNullOrWhiteSpace(Replacement) && !Replacement.Equals(Term, StringComparison.Ordinal);
}
