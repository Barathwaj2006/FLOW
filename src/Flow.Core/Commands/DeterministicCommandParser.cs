using System;
using System.Text.RegularExpressions;

namespace Flow.Core.Commands;

/// <summary>
/// Deterministic implementation of <see cref="ICommandParser"/>.
/// Maps spoken voice commands into strongly typed intents without hallucinations, guessing, or cloud LLMs.
/// Fails closed to <see cref="UnknownCommandIntent"/> for unrecognized, ambiguous, or prose-like phrases.
/// </summary>
public sealed class DeterministicCommandParser : ICommandParser
{
    private static readonly Regex ProsePrefixRegex = new(
        @"^(i (want|need|wish|prefer|think|would like)|we (should|need|want|have)|please tell|tell me|what (is|does|means)|the (meeting|file|document|session|command|system)|this (is|means|was)|there (is|are))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
    );

    /// <inheritdoc />
    public CommandIntent Parse(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return new UnknownCommandIntent(transcript ?? string.Empty, "Empty or whitespace command string.");
        }

        string raw = transcript.Trim();

        // Check if utterance is conversational prose
        if (ProsePrefixRegex.IsMatch(raw))
        {
            return new UnknownCommandIntent(raw, "Conversational or prose statement is not an imperative command.");
        }

        // Normalize: lowercase, strip trailing punctuation
        string normalized = raw.TrimEnd('.', '!', '?', ',').Trim().ToLowerInvariant();

        // Strip conversational courtesy & urgency affixes ("please", "now", "quick", "quickly")
        normalized = Regex.Replace(normalized, @"[,\s]+(please|now|quick|quickly)$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        normalized = Regex.Replace(normalized, @"^please\s+", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        normalized = normalized.TrimEnd('.', '!', '?', ',').Trim();

        // 0. Cancellation
        if (IsMatch(normalized, @"^(cancel|never mind|stop|abort|cancel command|dismiss)$"))
        {
            return new CancelCommandIntent(raw);
        }

        // 1. Text Transformation Intents
        // Bullet list
        if (IsMatch(normalized, @"^(make (this )?)?(bullet points?|bullet lists?|bullets?|turn into bullets?|format as (bullets?|bullet lists?)|add bullets?|convert to (bullets?|bullet lists?))$"))
        {
            return new TransformCommandIntent(raw, TransformType.BulletList);
        }

        // Numbered list
        if (IsMatch(normalized, @"^(make (this )?)?(numbered list|number this|number list|format as (a )?numbered list|numbers?|convert to numbers?)$"))
        {
            return new TransformCommandIntent(raw, TransformType.NumberedList);
        }

        // Uppercase / all caps
        if (IsMatch(normalized, @"^(make (this )?)?(uppercase|all caps|capitalize all|to uppercase)$"))
        {
            return new TransformCommandIntent(raw, TransformType.Uppercase);
        }

        // Lowercase
        if (IsMatch(normalized, @"^(make (this )?)?(lowercase|all lowercase|to lowercase)$"))
        {
            return new TransformCommandIntent(raw, TransformType.Lowercase);
        }

        // Title Case
        if (IsMatch(normalized, @"^(make (this )?)?(title case|capitalize words|to title case)$"))
        {
            return new TransformCommandIntent(raw, TransformType.TitleCase);
        }

        // CamelCase
        if (IsMatch(normalized, @"^(make (this )?)?(camel case|to camel case)$"))
        {
            return new TransformCommandIntent(raw, TransformType.CamelCase);
        }

        // Snake_case
        if (IsMatch(normalized, @"^(make (this )?)?(snake case|to snake case)$"))
        {
            return new TransformCommandIntent(raw, TransformType.SnakeCase);
        }

        // PascalCase
        if (IsMatch(normalized, @"^(make (this )?)?(pascal case|to pascal case)$"))
        {
            return new TransformCommandIntent(raw, TransformType.PascalCase);
        }

        // Kebab-case
        if (IsMatch(normalized, @"^(make (this )?)?(kebab case|to kebab case)$"))
        {
            return new TransformCommandIntent(raw, TransformType.KebabCase);
        }

        // Quotes
        if (IsMatch(normalized, @"^(wrap in quotes|put in quotes|add quotes|quotes?|surround with quotes)$"))
        {
            return new TransformCommandIntent(raw, TransformType.WrapQuotes);
        }

        // Backticks
        if (IsMatch(normalized, @"^(wrap in backticks|add backticks|put in backticks|inline code)$"))
        {
            return new TransformCommandIntent(raw, TransformType.WrapBackticks);
        }

        // Code block
        if (IsMatch(normalized, @"^(make (a )?)?(code block|wrap in code block)$"))
        {
            return new TransformCommandIntent(raw, TransformType.WrapCodeBlock);
        }

        // Whitespace cleanup
        if (IsMatch(normalized, @"^(trim (whitespace|spaces?)|clean up (whitespace|spaces?|text)|remove extra spaces?)$"))
        {
            return new TransformCommandIntent(raw, TransformType.TrimWhitespace);
        }

        // Concise
        if (IsMatch(normalized, @"^(make (this )?)?(concise|shorter|shorten this|summarize)$"))
        {
            return new TransformCommandIntent(raw, TransformType.MakeConcise);
        }

        // Formal
        if (IsMatch(normalized, @"^(make (this )?)?(formal|formalize|expand contractions)$"))
        {
            return new TransformCommandIntent(raw, TransformType.MakeFormal);
        }

        // Fix whitespace
        if (IsMatch(normalized, @"^(fix whitespace|normalize whitespace|clean up lines)$"))
        {
            return new TransformCommandIntent(raw, TransformType.FixWhitespace);
        }

        // Fix punctuation
        if (IsMatch(normalized, @"^(fix punctuation|normalize punctuation|clean punctuation)$"))
        {
            return new TransformCommandIntent(raw, TransformType.FixPunctuation);
        }

        // Normalize spacing
        if (IsMatch(normalized, @"^(normalize spacing|fix spacing|clean spacing)$"))
        {
            return new TransformCommandIntent(raw, TransformType.NormalizeSpacing);
        }

        // Normalize quotes
        if (IsMatch(normalized, @"^(normalize quotes|fix quotes|standardize quotes)$"))
        {
            return new TransformCommandIntent(raw, TransformType.NormalizeQuotes);
        }

        // 2. Editor Action Intents (including multilingual)
        if (IsMatch(normalized, @"^(select all|highlight all|select everything)$"))
        {
            return new EditorCommandIntent(raw, "select_all");
        }

        // Undo: English ("undo", "undo that", "revert"), Tamil ("ரத்து செய்", "தயவுசெய்து ரத்து செய்"), Hindi ("पूर्ववत करो", "कृपया पूर्ववत करो")
        if (IsMatch(normalized, @"^(undo|undo that|revert|(தயவுசெய்து\s+)?ரத்து செய்|(कृपया\s+)?पूर्ववत करो)$"))
        {
            return new EditorCommandIntent(raw, "undo");
        }

        if (IsMatch(normalized, @"^(redo|redo that)$"))
        {
            return new EditorCommandIntent(raw, "redo");
        }

        if (IsMatch(normalized, @"^(copy|copy that|copy this|copy selection)$"))
        {
            return new EditorCommandIntent(raw, "copy");
        }

        if (IsMatch(normalized, @"^(cut|cut that|cut selection)$"))
        {
            return new EditorCommandIntent(raw, "cut");
        }

        if (IsMatch(normalized, @"^(paste|paste that|insert clipboard)$"))
        {
            return new EditorCommandIntent(raw, "paste");
        }

        if (IsMatch(normalized, @"^(deselect|clear selection|unselect)$"))
        {
            return new EditorCommandIntent(raw, "deselect");
        }

        // 3. Deletion (requires confirmation)
        if (IsMatch(normalized, @"^(delete this|delete selection|remove selection|delete that)$"))
        {
            return new DeleteSelectionIntent(raw);
        }

        // 4. Allowlisted Application Launch / Focus
        var appMatch = Regex.Match(normalized, @"^(?:open|launch)\s+(?<app>[a-zA-Z0-9\s]+)$");
        if (appMatch.Success)
        {
            string candidateApp = appMatch.Groups["app"].Value.Trim();
            if (ApplicationAllowlist.TryGetAllowlistedApp(candidateApp, out var allowlisted))
            {
                return new ApplicationCommandIntent(raw, allowlisted!.DisplayName);
            }
        }

        // 5. Safe URL Navigation
        var urlMatch = Regex.Match(normalized, @"^(?:open|go to|browse to)\s+(?:website|url|site)?\s*(?<url>https?://\S+|www\.\S+|[a-zA-Z0-9\-]+\.[a-zA-Z]{2,}(?:/\S*)?)$");
        if (urlMatch.Success)
        {
            string candidateUrl = urlMatch.Groups["url"].Value.Trim();
            if (UrlSafetyValidator.TryValidateUrl(candidateUrl, out var validUri, out _))
            {
                return new UrlCommandIntent(raw, validUri!);
            }
        }

        // 6. Fail closed if not matched
        return new UnknownCommandIntent(raw, $"Phrase '{raw}' did not match any recognized voice command.");
    }

    private static bool IsMatch(string input, string pattern)
    {
        return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
