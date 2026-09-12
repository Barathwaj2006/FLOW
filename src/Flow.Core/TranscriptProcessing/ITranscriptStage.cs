using System.Collections.Generic;
using Flow.Core.Context;
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
    public DeveloperContext DeveloperContext { get; init; }

    public TranscriptProcessingContext(FormattingOptions? options = null, string? targetApplication = null, LanguageInfo? language = null)
    {
        Options = options ?? new FormattingOptions();
        TargetApplication = targetApplication ?? Options.TargetApplication;
        Language = language ?? Options.Language ?? LanguageCatalog.English;
        DeveloperContext = Options.DeveloperContext ?? new DeveloperContext(
            Application: TargetApplication,
            Category: Options.Category,
            Language: Language,
            PreferredCasing: Options.Category == ApplicationCategory.Code ? IdentifierCasingStyle.CamelCase : IdentifierCasingStyle.None,
            IsCodeEditor: Options.Category == ApplicationCategory.Code,
            IsTerminal: Options.Category == ApplicationCategory.Terminal
        );
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
