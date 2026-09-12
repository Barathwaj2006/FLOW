using System;
using System.Collections.Generic;

namespace Flow.Core.ASR;

/// <summary>
/// A recognized segment within transcribed speech.
/// </summary>
public sealed record ASRSegment(
    string Text,
    double StartSeconds,
    double EndSeconds,
    float Confidence = 1.0f
);

/// <summary>
/// Final result of Automatic Speech Recognition.
/// </summary>
public sealed record ASRResult(
    string Text,
    float Confidence,
    TimeSpan AudioDuration,
    TimeSpan InferenceDuration,
    string EngineId,
    IReadOnlyList<ASRSegment> Segments,
    string? DetectedLanguage = null,
    float? LanguageConfidence = null
)
{
    public static ASRResult Empty(string engineId = "none") =>
        new(string.Empty, 1.0f, TimeSpan.Zero, TimeSpan.Zero, engineId, Array.Empty<ASRSegment>());
}

/// <summary>
/// Configuration options passed to an ASR engine for transcription.
/// </summary>
public sealed record ASROptions(
    string Language = "en",
    string? Prompt = null,
    float Temperature = 0.0f,
    bool EnableTimestamps = false
);

/// <summary>
/// Metadata describing an ASR engine backend.
/// </summary>
public sealed record ASREngineInfo(
    string Id,
    string DisplayName,
    string Version,
    bool IsAvailable,
    bool RequiresGpu,
    string ModelName
);

/// <summary>
/// Exception thrown during speech recognition processing.
/// </summary>
public sealed class ASRException : Exception
{
    public string EngineId { get; }

    public ASRException(string engineId, string message) : base($"[{engineId}] {message}")
    {
        EngineId = engineId;
    }

    public ASRException(string engineId, string message, Exception innerException) : base($"[{engineId}] {message}", innerException)
    {
        EngineId = engineId;
    }
}
