namespace Flow.Core.Commands;

/// <summary>
/// Broad category of voice command intent.
/// </summary>
public enum CommandType
{
    /// <summary>Unrecognized or ambiguous command intent; fails closed.</summary>
    Unknown,

    /// <summary>Transform currently selected text (bullets, casing, formatting, trimming).</summary>
    TransformSelection,

    /// <summary>Reversible in-editor action (undo, copy, select all, etc.).</summary>
    EditorAction,

    /// <summary>Informational query or request.</summary>
    QueryOrPrompt
}

/// <summary>
/// Specific text transformation requested for selected text.
/// </summary>
public enum TransformType
{
    /// <summary>No transform applied.</summary>
    None,

    /// <summary>Format selected items into bullet points (• or -).</summary>
    BulletList,

    /// <summary>Format selected items into numbered list (1., 2., etc.).</summary>
    NumberedList,

    /// <summary>Convert selected text to ALL UPPERCASE.</summary>
    Uppercase,

    /// <summary>Convert selected text to all lowercase.</summary>
    Lowercase,

    /// <summary>Convert selected text to Title Case.</summary>
    TitleCase,

    /// <summary>Convert selected text to camelCase.</summary>
    CamelCase,

    /// <summary>Convert selected text to snake_case.</summary>
    SnakeCase,

    /// <summary>Convert selected text to PascalCase.</summary>
    PascalCase,

    /// <summary>Convert selected text to kebab-case.</summary>
    KebabCase,

    /// <summary>Wrap selected text in quotation marks ("...").</summary>
    WrapQuotes,

    /// <summary>Wrap selected text in inline code backticks (`...`).</summary>
    WrapBackticks,

    /// <summary>Wrap selected text in a markdown code block (```...```).</summary>
    WrapCodeBlock,

    /// <summary>Collapse redundant whitespace and trim surrounding whitespace.</summary>
    TrimWhitespace,

    /// <summary>Remove filler words, repetitions, and tighten sentence structure.</summary>
    MakeConcise,

    /// <summary>Expand contractions and apply formal vocabulary styling.</summary>
    MakeFormal
}

/// <summary>
/// Safety classification verdict for voice commands.
/// Inviolable rule: Destructive shell/process executions are permanently Blocked.
/// </summary>
public enum CommandSafetyVerdict
{
    /// <summary>Safe and permitted: strictly in-editor text transformation or reversible action.</summary>
    Safe,

    /// <summary>Prohibited: potential shell execution, file system destruction, or external process launch.</summary>
    Blocked,

    /// <summary>Unrecognized command: fails closed to prevent accidental execution.</summary>
    Unknown
}
