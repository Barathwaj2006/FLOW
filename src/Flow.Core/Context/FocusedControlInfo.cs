using System;

namespace Flow.Core.Context;

/// <summary>
/// Information describing the currently focused UI element within the foreground window.
/// </summary>
public sealed record FocusedControlInfo(
    string ControlType,
    string AutomationId,
    string ClassName,
    string Name,
    bool IsPassword,
    bool HasTextPattern,
    bool HasValuePattern
)
{
    public static readonly FocusedControlInfo Empty = new(
        "Unknown",
        string.Empty,
        string.Empty,
        string.Empty,
        false,
        false,
        false
    );
}
