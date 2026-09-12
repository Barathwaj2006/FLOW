namespace Flow.Core.Context;

/// <summary>
/// Broad category of the active foreground application, used to adapt dictation and formatting rules.
/// </summary>
public enum ApplicationCategory
{
    Unknown = 0,
    GeneralProse = 1,
    Document = 2,
    Browser = 3,
    Code = 4,
    Terminal = 5,
    Sensitive = 6
}
