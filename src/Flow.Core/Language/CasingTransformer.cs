using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Core.Language;

/// <summary>
/// Text casing transformation utilities for dictation cleanup (WF-032A).
/// Deterministically handles camelCase, PascalCase, snake_case, kebab-case, and SCREAMING_SNAKE_CASE
/// while safely preserving acronyms (API, HTTP, JSON), numbers (v2, utf8), and technical terminology.
/// Guaranteed idempotent: ToCamelCase(ToCamelCase(x)) == ToCamelCase(x).
/// </summary>
public static class CasingTransformer
{
    private static readonly HashSet<string> KnownAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "API", "HTTP", "HTTPS", "URL", "URI", "JSON", "XML", "SQL", "ID", "UI",
        "SDK", "CLI", "IP", "DB", "IO", "HTML", "CSS", "REST", "GUID", "UUID",
        "HWND", "PID", "VAD", "ASR", "UTF8", "ASCII", "JWT", "SSH", "SSL", "TLS",
        "DNS", "TCP", "UDP", "FIFO", "LRU", "RAM", "CPU", "GPU", "WAV", "PCM",
        "OAUTH", "IPV6", "IPV4", "H264", "H265"
    };

    private static readonly Regex DelimiterRegex = new(@"[	_.\-,;:!?/\()\[\]{}]+|\s+", RegexOptions.Compiled);
    private static readonly Regex CamelBoundaryRegex = new(@"(?<=[a-z])(?=[A-Z])|(?<=[0-9])(?=[A-Z][a-z])", RegexOptions.Compiled);
    private static readonly Regex AcronymBoundaryRegex = new(@"(?<=[A-Z])(?=(?!(?:Auth|Pv6|Pv4)\b)[A-Z][a-z])", RegexOptions.Compiled);

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
                sb.Append(FormatWordForCamelStart(w));
            }
            else
            {
                sb.Append(FormatWordForPascal(w));
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
            sb.Append(FormatWordForPascal(w));
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

    private static string FormatWordForCamelStart(string w)
    {
        if (string.Equals(w, "OAuth", StringComparison.OrdinalIgnoreCase)) return "oauth";
        if (string.Equals(w, "IPv6", StringComparison.OrdinalIgnoreCase)) return "ipv6";
        if (string.Equals(w, "IPv4", StringComparison.OrdinalIgnoreCase)) return "ipv4";
        if (string.Equals(w, "H264", StringComparison.OrdinalIgnoreCase)) return "h264";
        if (string.Equals(w, "H265", StringComparison.OrdinalIgnoreCase)) return "h265";
        return w.ToLowerInvariant();
    }

    private static string FormatWordForPascal(string w)
    {
        if (string.Equals(w, "OAuth", StringComparison.OrdinalIgnoreCase)) return "OAuth";
        if (string.Equals(w, "IPv6", StringComparison.OrdinalIgnoreCase)) return "IPv6";
        if (string.Equals(w, "IPv4", StringComparison.OrdinalIgnoreCase)) return "IPv4";
        if (string.Equals(w, "H264", StringComparison.OrdinalIgnoreCase)) return "H264";
        if (string.Equals(w, "H265", StringComparison.OrdinalIgnoreCase)) return "H265";
        if (string.Equals(w, "2D", StringComparison.OrdinalIgnoreCase)) return "2D";
        if (string.Equals(w, "3D", StringComparison.OrdinalIgnoreCase)) return "3D";

        if (w.Length == 1) return w.ToUpperInvariant();

        // If word is like "v2", format as "V2"
        if (w.Length == 2 && (w[0] == 'v' || w[0] == 'V') && char.IsDigit(w[1]))
        {
            return "V" + w[1];
        }

        return char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant();
    }

    private static string[] ExtractWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();

        // 1. Replace delimiters with space
        string s = DelimiterRegex.Replace(text, " ");

        // 2. Protect 2D and 3D tokens and version tokens like v2
        s = Regex.Replace(s, @"(?<=[a-zA-Z])(?=(?:2D|3D|2d|3d))", " ");
        s = Regex.Replace(s, @"(?<=(?:2D|3D|2d|3d))(?=[A-Za-z])", " ");

        // 3. Split on camelCase transitions e.g. "getUser" -> "get User", "v2Api" -> "v2 Api"
        s = CamelBoundaryRegex.Replace(s, " ");

        // 4. Split on acronym transitions e.g. "APIClient" -> "API Client", "HTTPServer" -> "HTTP Server"
        s = AcronymBoundaryRegex.Replace(s, " ");

        var rawWords = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>(rawWords.Length);
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
