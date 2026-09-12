using System;

namespace Flow.Core.Personalization.Styles;

/// <summary>
/// Contraction handling policy for transcript formatting.
/// </summary>
public enum ContractionPolicy
{
    Preserve = 0,
    Expand = 1,
    Contract = 2
}

/// <summary>
/// Formality level for voice transcript styling.
/// </summary>
public enum FormalityLevel
{
    Casual = 0,
    Balanced = 1,
    Formal = 2
}

/// <summary>
/// Represents a writing style profile (e.g., Default, Personal, Work, Email, Technical, Casual).
/// </summary>
public sealed class StyleProfile
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ContractionPolicy ContractionPolicy { get; set; } = ContractionPolicy.Preserve;
    public FormalityLevel FormalityLevel { get; set; } = FormalityLevel.Balanced;
    public bool UseBulletPoints { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maps a Windows application process name to a specific StyleProfile.
/// </summary>
public sealed class AppStyleMapping
{
    public string ProcessName { get; init; } = string.Empty;
    public string StyleProfileId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
