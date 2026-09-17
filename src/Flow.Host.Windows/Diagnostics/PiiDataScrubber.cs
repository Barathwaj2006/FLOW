using System;
using System.Text.RegularExpressions;

namespace Flow.Host.Windows.Diagnostics;

/// <summary>
/// High-performance deterministic data scrubber that strips PII, authentication tokens,
/// Windows account usernames, and user dictation transcripts from persistent diagnostic logs.
/// Guarantees compliance with FLOW's 100% offline privacy and sensitive data redaction rule.
/// </summary>
public static class PiiDataScrubber
{
    private static readonly Regex BearerTokenRegex = new(
        @"Bearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ApiKeysRegex = new(
        @"\b(?:sk-[A-Za-z0-9]{20,}|AIza[0-9A-Za-z\-_]{35}|AKIA[0-9A-Z]{16}|ghp_[A-Za-z0-9]{36})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex EmailRegex = new(
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d{4}[ -]?){3}\d{4}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex UserProfilePathRegex = new(
        @"(?i)\b[a-zA-Z]:\\Users\\([^\\]+)\\",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TranscriptPayloadRegex = new(
        @"(?i)(transcript|transcription|dictation|recognized text|user text)\s*[:=]\s*(?:""[^""]*""|'[^']*'|[^\r\n,;]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Scrubs sensitive identifiers, credentials, user profile paths, and speech transcripts from a log message.
    /// </summary>
    public static string Scrub(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return message ?? string.Empty;

        // 1. Scrub Bearer tokens & API Keys
        string scrubbed = BearerTokenRegex.Replace(message, "Bearer [REDACTED_API_KEY]");
        scrubbed = ApiKeysRegex.Replace(scrubbed, "[REDACTED_API_KEY]");

        // 2. Scrub emails
        scrubbed = EmailRegex.Replace(scrubbed, "[REDACTED_EMAIL]");

        // 3. Scrub credit cards
        scrubbed = CreditCardRegex.Replace(scrubbed, "[REDACTED_CARD]");

        // 4. Scrub transcript payloads if logged
        scrubbed = TranscriptPayloadRegex.Replace(scrubbed, "$1: [REDACTED_TRANSCRIPT]");

        // 5. Scrub Windows user names in file paths
        scrubbed = UserProfilePathRegex.Replace(scrubbed, "C:\\Users\\[REDACTED_USER]\\");

        return scrubbed;
    }
}
