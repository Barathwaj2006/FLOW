using System;

namespace Flow.Core.Developer.Spans;

/// <summary>
/// Structured representation of a protected technical span.
/// Eliminates fragile string-sentinel assumptions by maintaining original value,
/// category, and location metadata.
/// </summary>
public sealed record ProtectedTechnicalSpan(
    int Id,
    TechnicalSpanCategory Category,
    string OriginalValue,
    int StartIndex,
    int Length,
    string Placeholder
);
