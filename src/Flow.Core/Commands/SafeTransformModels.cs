using System;

namespace Flow.Core.Commands;

/// <summary>
/// Size tiers for selected text to protect against unbounded memory growth or accidental mass deletion.
/// </summary>
public enum SelectionSizeCategory
{
    /// <summary>Small selection (0 - 1,000 characters). Safe to transform immediately.</summary>
    Small,

    /// <summary>Medium selection (1,001 - 10,000 characters). Safe for non-destructive transforms.</summary>
    Medium,

    /// <summary>Large selection (10,001 - 100,000 characters). Requires explicit user confirmation.</summary>
    Large,

    /// <summary>Extreme selection (> 100,000 characters). Rejected to prevent memory exhaustion.</summary>
    Extreme
}

/// <summary>
/// Evaluates selection size and bounds against safety thresholds (Section 16).
/// </summary>
public static class TransformSafetyValidator
{
    public const int MaxSmallSelectionChars = 1000;
    public const int MaxMediumSelectionChars = 10000;
    public const int MaxLargeSelectionChars = 100000;

    public static SelectionSizeCategory CategorizeSelection(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return SelectionSizeCategory.Small;
        }

        int length = text.Length;
        if (length <= MaxSmallSelectionChars)
        {
            return SelectionSizeCategory.Small;
        }

        if (length <= MaxMediumSelectionChars)
        {
            return SelectionSizeCategory.Medium;
        }

        if (length <= MaxLargeSelectionChars)
        {
            return SelectionSizeCategory.Large;
        }

        return SelectionSizeCategory.Extreme;
    }

    public static CommandRisk EvaluateTransformRisk(TransformType transform, string? selectedText)
    {
        var category = CategorizeSelection(selectedText);

        return category switch
        {
            SelectionSizeCategory.Small => CommandRisk.Safe,
            SelectionSizeCategory.Medium => CommandRisk.Safe,
            SelectionSizeCategory.Large => CommandRisk.ConfirmRequired,
            SelectionSizeCategory.Extreme => CommandRisk.Blocked,
            _ => CommandRisk.Blocked
        };
    }
}

/// <summary>
/// Contract for local deterministic text transform providers.
/// Decouples transformation execution from caller context and allows future local/differentiated providers.
/// </summary>
public interface ITextTransformProvider
{
    /// <summary>
    /// Transforms the provided text deterministically according to the specified transform type.
    /// </summary>
    string Transform(string text, TransformType type);

    /// <summary>
    /// Checks whether the provider supports the given transform type.
    /// </summary>
    bool Supports(TransformType type);
}
