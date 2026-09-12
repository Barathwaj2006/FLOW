using System;

namespace Flow.Core.Personalization;

/// <summary>
/// Service that generates bounded, deterministic initial prompts for Whisper ASR biasing
/// based on personalized user vocabulary, active session language, and target application context.
/// </summary>
public interface IASRBiasingService
{
    /// <summary>
    /// Builds a deterministic, bounded prompt for ASR vocabulary biasing.
    /// Strictly limits prompt length, removes control characters/newlines, and prioritizes
    /// application-specific, language-specific, and starred personal vocabulary terms.
    /// </summary>
    /// <param name="language">Active session language code (e.g. "en", "ta", "hi") or null for universal.</param>
    /// <param name="targetApplication">Target application process name (e.g. "code", "notepad") or null.</param>
    /// <param name="additionalPrompt">Optional base code-switching or system biasing prompt.</param>
    /// <returns>Sanitized, bounded prompt string suitable for Whisper.net WithPrompt(), or null if empty.</returns>
    string? BuildPrompt(string? language, string? targetApplication, string? additionalPrompt = null);
}
