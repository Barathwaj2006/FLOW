using System;
using System.Globalization;
using System.Text;

namespace Flow.Core.Language;

/// <summary>
/// Text casing transformation utilities for dictation cleanup.
/// </summary>
public static class CasingTransformer
{
    /// <summary>
    /// Converts text to Title Case (e.g. "software architecture patterns").
    /// </summary>
    public static string ToTitleCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return textInfo.ToTitleCase(text.ToLower());
    }

    /// <summary>
    /// Converts text to Sentence Case (capitalizes first letter after each period, question mark, or exclamation mark).
    /// </summary>
    public static string ToSentenceCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        var sb = new StringBuilder(text.Length);
        bool capitalizeNext = true;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (char.IsWhiteSpace(c))
            {
                sb.Append(c);
                continue;
            }

            if (capitalizeNext && char.IsLetter(c))
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }

            if (c is '.' or '!' or '?')
            {
                capitalizeNext = true;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts text to camelCase (e.g. "get user name" -> "getUserName").
    /// </summary>
    public static string ToCamelCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            if (i == 0)
            {
                sb.Append(w.ToLowerInvariant());
            }
            else
            {
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w[1..].ToLowerInvariant());
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts text to PascalCase (e.g. "user profile manager" -> "UserProfileManager").
    /// </summary>
    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (var w in words)
        {
            sb.Append(char.ToUpperInvariant(w[0]));
            if (w.Length > 1) sb.Append(w[1..].ToLowerInvariant());
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts text to snake_case (e.g. "get user name" -> "get_user_name").
    /// </summary>
    public static string ToSnakeCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var lowerWords = Array.ConvertAll(words, w => w.ToLowerInvariant());
        return string.Join('_', lowerWords);
    }

    /// <summary>
    /// Converts text to kebab-case (e.g. "user profile manager" -> "user-profile-manager").
    /// </summary>
    public static string ToKebabCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var lowerWords = Array.ConvertAll(words, w => w.ToLowerInvariant());
        return string.Join('-', lowerWords);
    }

    /// <summary>
    /// Converts text to CONSTANT_CASE (e.g. "max retry count" -> "MAX_RETRY_COUNT").
    /// </summary>
    public static string ToConstantCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var upperWords = Array.ConvertAll(words, w => w.ToUpperInvariant());
        return string.Join('_', upperWords);
    }

    private static string[] ExtractWords(string text)
    {
        // Split on whitespace, underscores, hyphens, and punctuation
        var rawWords = text.Split(new[] { ' ', '\t', '_', '-', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        foreach (var w in rawWords)
        {
            string clean = w.Trim();
            if (!string.IsNullOrEmpty(clean))
            {
                result.Add(clean);
            }
        }
        return result.ToArray();
    }
}

