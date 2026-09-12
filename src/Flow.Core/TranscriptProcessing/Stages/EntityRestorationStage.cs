using System;
using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Restores protected technical tokens (URLs, paths, CLI commands, identifiers)
/// and applies terminal punctuation rules.
/// </summary>
public sealed class EntityRestorationStage : ITranscriptStage
{
    private static readonly Regex MultipleSpacesRegex = new(@"[ \t]+", RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string result = text;

        // 1. Clean up duplicate punctuation and spacing BEFORE restoring tokens
        // This ensures protected tokens (e.g. paths with ..\ or operators) are never corrupted.
        result = result
            .Replace("..", ".")
            .Replace(",,", ",")
            .Replace("!!", "!")
            .Replace("??", "?")
            .Replace(" .", ".")
            .Replace(" ,", ",")
            .Replace(" ?", "?")
            .Replace(" !", "!")
            .Replace(" :", ":")
            .Replace(" ;", ";");

        // 2. Restore all protected tokens
        foreach (var (placeholder, original) in context.ProtectedTokens)
        {
            result = result.Replace(placeholder, original);
        }

        // 3. Normalize multiple whitespace
        result = MultipleSpacesRegex.Replace(result, " ").Trim();

        // 4. Ensure terminal punctuation if requested (skip standalone code identifiers, URLs, emails, paths)
        if (context.Options.EnsureTerminalPunctuation && result.Length > 0)
        {
            bool isStandaloneIdentifier = !result.Contains(' ') &&
                (Regex.IsMatch(result, @"^(?:https?://\S+|[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}|[A-Za-z]:\\[\S]+|(?:\.|\.\.)?\\[\S]+|[a-z]+[A-Z][a-zA-Z0-9]*|[a-zA-Z0-9]+_[a-zA-Z0-9_]+|[a-zA-Z0-9]+-[a-zA-Z0-9-]+|[A-Z0-9_]{2,}|@[a-zA-Z0-9_\-\.]+|[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]+)+)$"));

            char lastChar = result[^1];
            if (!isStandaloneIdentifier && !IsTerminalPunctuation(lastChar))
            {
                result += ".";
            }
        }

        return result;
    }

    private static bool IsTerminalPunctuation(char c) =>
        c is '.' or '!' or '?' or ':' or ';' or '"' or '\'' or '`' or '\u0964' or '\u0965';
}
