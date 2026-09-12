using System;

namespace Flow.Core.Context;

/// <summary>
/// Immutable snapshot of the desktop UI context captured at session start.
/// </summary>
public sealed record ContextSnapshot(
    Guid SessionId,
    DateTimeOffset Timestamp,
    ForegroundTargetInfo TargetInfo,
    ApplicationCategory Category,
    FocusedControlInfo FocusedControl,
    bool IsSensitive,
    string? NearbyText,
    string? SelectionText,
    string? DocumentLanguage = null,
    float Confidence = 1.0f,
    string Source = "Windows.UIAutomation"
)
{
    public const int MaxNearbyCharacters = 200;
    public const int MaxSelectionCharacters = 10000;

    public static ContextSnapshot CreateEmpty(Guid sessionId) => new(
        sessionId,
        DateTimeOffset.UtcNow,
        ForegroundTargetInfo.Empty,
        ApplicationCategory.Unknown,
        FocusedControlInfo.Empty,
        false,
        null,
        null
    );

    public static ContextSnapshot CreateSensitive(Guid sessionId, ForegroundTargetInfo target) => new(
        sessionId,
        DateTimeOffset.UtcNow,
        target,
        ApplicationCategory.Sensitive,
        new FocusedControlInfo("PasswordBox", string.Empty, string.Empty, string.Empty, true, false, false),
        true,
        null,
        null
    );
}
