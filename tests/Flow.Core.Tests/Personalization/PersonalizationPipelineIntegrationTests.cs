using System;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Personalization.Styles;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Personalization;

public sealed class PersonalizationPipelineIntegrationTests
{
    [Fact]
    public void Pipeline_ExpandsSnippet_AndAppliesDictionaryCorrection()
    {
        var dictEngine = new PersonalDictionaryEngine();
        dictEngine.SetEntries(new[]
        {
            new DictionaryEntry { Term = "anti gravity", Replacement = "Antigravity" }
        });

        var snippetEngine = new SnippetExpansionEngine();
        snippetEngine.SetSnippets(new[]
        {
            new SnippetEntry { TriggerPhrase = "my email", ExpansionText = "barat@example.com" }
        });

        var styleEngine = new StyleFormattingEngine();

        var pipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine, styleEngine);

        string input = "hello please send anti gravity notes to my email period";
        string formatted = pipeline.Format(input);

        Assert.Equal("Hello please send Antigravity notes to barat@example.com.", formatted);
    }

    [Fact]
    public void Pipeline_AppliesTargetAppStyle_InCombinationWithPersonalization()
    {
        var dictEngine = new PersonalDictionaryEngine();
        var snippetEngine = new SnippetExpansionEngine();
        var styleEngine = new StyleFormattingEngine();

        // Configure a Formal profile that expands contractions
        var formalProfile = new StyleProfile
        {
            Id = "style_formal",
            Name = "Formal",
            ContractionPolicy = ContractionPolicy.Expand,
            FormalityLevel = FormalityLevel.Formal
        };
        styleEngine.ActiveProfile = formalProfile;

        var pipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine, styleEngine);

        string raw = "I don't think we're gonna fail this release period";
        string formatted = pipeline.Format(raw);

        // "don't" -> "do not", "we're" -> "we are", "gonna" -> "going to", "period" -> "."
        Assert.Equal("I do not think we are going to fail this release.", formatted);
    }

    [Fact]
    public void Pipeline_GuaranteesZeroEnter_EvenIfSnippetContainsNewlines()
    {
        var dictEngine = new PersonalDictionaryEngine();
        var snippetEngine = new SnippetExpansionEngine();
        snippetEngine.SetSnippets(new[]
        {
            new SnippetEntry
            {
                TriggerPhrase = "my template",
                ExpansionText = "Line 1\r\nLine 2\nLine 3"
            }
        });
        var styleEngine = new StyleFormattingEngine();

        var pipeline = new TranscriptProcessingPipeline(dictEngine, snippetEngine, styleEngine);

        string raw = "start my template finish";
        string formatted = pipeline.Format(raw);

        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
        Assert.Equal("Start Line 1 Line 2 Line 3 finish.", formatted);
    }
}
