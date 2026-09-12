using System;

namespace Flow.Core.Language;

/// <summary>
/// Immutable metadata record describing a supported spoken language.
/// </summary>
public sealed record LanguageInfo(
    LanguageCode Code,
    string DisplayName,
    string NativeName,
    string Script,
    bool IsRtl = false,
    bool IsMultilingualSupported = true
)
{
    public bool IsAutoDetect => Code.IsAutoDetect;
    public string WhisperCode => Code.Value;

    public override string ToString() => $"{DisplayName} ({NativeName}) [{Code}]";
}
