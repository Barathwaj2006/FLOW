using System.Collections.Generic;
using Flow.Core.Language;

namespace Flow.Core.TranscriptProcessing;

/// <summary>
/// State machine tracking spoken numbered list detection.
/// </summary>
public enum ListState
{
    NotInList,
    InNumberedList
}

/// <summary>
/// Processing context maintained across multi-pass pipeline stages.
/// </summary>
public sealed class TranscriptProcessingContext
{
    public FormattingOptions Options { get; }
    public Dictionary<string, string> ProtectedTokens { get; } = new();
    public ListState CurrentListState { get; set; } = ListState.NotInList;
    public int CurrentListIndex { get; set; } = 0;
    public string? TargetApplication { get; init; }
    public LanguageInfo Language { get; init; }

    public TranscriptProcessingContext(FormattingOptions? options = null, string? targetApplication = null, LanguageInfo? language = null)
    {
        Options = options ?? new FormattingOptions();
        TargetApplication = targetApplication;
        Language = language ?? Options.Language ?? LanguageCatalog.English;
    }
}

/// <summary>
/// Composable, deterministic processing stage within the transcript pipeline.
/// </summary>
public interface ITranscriptStage
{
    /// <summary>
    /// Executes a deterministic transformation pass over the input text.
    /// </summary>
    string Process(string text, TranscriptProcessingContext context);
}
