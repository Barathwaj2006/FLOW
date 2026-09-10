using Flow.Core.Language;
using Xunit;

namespace Flow.Core.Tests.Language;

public class DeterministicTextSanitizerTests
{
    private readonly DeterministicTextSanitizer _sanitizer = new();

    [Fact]
    public void ZeroEnter_StripsAllCarriageReturnsAndLineFeeds()
    {
        string rawInput = "First line\r\nSecond line\nThird line\rFourth line";
        string formatted = _sanitizer.Format(rawInput);

        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);
        Assert.Equal("First line Second line Third line Fourth line.", formatted);
    }

    [Fact]
    public void SpokenNewLine_MapsToSpaceInsteadOfEnter()
    {
        string rawInput = "send the email new line immediately";
        string formatted = _sanitizer.Format(rawInput);

        Assert.DoesNotContain("\n", formatted);
        Assert.DoesNotContain("\r", formatted);
        Assert.Equal("Send the email immediately.", formatted);
    }

    [Fact]
    public void CapitalizeFirstWord_CapitalizesLowercaseStart()
    {
        string rawInput = "this is a native Windows app";
        string formatted = _sanitizer.Format(rawInput);

        Assert.Equal("This is a native Windows app.", formatted);
    }

    [Fact]
    public void TerminalPunctuation_AppendsPeriodIfMissing()
    {
        string rawInput = "Ship the product";
        string formatted = _sanitizer.Format(rawInput);

        Assert.EndsWith(".", formatted);
    }

    [Fact]
    public void TerminalPunctuation_PreservesExistingPunctuation()
    {
        string question = "Is this ready?";
        string formattedQuestion = _sanitizer.Format(question);
        Assert.Equal("Is this ready?", formattedQuestion);

        string exclamation = "Awesome!";
        string formattedExclamation = _sanitizer.Format(exclamation);
        Assert.Equal("Awesome!", formattedExclamation);
    }

    [Fact]
    public void SpokenPunctuation_ConvertsWordsToSymbols()
    {
        string rawInput = "Hello comma how are you question mark";
        string formatted = _sanitizer.Format(rawInput);

        Assert.Equal("Hello, how are you?", formatted);
    }

    [Fact]
    public void FillerWords_RemovesUmAndUh()
    {
        string rawInput = "Um we should uh proceed with the build";
        string formatted = _sanitizer.Format(rawInput);

        Assert.Equal("We should proceed with the build.", formatted);
    }

    [Fact]
    public void ContentLock_ProtectsTechnicalIdentifiersAndNegativeConstraints()
    {
        string rawInput = "do not use Firebase and configure C:\\Windows\\System32 with port 8080";
        string formatted = _sanitizer.Format(rawInput);

        // Content lock guarantees: negative constraint "do not" protected, path protected, port number protected
        Assert.Contains("Do not use Firebase", formatted);
        Assert.Contains("C:\\Windows\\System32", formatted);
        Assert.Contains("8080", formatted);
    }

    [Fact]
    public void WhitespaceNormalization_CollapsesMultipleSpaces()
    {
        string rawInput = "Too    many     spaces   here";
        string formatted = _sanitizer.Format(rawInput);

        Assert.Equal("Too many spaces here.", formatted);
    }
}
