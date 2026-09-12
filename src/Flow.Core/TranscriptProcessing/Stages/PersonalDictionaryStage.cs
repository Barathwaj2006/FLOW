using System;
using Flow.Core.Personalization.Dictionary;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage that applies personal dictionary custom vocabulary and misrecognition correction rules.
/// </summary>
public sealed class PersonalDictionaryStage : ITranscriptStage
{
    private readonly PersonalDictionaryEngine _engine;

    public PersonalDictionaryEngine Engine => _engine;

    public PersonalDictionaryStage(PersonalDictionaryEngine? engine = null)
    {
        _engine = engine ?? new PersonalDictionaryEngine();
    }

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return _engine.Apply(text, context.TargetApplication, context.Language);
    }
}
