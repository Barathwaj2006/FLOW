using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.TranscriptProcessing;

public class TranscriptProcessingPipelineTests
{
    private readonly TranscriptProcessingPipeline _pipeline = new();

    [Fact]
    public void ZeroEnter_StripsAllPhysicalNewlines()
    {
        string rawInput = "Alpha line\r\nBeta line\nGamma line\rDelta line";
        string formatted = _pipeline.Format(rawInput);

        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
        Assert.Equal("Alpha line Beta line Gamma line Delta line.", formatted);
    }

    [Fact]
    public void SpokenNewline_MapsToSpaceInsteadOfEnter()
    {
        string rawInput = "send the email new line immediately";
        string formatted = _pipeline.Format(rawInput);

        Assert.DoesNotContain("\n", formatted);
        Assert.DoesNotContain("\r", formatted);
        Assert.Equal("Send the email immediately.", formatted);
    }

    [Fact]
    public void SpokenPunctuation_ReplacesCommandsWithSymbols()
    {
        string rawInput = "hello period how are you question mark";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("Hello. How are you?", formatted);
    }

    [Fact]
    public void SpokenPunctuation_QuotesAndParentheses_HandledCorrectly()
    {
        string rawInput = "open quote important close quote open parenthesis secret close parenthesis";
        string formatted = _pipeline.Format(rawInput);

        Assert.Contains("\"Important\"", formatted);
        Assert.Contains("(secret)", formatted);
    }

    [Fact]
    public void ConservativeFiller_RemovesHesitations_PreservesLikeAsVerb()
    {
        string rawInput = "um we should uh use Python and I like Python";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("We should use Python and I like Python.", formatted);
    }

    [Fact]
    public void ConservativeFiller_PreservesLikeInNaturalPhrases()
    {
        string rawInput = "it feels like summer and looks like rain";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("It feels like summer and looks like rain.", formatted);
    }

    [Fact]
    public void ConservativeFiller_RemovesDisfluentLikeSurroundedByCommas()
    {
        string rawInput = "they are comma like comma really efficient";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("They are really efficient.", formatted);
    }

    [Fact]
    public void NumberedList_CardinalSequence_FormatsCorrectly()
    {
        string rawInput = "one buy milk two finish report three test code";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("1. Buy milk 2. Finish report 3. Test code.", formatted);
    }

    [Fact]
    public void NumberedList_OrdinalSequence_FormatsCorrectly()
    {
        string rawInput = "first check logs second run tests third deploy";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("1. Check logs 2. Run tests 3. Deploy.", formatted);
    }

    [Fact]
    public void NumberedList_SingleItem_RejectsFalsePositive()
    {
        string rawInput = "one important thing to remember";
        string formatted = _pipeline.Format(rawInput);

        // Crucial: Single "one" without a confirmed "two" must NOT format as list
        Assert.DoesNotContain("1.", formatted);
        Assert.Equal("One important thing to remember.", formatted);
    }

    [Fact]
    public void NumberedList_NoItemOne_RejectsFalsePositive()
    {
        string rawInput = "I have two dogs and three cats";
        string formatted = _pipeline.Format(rawInput);

        Assert.DoesNotContain("2.", formatted);
        Assert.DoesNotContain("3.", formatted);
        Assert.Equal("I have two dogs and three cats.", formatted);
    }

    [Fact]
    public void TechnicalTokens_WindowsPaths_Preserved()
    {
        string rawInput = @"save the config to C:\Windows\System32\drivers\etc\hosts";
        string formatted = _pipeline.Format(rawInput);

        Assert.Contains(@"C:\Windows\System32\drivers\etc\hosts", formatted);
    }

    [Fact]
    public void TechnicalTokens_CliCommands_Preserved()
    {
        string rawInput = "execute dotnet test and check git status";
        string formatted = _pipeline.Format(rawInput);

        Assert.Contains("dotnet test", formatted);
        Assert.Contains("git status", formatted);
    }

    [Fact]
    public void TechnicalTokens_CodeIdentifiersAndUrls_Preserved()
    {
        string rawInput = "visit https://flow.dev and review Flow.Core and camelCaseVar";
        string formatted = _pipeline.Format(rawInput);

        Assert.Contains("https://flow.dev", formatted);
        Assert.Contains("Flow.Core", formatted);
        Assert.Contains("camelCaseVar", formatted);
    }

    [Fact]
    public void TechnicalTokens_Acronyms_PreservedInUppercase()
    {
        string rawInput = "configure HTTP and WASAPI with GPU and AVX2";
        string formatted = _pipeline.Format(rawInput);

        Assert.Contains("HTTP", formatted);
        Assert.Contains("WASAPI", formatted);
        Assert.Contains("GPU", formatted);
        Assert.Contains("AVX2", formatted);
    }

    [Fact]
    public void SmartCapitalization_CapitalizesAfterTerminalPunctuation()
    {
        string rawInput = "hello period how are you question mark yes comma I am fine exclamation mark";
        string formatted = _pipeline.Format(rawInput);

        Assert.Equal("Hello. How are you? Yes, I am fine!", formatted);
    }

    [Fact]
    public void TerminalPunctuation_AppendsMissingPeriod()
    {
        string rawInput = "ship the native Windows app";
        string formatted = _pipeline.Format(rawInput);

        Assert.EndsWith(".", formatted);
    }

    [Fact]
    public void TerminalPunctuation_PreservesExistingQuestionsAndExclamations()
    {
        string question = "is this ready question mark";
        string formattedQuestion = _pipeline.Format(question);
        Assert.Equal("Is this ready?", formattedQuestion);

        string exclamation = "this is amazing exclamation mark";
        string formattedExclamation = _pipeline.Format(exclamation);
        Assert.Equal("This is amazing!", formattedExclamation);
    }
}
