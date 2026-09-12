using System;
using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Capitalizes the initial word and words following terminal sentence boundaries (. ! ?) or numbered lists.
/// Strictly respects protected tokens (e.g., lowercase CLI commands or camelCase identifiers).
/// </summary>
public sealed class SmartCapitalizationStage : ITranscriptStage
{
    // Matches lower-case letter at start of string (ignoring leading quotes/brackets/whitespace)
    private static readonly Regex InitialLetterRegex = new(
        @"^(?:[""'\(\[\s]*)([a-z])",
        RegexOptions.Compiled);

    // Matches lower-case letter following sentence terminators (. ! ? । ॥) and whitespace
    private static readonly Regex SentenceBoundaryRegex = new(
        @"(?<=[.!?\u0964\u0965]\s+)([a-z])",
        RegexOptions.Compiled);

    // Matches lower-case letter following list item marker e.g. "1. " or "2. "
    private static readonly Regex ListItemBoundaryRegex = new(
        @"(?<=\b\d+\.\s+)([a-z])",
        RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text) || !context.Options.CapitalizeFirstWord)
        {
            return text;
        }

        string result = text;

        // 1. Initial word capitalization
        // Do not capitalize if the text starts with a code identifier (camelCase, snake_case, kebab-case, or file tag)
        if (!Regex.IsMatch(result, @"^(?:[""'\(\[\s]*)(?:[a-z]+[A-Z]|[a-z0-9]+_[a-z0-9_]+|[a-z0-9]+-[a-z0-9-]+|@[a-zA-Z0-9])"))
        {
            result = InitialLetterRegex.Replace(result, m =>
            {
                int letterIndex = m.Length - 1;
                char upper = char.ToUpperInvariant(m.Value[letterIndex]);
                return m.Value.Substring(0, letterIndex) + upper;
            });
        }

        // 2. Sentence boundary capitalization
        result = SentenceBoundaryRegex.Replace(result, m => m.Value.ToUpperInvariant());

        // 3. Numbered list item capitalization
        result = ListItemBoundaryRegex.Replace(result, m => m.Value.ToUpperInvariant());

        return result;
    }
}
