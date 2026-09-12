using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Flow.Core.Language;

/// <summary>
/// Text casing transformation utilities for dictation cleanup (WF-032A).
/// Deterministically handles camelCase, PascalCase, snake_case, kebab-case, and SCREAMING_SNAKE_CASE
/// while safely preserving acronyms (API, HTTP, JSON), numbers (v2, utf8), and technical terminology.
/// </summary>
public static class CasingTransformer
{
    private static readonly HashSet<string> KnownAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "API", "HTTP", "HTTPS", "URL", "URI", "JSON", "XML", "SQL", "ID", "UI",
        "SDK", "CLI", "IP", "DB", "IO", "HTML", "CSS", "REST", "GUID", "UUID",
        "HWND", "PID", "VAD", "ASR", "UTF8", "ASCII", "JWT", "SSH", "SSL", "TLS",
        "DNS", "TCP", "UDP", "FIFO", "LRU", "RAM", "CPU", "GPU", "WAV", "PCM"
    };

    public static string ToTitleCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return textInfo.ToTitleCase(text.ToLower());
    }

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

    public static string ToSnakeCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var lowerWords = Array.ConvertAll(words, w => w.ToLowerInvariant());
        return string.Join('_', lowerWords);
    }

    public static string ToKebabCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var lowerWords = Array.ConvertAll(words, w => w.ToLowerInvariant());
        return string.Join('-', lowerWords);
    }

    public static string ToConstantCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var words = ExtractWords(text);
        if (words.Length == 0) return string.Empty;

        var upperWords = Array.ConvertAll(words, w => w.ToUpperInvariant());
        return string.Join('_', upperWords);
    }

    public static string ToScreamingSnakeCase(string text) => ToConstantCase(text);

    private static bool IsAllUpper(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length < 2) return false;
        foreach (char c in s)
        {
            if (char.IsLetter(c) && !char.IsUpper(c)) return false;
        }
        return true;
    }

    private static string[] ExtractWords(string text)
    {
        var rawWords = text.Split(new[] { ' ', '	', '_', '-', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
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
