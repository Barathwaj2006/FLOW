using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Normalizes whitespace and strictly guarantees the ZERO-ENTER invariant:
/// All \r, \n, and spoken "new line" / "new paragraph" phrases are converted to single spaces.
/// </summary>
public sealed class WhitespaceAndZeroEnterStage : ITranscriptStage
{
    private static readonly Regex SpokenNewlineRegex = new(
        @"\b(new line|new paragraph)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MultipleWhitespaceRegex = new(
        @"[ \t\r\n]+",
        RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Immediately replace physical carriage returns and line feeds with spaces
        string cleaned = text.Replace("\r", " ").Replace("\n", " ");

        // 2. Convert spoken newlines to spaces (never simulate Enter)
        cleaned = SpokenNewlineRegex.Replace(cleaned, " ");

        // 3. Collapse multiple whitespace
        return MultipleWhitespaceRegex.Replace(cleaned, " ").Trim();
    }
}
