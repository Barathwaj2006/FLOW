using System;
using System.Collections.Generic;
using Flow.Core.Language;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.TranscriptProcessing.Stages;

namespace Flow.Core.TranscriptProcessing;

/// <summary>
/// Production multi-pass transcript processing pipeline.
/// Orchestrates deterministic stages for whitespace normalization, entity protection,
/// spoken punctuation, snippets expansion, personal dictionary & corrections, filler removal,
/// numbered lists, style formatting, smart capitalization, and entity restoration.
/// 
/// Strictly guarantees the ZERO-ENTER invariant:
/// No \r, \n, or Enter simulation will ever be emitted.
/// </summary>
public sealed class TranscriptProcessingPipeline : ILanguageEngine
{
    private readonly IReadOnlyList<ITranscriptStage> _stages;

    public IReadOnlyList<ITranscriptStage> Stages => _stages;

    public TranscriptProcessingPipeline(IReadOnlyList<ITranscriptStage>? stages = null)
    {
        _stages = stages ?? CreateDefaultStages();
    }

    public TranscriptProcessingPipeline(
        PersonalDictionaryEngine dictionaryEngine,
        SnippetExpansionEngine? snippetEngine = null,
        StyleFormattingEngine? styleEngine = null)
    {
        _stages = CreateDefaultStages(dictionaryEngine, snippetEngine, styleEngine);
    }

    private static IReadOnlyList<ITranscriptStage> CreateDefaultStages(
        PersonalDictionaryEngine? dictionaryEngine = null,
        SnippetExpansionEngine? snippetEngine = null,
        StyleFormattingEngine? styleEngine = null)
    {
        return new ITranscriptStage[]
        {
            new WhitespaceAndZeroEnterStage(),
            new TechnicalEntityProtectionStage(),
            new SpokenPunctuationStage(),
            new SnippetsExpansionStage(snippetEngine),
            new PersonalDictionaryStage(dictionaryEngine),
            new ConservativeFillerRemovalStage(),
            new NumberedListStage(),
            new SpokenCasingStage(),
            new VoiceFileTaggingStage(),
            new StyleFormattingStage(styleEngine),
            new SmartCapitalizationStage(),
            new EntityRestorationStage()
        };
    }

    /// <inheritdoc />
    public string Format(string rawText, FormattingOptions? options = null)
    {
        return Format(rawText, options, targetApplication: options?.TargetApplication);
    }

    /// <summary>
    /// Processes raw transcript with target application context.
    /// </summary>
    public string Format(string rawText, string? targetApplication)
    {
        return Format(rawText, options: null, targetApplication);
    }

    /// <summary>
    /// Processes raw transcript with optional formatting options and target application context.
    /// </summary>
    public string Format(string rawText, FormattingOptions? options, string? targetApplication)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var context = new TranscriptProcessingContext(options, targetApplication);
        string currentText = rawText;

        foreach (var stage in _stages)
        {
            currentText = stage.Process(currentText, context);
            if (string.IsNullOrWhiteSpace(currentText))
            {
                return string.Empty;
            }
        }

        // Inviolable Zero-Enter guarantee: Post-pipeline verification (FAIL CLOSED)
        if (currentText.Contains('\r') || currentText.Contains('\n'))
        {
            throw new InvalidOperationException("CRITICAL SAFETY VIOLATION: Zero-Enter invariant violated — newline detected post-pipeline.");
        }

        return currentText;
    }
}
