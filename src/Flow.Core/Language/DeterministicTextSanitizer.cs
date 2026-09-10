using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Core.Language;

/// <summary>
/// Deterministic speech-to-text sanitizer and language formatter.
/// Strictly enforces the ZERO-ENTER invariant: All newline, carriage return,
/// and line feed characters are permanently stripped to prevent unintentional
/// command execution, message sends, or form submissions.
/// </summary>
public sealed class DeterministicTextSanitizer : ILanguageEngine
{
    private static readonly Regex MultipleSpacesRegex = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex FillerWordsRegex = new(@"\b(um|uh|erm|ah)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // Spoken punctuation replacements
    private static readonly (Regex Pattern, string Replacement)[] SpokenPunctuation = new[]
    {
        (new Regex(@"\s+\bperiod\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "."),
        (new Regex(@"\s+\bfull stop\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "."),
        (new Regex(@"\s+\bcomma\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ","),
        (new Regex(@"\s+\bquestion mark\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "?"),
        (new Regex(@"\s+\bexclamation point\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "!"),
        (new Regex(@"\s+\bexclamation mark\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "!"),
        (new Regex(@"\s+\bcolon\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ":"),
        (new Regex(@"\s+\bsemicolon\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), ";"),
        // Crucial: "new line" or "new paragraph" commands are mapped to a space to NEVER simulate Enter
        (new Regex(@"\s+\bnew line\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), " "),
        (new Regex(@"\s+\bnew paragraph\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), " ")
    };

    /// <inheritdoc />
    public string Format(string rawText, FormattingOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        options ??= new FormattingOptions();

        // 1. INVIOLABLE SAFETY: Strip all newlines and carriage returns immediately
        string text = rawText.Replace("\r", " ").Replace("\n", " ");

        // 2. Remove filler words if enabled
        if (options.RemoveFillerWords)
        {
            text = FillerWordsRegex.Replace(text, string.Empty);
        }

        // 3. Spoken punctuation substitution if enabled
        if (options.ReplaceSpokenPunctuation)
        {
            foreach (var (pattern, replacement) in SpokenPunctuation)
            {
                text = pattern.Replace(text, replacement);
            }
        }

        // 4. Normalize multiple whitespaces into a single space
        text = MultipleSpacesRegex.Replace(text, " ").Trim();

        // Fix spaces before punctuation (e.g. "hello ." -> "hello.")
        text = text.Replace(" .", ".")
                   .Replace(" ,", ",")
                   .Replace(" ?", "?")
                   .Replace(" !", "!")
                   .Replace(" :", ":")
                   .Replace(" ;", ";");

        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // 5. Initial capitalization
        if (options.CapitalizeFirstWord && char.IsLower(text[0]))
        {
            text = char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        // 6. Ensure terminal punctuation if enabled
        if (options.EnsureTerminalPunctuation && text.Length > 0)
        {
            char lastChar = text[^1];
            if (!IsTerminalPunctuation(lastChar))
            {
                text += ".";
            }
        }

        return text;
    }

    private static bool IsTerminalPunctuation(char c) =>
        c is '.' or '!' or '?' or ':' or ';' or '"' or '\'' or '`';
}
