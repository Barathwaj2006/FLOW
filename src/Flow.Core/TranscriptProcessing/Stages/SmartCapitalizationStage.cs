using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Flow.Core.Context;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Capitalizes the initial word and words following terminal sentence boundaries (. ! ?) or numbered lists.
/// Strictly respects protected tokens, code keywords in code editor context, and terminal commands.
/// </summary>
public sealed class SmartCapitalizationStage : ITranscriptStage
{
    private static readonly Regex InitialLetterRegex = new(
        @"^(?:[""'\(\[\s]*)([a-z])",
        RegexOptions.Compiled);

    private static readonly Regex SentenceBoundaryRegex = new(
        @"(?<=[.!?\u0964\u0965]\s+)([a-z])",
        RegexOptions.Compiled);

    private static readonly Regex ListItemBoundaryRegex = new(
        @"(?<=\b\d+\.\s+)([a-z])",
        RegexOptions.Compiled);

    private static readonly HashSet<string> LowercaseCodeKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "const", "let", "var", "function", "class", "interface", "import", "export",
        "async", "await", "if", "else", "for", "while", "return", "switch", "case",
        "break", "continue", "try", "catch", "finally", "throw", "new", "this",
        "typeof", "public", "private", "protected", "static", "void", "int", "string",
        "bool", "double", "float", "char", "byte", "long", "short", "null", "true",
        "false", "undefined", "nil", "def", "val", "fn", "mut", "impl", "trait",
        "struct", "enum", "type", "items", "pointer"
    };

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text) || !context.Options.CapitalizeFirstWord)
        {
            return text;
        }

        // Terminal context: do not capitalize commands (e.g. "git status", "rm -rf /")
        if (context.Options.Category == ApplicationCategory.Terminal || context.DeveloperContext.IsTerminal)
        {
            return text;
        }

        string result = text;

        // Code context: do not capitalize if starting with a lowercase code keyword, identifier, or containing code operators
        if (context.Options.Category == ApplicationCategory.Code || context.DeveloperContext.IsCodeEditor)
        {
            string firstWord = GetFirstWord(result);
            if (LowercaseCodeKeywords.Contains(firstWord) ||
                firstWord.Contains('.') || firstWord.Contains('_') || firstWord.Contains("=>") || firstWord.Contains("->") ||
                result.Contains(" == ") || result.Contains(" != ") || result.Contains(" <= ") || result.Contains(" >= ") ||
                result.Contains(" < ") || result.Contains(" > ") || result.Contains("::") || result.Contains(" => ") || result.Contains(" -> ") ||
                result.Contains(" = "))
            {
                return text;
            }
        }

        // Do not capitalize if starting with a code identifier, file tag (@app.ts), or technical placeholder
        if (!Regex.IsMatch(result, @"^(?:[""'\(\[\s]*)(?:[a-z]+[A-Z]|[a-z0-9]+_[a-z0-9_]+|[a-z0-9]+-[a-z0-9-]+|@[a-zA-Z0-9]|\uE000)"))
        {
            result = InitialLetterRegex.Replace(result, m =>
            {
                int letterIndex = m.Length - 1;
                char upper = char.ToUpperInvariant(m.Value[letterIndex]);
                return m.Value.Substring(0, letterIndex) + upper;
            });
        }

        result = SentenceBoundaryRegex.Replace(result, m => m.Value.ToUpperInvariant());
        result = ListItemBoundaryRegex.Replace(result, m => m.Value.ToUpperInvariant());

        return result;
    }

    private static string GetFirstWord(string text)
    {
        int idx = 0;
        while (idx < text.Length && (char.IsWhiteSpace(text[idx]) || text[idx] is '"' or '\'' or '`' or '(' or '['))
        {
            idx++;
        }
        int start = idx;
        while (idx < text.Length && !char.IsWhiteSpace(text[idx]) && text[idx] is not ('(' or ')' or '[' or ']' or '{' or '}' or ':' or ';'))
        {
            idx++;
        }
        return start < text.Length ? text[start..idx] : string.Empty;
    }
}
