namespace Flow.Core.Commands;

/// <summary>
/// Engine for applying deterministic text transformations to selected text.
/// Supports bullet points, numbering, code casing, quotes, and structural formatting (WF-037A).
/// </summary>
public interface ITextTransformEngine
{
    /// <summary>
    /// Transforms the specified input text according to the target <see cref="TransformType"/>.
    /// </summary>
    /// <param name="selectedText">The text currently selected in the focused application.</param>
    /// <param name="transform">The requested transform type.</param>
    /// <returns>The transformed text ready for insertion.</returns>
    string Transform(string selectedText, TransformType transform);
}
