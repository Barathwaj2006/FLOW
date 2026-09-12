using System;
using System.Text.RegularExpressions;
using Flow.Core.Context;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Restores protected technical tokens (URLs, paths, CLI commands, identifiers)
/// and applies terminal punctuation rules (WF-033, WF-035).
/// </summary>
public sealed class EntityRestorationStage : ITranscriptStage
{
    private static readonly Regex MultipleSpacesRegex = new(@"[ \t]+", RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string result = text;

        // 1. Clean up duplicate punctuation and spacing BEFORE restoring tokens
        result = result
            .Replace("..", ".")
            .Replace(",,", ",")
            .Replace("!!", "!")
            .Replace("??", "?")
            .Replace(" .", ".")
            .Replace(" ,", ",")
            .Replace(" ?", "?")
            .Replace(" :", ":")
            .Replace(" ;", ";");
        result = Regex.Replace(result, @"\s+!(?!=)", "!");

        // 2. Restore all protected tokens
        foreach (var (placeholder, original) in context.ProtectedTokens)
        {
            result = result.Replace(placeholder, original);
        }

        // 3. Normalize multiple whitespace
        result = MultipleSpacesRegex.Replace(result, " ").Trim();

        // 4. Ensure terminal punctuation if requested
        if (context.Options.EnsureTerminalPunctuation && result.Length > 0)
        {
            // Terminal context: NEVER add a terminal period to CLI commands
            if (context.Options.Category == ApplicationCategory.Terminal || context.DeveloperContext.IsTerminal)
            {
                return result;
            }

            // File tags: NEVER add a terminal period to @app.ts
            if (result.StartsWith("@"))
            {
                return result;
            }

            // Code context: NEVER add a terminal period if the line is code (contains =>, ->, ==, !=, =, ., or is an identifier)
            if (context.Options.Category == ApplicationCategory.Code || context.DeveloperContext.IsCodeEditor)
            {
                if (!result.Contains(' ') ||
                    result.Contains("=>") || result.Contains("->") || result.Contains("==") ||
                    result.Contains("!=") || result.Contains('=') || result.Contains('.') ||
                    result.StartsWith("async ") || result.StartsWith("const ") || result.StartsWith("let ") ||
                    result.StartsWith("var ") || result.StartsWith("if ") || result.StartsWith("wrap in `"))
                {
                    return result;
                }
            }

            bool isStandaloneIdentifier = !result.Contains(' ') &&
                (Regex.IsMatch(result, @"^(?:https?://\S+|[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}|[A-Za-z]:\\[\S]+|(?:\.|\.\.)?\\[\S]+|[a-z]+[A-Z][a-zA-Z0-9]*|[a-zA-Z0-9]+_[a-zA-Z0-9_]+|[a-zA-Z0-9]+-[a-zA-Z0-9-]+|[A-Z0-9_]{2,}|@[a-zA-Z0-9_\-\.]+|[A-Z][a-z0-9_\-]+|[A-Z]{1,2}[a-zA-Z0-9]+)$"));

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
