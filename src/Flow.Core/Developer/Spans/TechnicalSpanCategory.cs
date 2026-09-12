using System;

namespace Flow.Core.Developer.Spans;

/// <summary>
/// Categories of protected technical spans (WF-033).
/// Enables strongly-typed preservation of technical syntax tokens without corruption.
/// </summary>
public enum TechnicalSpanCategory
{
    LanguageToken,
    FrameworkToken,
    RuntimeToken,
    LibraryToken,
    AcronymToken,
    VersionToken,
    IdentifierToken,
    PathToken,
    FlagToken,
    URLToken,
    NamespaceToken,
    EnvVarToken,
    PackageToken
}
