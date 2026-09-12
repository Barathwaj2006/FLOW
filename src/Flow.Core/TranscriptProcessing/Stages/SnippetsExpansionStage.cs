using System;
using Flow.Core.Personalization.Snippets;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage that expands spoken voice trigger phrases into rich text snippets.
/// </summary>
public sealed class SnippetsExpansionStage : ITranscriptStage
{
    private readonly SnippetExpansionEngine _engine;

    public SnippetExpansionEngine Engine => _engine;

    public SnippetsExpansionStage(SnippetExpansionEngine? engine = null)
    {
        _engine = engine ?? new SnippetExpansionEngine();
    }

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return _engine.Expand(text);
    }
}
