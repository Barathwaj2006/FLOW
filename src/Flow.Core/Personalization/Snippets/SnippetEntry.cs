using System;

namespace Flow.Core.Personalization.Snippets;

/// <summary>
/// Represents a voice snippet mapping: a short spoken trigger phrase
/// that expands into rich boilerplate text (up to 4,000 characters).
/// </summary>
public sealed class SnippetEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string TriggerPhrase { get; set; } = string.Empty;
    public string ExpansionText { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Language { get; set; }
    public string? ApplicationScope { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
