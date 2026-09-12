namespace Flow.Core.Commands;

/// <summary>
/// Parser interface for converting spoken command transcripts into typed <see cref="CommandIntent"/>.
/// Fails closed for unknown or ambiguous speech input.
/// </summary>
public interface ICommandParser
{
    /// <summary>
    /// Parses a raw or sanitized voice transcript into a typed command intent.
    /// </summary>
    CommandIntent Parse(string transcript);
}
