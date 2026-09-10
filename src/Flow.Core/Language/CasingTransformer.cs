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
}
