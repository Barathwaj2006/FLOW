using System;
using Flow.Core.Language;

namespace Flow.Core.Context;

/// <summary>
/// Strongly-typed developer context model (WF-035).
/// Derived deterministically from desktop UI context without global booleans.
/// </summary>
public sealed record DeveloperContext(
    string? Application,
    ApplicationCategory Category,
    LanguageInfo? Language,
    IdentifierCasingStyle PreferredCasing = IdentifierCasingStyle.None,
    bool IsCodeEditor = false,
    bool IsTerminal = false,
    string? FileContext = null,
    float Confidence = 1.0f
)
{
    public static readonly DeveloperContext Empty = new(
        Application: null,
        Category: ApplicationCategory.Unknown,
        Language: null,
        PreferredCasing: IdentifierCasingStyle.None,
        IsCodeEditor: false,
        IsTerminal: false,
        FileContext: null,
        Confidence: 1.0f
    );

    public static DeveloperContext FromSnapshot(ContextSnapshot? snapshot, LanguageInfo? language = null)
    {
        if (snapshot == null) return Empty;

        bool isCode = snapshot.Category == ApplicationCategory.Code;
        bool isTerminal = snapshot.Category == ApplicationCategory.Terminal;

        return new DeveloperContext(
            Application: snapshot.TargetInfo.ProcessName,
            Category: snapshot.Category,
            Language: language,
            PreferredCasing: isCode ? IdentifierCasingStyle.CamelCase : IdentifierCasingStyle.None,
            IsCodeEditor: isCode,
            IsTerminal: isTerminal,
            FileContext: snapshot.DocumentLanguage,
            Confidence: snapshot.Confidence
        );
    }
}
