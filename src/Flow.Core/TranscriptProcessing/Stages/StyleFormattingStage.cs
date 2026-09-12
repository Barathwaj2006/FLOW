using System;
using Flow.Core.Personalization.Styles;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage that applies writing styles, formality adjustments, and contraction policies.
/// </summary>
public sealed class StyleFormattingStage : ITranscriptStage
{
    private readonly StyleFormattingEngine _engine;

    public StyleFormattingEngine Engine => _engine;

    public StyleFormattingStage(StyleFormattingEngine? engine = null)
    {
        _engine = engine ?? new StyleFormattingEngine();
    }

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var profile = _engine.ResolveProfile(context.TargetApplication, context.Language);
        return _engine.Format(text, profile);
    }
}
