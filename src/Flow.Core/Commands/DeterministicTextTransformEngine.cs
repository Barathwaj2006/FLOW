using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Flow.Core.Language;

namespace Flow.Core.Commands;

/// <summary>
/// Deterministic text transformation engine for Selection-Aware Voice Editing (WF-037A).
/// Applies structured transforms to selected text without inventing content or hallucinating.
/// </summary>
public sealed class DeterministicTextTransformEngine : ITextTransformEngine, ITextTransformProvider
{
    private static readonly Dictionary<string, string> ContractionExpansions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "can't", "cannot" },
        { "won't", "will not" },
        { "don't", "do not" },
        { "doesn't", "does not" },
        { "didn't", "did not" },
        { "it's", "it is" },
        { "that's", "that is" },
        { "what's", "what is" },
        { "there's", "there is" },
        { "here's", "here is" },
        { "they're", "they are" },
        { "we're", "we are" },
        { "you're", "you are" },
        { "i'm", "I am" },
        { "i've", "I have" },
        { "you've", "you have" },
        { "we've", "we have" },
        { "they've", "they have" },
        { "isn't", "is not" },
        { "aren't", "are not" },
        { "wasn't", "was not" },
        { "weren't", "were not" },
        { "haven't", "have not" },
        { "hasn't", "has not" },
        { "hadn't", "had not" },
        { "wouldn't", "would not" },
        { "shouldn't", "should not" },
        { "couldn't", "could not" }
    };

    private static readonly string[] FillerWords =
    {
        "um", "uh", "like", "you know", "basically", "actually", "sort of", "kind of"
    };

    /// <inheritdoc />
    public string Transform(string selectedText, TransformType transform)
    {
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return selectedText ?? string.Empty;
        }

        return transform switch
        {
            TransformType.BulletList => FormatBulletList(selectedText),
            TransformType.NumberedList => FormatNumberedList(selectedText),
            TransformType.Uppercase => selectedText.ToUpperInvariant(),
            TransformType.Lowercase => selectedText.ToLowerInvariant(),
            TransformType.TitleCase => CasingTransformer.ToTitleCase(selectedText),
            TransformType.CamelCase => CasingTransformer.ToCamelCase(selectedText),
            TransformType.SnakeCase => CasingTransformer.ToSnakeCase(selectedText),
            TransformType.PascalCase => CasingTransformer.ToPascalCase(selectedText),
            TransformType.KebabCase => CasingTransformer.ToKebabCase(selectedText),
            TransformType.WrapQuotes => WrapInQuotes(selectedText),
            TransformType.WrapBackticks => WrapInBackticks(selectedText),
            TransformType.WrapCodeBlock => WrapInCodeBlock(selectedText),
            TransformType.TrimWhitespace => CollapseWhitespace(selectedText),
            TransformType.MakeConcise => MakeConcise(selectedText),
            TransformType.MakeFormal => MakeFormal(selectedText),
            TransformType.FixWhitespace => FixWhitespace(selectedText),
            TransformType.FixPunctuation => FixPunctuation(selectedText),
            TransformType.NormalizeSpacing => NormalizeSpacing(selectedText),
            TransformType.NormalizeQuotes => NormalizeQuotes(selectedText),
            _ => selectedText
        };
    }

    /// <inheritdoc />
    public bool Supports(TransformType type) => type != TransformType.None;


    private static string FormatBulletList(string text)
    {
        var items = ExtractListItems(text);
        if (items.Count == 0) return text;

        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            string item = CapitalizeFirstLetter(items[i].Trim());
            // Strip any pre-existing bullet or number
            item = Regex.Replace(item, @"^[-*•]\s*", "");
            item = Regex.Replace(item, @"^\d+[\.\)]\s*", "");

            if (i > 0) sb.Append(" ");
            sb.Append("• ").Append(item);
        }
        return sb.ToString();
    }

    private static string FormatNumberedList(string text)
    {
        var items = ExtractListItems(text);
        if (items.Count == 0) return text;

        var sb = new StringBuilder();
        for (int i = 0; i < items.Count; i++)
        {
            string item = CapitalizeFirstLetter(items[i].Trim());
            // Strip any pre-existing bullet or number
            item = Regex.Replace(item, @"^[-*•]\s*", "");
            item = Regex.Replace(item, @"^\d+[\.\)]\s*", "");

            if (i > 0) sb.Append(" ");
            sb.Append($"{i + 1}. ").Append(item);
        }
        return sb.ToString();
    }

    private static List<string> ExtractListItems(string text)
    {
        var rawLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (rawLines.Length > 1)
        {
            return rawLines.Select(l => l.Trim()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        // Check if text already has multiple numbered or bullet items on the same line
        if (Regex.IsMatch(text, @"\b\d+[\.\)]\s+"))
        {
            var splitNumbered = Regex.Split(text, @"(?:\s+|^)(?=\d+[\.\)]\s+)").Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (splitNumbered.Count > 0) return splitNumbered;
        }
        if (text.Contains('•') || text.Contains('-') || text.Contains('*'))
        {
            var splitBullets = Regex.Split(text, @"(?:\s+|^)(?=[-*•]\s+)").Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (splitBullets.Count > 1) return splitBullets;
        }

        // If single line with commas, split on commas if at least 2 commas present
        if (text.Count(c => c == ',') >= 2)
        {
            var commaItems = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            return commaItems.Select(c => c.Trim().TrimEnd('.')).Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        }

        // Split on sentences (. followed by space or end), but NOT on numbers like "1. "
        var sentenceMatches = Regex.Split(text, @"(?<!\b\d+)(?<=[.!?])\s+");
        var items = sentenceMatches.Select(s => s.Trim().TrimEnd('.', '!', '?')).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        if (items.Count > 0) return items;
        return new List<string> { text.Trim() };
    }

    private static string WrapInQuotes(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            return trimmed;
        }
        return $"\"{trimmed}\"";
    }

    private static string WrapInBackticks(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.StartsWith('`') && trimmed.EndsWith('`') && trimmed.Length >= 2)
        {
            return trimmed;
        }
        return $"`{trimmed}`";
    }

    private static string WrapInCodeBlock(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.StartsWith("```") && trimmed.EndsWith("```") && trimmed.Length >= 6)
        {
            return trimmed;
        }
        return $"``` {trimmed} ```";
    }

    private static string CollapseWhitespace(string text)
    {
        return Regex.Replace(text.Trim(), @"\s+", " ");
    }

    private static string MakeConcise(string text)
    {
        string result = text;

        // Remove filler words
        foreach (var filler in FillerWords)
        {
            result = Regex.Replace(result, $@"\b{Regex.Escape(filler)}\b,?\s*", "", RegexOptions.IgnoreCase);
        }

        // Remove immediate consecutive duplicate words ("the the" -> "the")
        result = Regex.Replace(result, @"\b(\w+)\s+\1\b", "$1", RegexOptions.IgnoreCase);

        // Collapse whitespace
        result = CollapseWhitespace(result);
        return CapitalizeFirstLetter(result);
    }

    private static string MakeFormal(string text)
    {
        string result = text;

        // Expand contractions
        foreach (var (contraction, expansion) in ContractionExpansions)
        {
            result = Regex.Replace(
                result,
                $@"\b{Regex.Escape(contraction)}\b",
                match =>
                {
                    // Preserve casing if capitalized
                    if (char.IsUpper(match.Value[0]))
                    {
                        return char.ToUpperInvariant(expansion[0]) + expansion[1..];
                    }
                    return expansion;
                },
                RegexOptions.IgnoreCase
            );
        }

        return CollapseWhitespace(result);
    }

    private static string CapitalizeFirstLetter(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (char.IsUpper(s[0])) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static string FixWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Normalize newlines to spaces (Zero-Enter safe) and collapse multiple spaces
        string s = Regex.Replace(text, @"[\r\n]+", " ");
        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    private static string FixPunctuation(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Remove spaces before punctuation
        string s = Regex.Replace(text, @"\s+([,.:;?!])", "$1");
        // Ensure single space after punctuation if followed by letter/digit
        s = Regex.Replace(s, @"([,.:;?!])([a-zA-Z0-9])", "$1 $2");
        // Capitalize letter following sentence punctuation
        s = Regex.Replace(s, @"(?<=[.!?]\s+)([a-z])", m => m.Value.ToUpperInvariant());
        return CapitalizeFirstLetter(s.Trim());
    }

    private static string NormalizeSpacing(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Normalize inside parentheses
        string s = Regex.Replace(text, @"\(\s+", "(");
        s = Regex.Replace(s, @"\s+\)", ")");
        // Normalize before punctuation
        s = Regex.Replace(s, @"\s+([,.:;?!])", "$1");
        s = Regex.Replace(s, @"[ \t]+", " ");
        return s.Trim();
    }

    private static string NormalizeQuotes(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Normalize smart double quotes to standard double quote
        string s = Regex.Replace(text, @"[“”„«»]", "\"");
        // Normalize smart single quotes to standard single quote
        s = Regex.Replace(s, @"[‘’‚`]", "'");
        return s.Trim();
    }
}
