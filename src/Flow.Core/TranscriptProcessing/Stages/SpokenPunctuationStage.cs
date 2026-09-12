using System;
using System.Text.RegularExpressions;
using Flow.Core.Context;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Replaces spoken punctuation commands with appropriate typographical symbols.
/// Handles spacing around punctuation and quotes naturally.
/// </summary>
public sealed class SpokenPunctuationStage : ITranscriptStage
{
    private static readonly Regex OpenQuoteRegex = new(@"(?:\s+|^)\bopen quote\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CloseQuoteRegex = new(@"\s*\bclose quote\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OpenParenRegex = new(@"(?:\s+|^)\b(open parenthesis|open paren)\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CloseParenRegex = new(@"\s*\b(close parenthesis|close paren)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OpenBracketRegex = new(@"(?:\s+|^)\b(open bracket|open square bracket)\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CloseBracketRegex = new(@"\s*\b(close bracket|close square bracket)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex OpenBraceRegex = new(@"(?:\s+|^)\b(open brace|open curly brace)\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CloseBraceRegex = new(@"\s*\b(close brace|close curly brace)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly (Regex Pattern, string Replacement)[] PunctuationRules = new[]
    {
        // Multi-word punctuation commands
        (new Regex(@"(?:\s+|^)\b(question mark)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "?"),
        (new Regex(@"(?:\s+|^)\b(exclamation mark|exclamation point)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "!"),
        (new Regex(@"(?:\s+|^)\b(full stop)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "."),

        // Single-word punctuation commands
        (new Regex(@"(?:\s+|^)\bperiod\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "."),
        (new Regex(@"(?:\s+|^)\bcomma\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ","),
        (new Regex(@"(?:\s+|^)\bcolon\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ":"),
        (new Regex(@"(?:\s+|^)\bsemicolon\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ";"),
        (new Regex(@"\s*\b(dash|hyphen)\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled), "-"),
        (new Regex(@"\s*\bapostrophe\b\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled), "'")
    };

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text) || !context.Options.ReplaceSpokenPunctuation)
        {
            return text;
        }

        string result = text;

        // Quotes, parentheses, brackets, and braces with boundary-aware spacing
        result = OpenQuoteRegex.Replace(result, m => m.Value.StartsWith(" ") ? " \"" : "\"");
        result = CloseQuoteRegex.Replace(result, "\"");
        result = OpenParenRegex.Replace(result, m => m.Value.StartsWith(" ") ? " (" : "(");
        result = CloseParenRegex.Replace(result, ")");
        result = OpenBracketRegex.Replace(result, m => m.Value.StartsWith(" ") ? " [" : "[");
        result = CloseBracketRegex.Replace(result, "]");
        result = OpenBraceRegex.Replace(result, m => m.Value.StartsWith(" ") ? " {" : "{");
        result = CloseBraceRegex.Replace(result, "}");

        foreach (var (pattern, replacement) in PunctuationRules)
        {
            result = pattern.Replace(result, replacement);
        }

        // Clean up duplicate punctuation and spacing around punctuation
        result = result
            .Replace("..", ".")
            .Replace(",,", ",")
            .Replace("!!", "!")
            .Replace("??", "?");

        if (context.Options.Category != ApplicationCategory.Terminal && !context.DeveloperContext.IsTerminal)
        {
            result = result.Replace(" .", ".");
        }
        else
        {
            result = Regex.Replace(result, @"\s+\.(?=[a-zA-Z0-9])", ".");
        }

        result = result
            .Replace(" ,", ",")
            .Replace(" ?", "?")
            .Replace(" :", ":")
            .Replace(" ;", ";");
        result = Regex.Replace(result, @"\s+!(?!=)", "!");
        result = result
            .Replace("( ", "(")
            .Replace(" )", ")")
            .Replace("[ ", "[")
            .Replace(" ]", "]")
            .Replace("{ ", "{")
            .Replace(" }", "}");
        result = Regex.Replace(result, @"(?<=[a-zA-Z])\s+'\s*(?=[a-zA-Z])", "'");
        result = result.Trim();

        return result;
    }
}
