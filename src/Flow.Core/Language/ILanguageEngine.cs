using System;
using Flow.Core.Context;

namespace Flow.Core.Language;

/// <summary>
/// Options for text formatting.
/// </summary>
public sealed record FormattingOptions(
    bool CapitalizeFirstWord = true,
    bool EnsureTerminalPunctuation = true,
    bool RemoveFillerWords = true,
    bool ReplaceSpokenPunctuation = true,
    bool FormatNumberedLists = true,
    LanguageInfo? Language = null,
    string? TargetApplication = null,
    ApplicationCategory Category = ApplicationCategory.Unknown,
    string? NearbyContext = null,
    DeveloperContext? DeveloperContext = null
);

/// <summary>
/// Abstraction for language formatting and sanitization.
/// </summary>
public interface ILanguageEngine
{
    /// <summary>
    /// Deterministically sanitizes and formats speech-to-text output.
    /// Strictly guarantees ZERO newline/enter characters are emitted.
    /// </summary>
    /// <param name="rawText">Raw transcription from ASR engine.</param>
    /// <param name="options">Formatting options.</param>
    /// <returns>Sanitized, safe string suitable for cursor insertion.</returns>
    string Format(string rawText, FormattingOptions? options = null);
}
