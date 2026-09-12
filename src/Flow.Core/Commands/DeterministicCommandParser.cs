using System;
using System.Text.RegularExpressions;

namespace Flow.Core.Commands;

/// <summary>
/// Deterministic implementation of <see cref="ICommandParser"/>.
/// Maps spoken voice commands into strongly typed intents without hallucinations or guessing.
/// Fails closed to <see cref="UnknownCommandIntent"/> for unrecognized phrases.
/// </summary>
public sealed class DeterministicCommandParser : ICommandParser
{
    /// <inheritdoc />
    public CommandIntent Parse(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return new UnknownCommandIntent(transcript ?? string.Empty, "Empty or whitespace command string.");
        }

        string raw = transcript.Trim();
        // Normalize: lowercase, strip punctuation
        string normalized = raw.TrimEnd('.', '!', '?', ',').Trim().ToLowerInvariant();

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

        // 2. Editor Action Intents
        if (IsMatch(normalized, @"^(select all|highlight all)$"))
        {
            return new EditorCommandIntent(raw, "select_all");
        }

        if (IsMatch(normalized, @"^(undo|undo that|revert)$"))
        {
            return new EditorCommandIntent(raw, "undo");
        }

        if (IsMatch(normalized, @"^(redo|redo that)$"))
        {
            return new EditorCommandIntent(raw, "redo");
        }

        if (IsMatch(normalized, @"^(copy|copy that|copy selection)$"))
        {
            return new EditorCommandIntent(raw, "copy");
        }

        if (IsMatch(normalized, @"^(cut|cut that|cut selection)$"))
        {
            return new EditorCommandIntent(raw, "cut");
        }

        if (IsMatch(normalized, @"^(paste|paste that)$"))
        {
            return new EditorCommandIntent(raw, "paste");
        }

        if (IsMatch(normalized, @"^(deselect|clear selection|unselect)$"))
        {
            return new EditorCommandIntent(raw, "deselect");
        }

        // 3. Fail closed if not matched
        return new UnknownCommandIntent(raw, $"Phrase '{raw}' did not match any recognized voice command.");
    }

    private static bool IsMatch(string input, string pattern)
    {
        return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
