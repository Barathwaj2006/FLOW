using Flow.Core.TranscriptProcessing;
using Flow.Core.TranscriptProcessing.Stages;
using Xunit;

namespace Flow.Core.Tests.TranscriptProcessing;

public class VoiceFileTaggingStageTests
{
    private readonly VoiceFileTaggingStage _stage = new();
    private readonly TranscriptProcessingContext _context = new();

    [Theory]
    [InlineData("open at app dot ts", "open @app.ts")]
    [InlineData("look at index dot js", "look @index.js")]
    [InlineData("modify at program dot cs", "modify @program.cs")]
    [InlineData("run at test script dot py", "run @testScript.py")]
    [InlineData("check at config dot json", "check @config.json")]
    [InlineData("edit at style dot css", "edit @style.css")]
    [InlineData("see at readme dot md", "see @readme.md")]
    public void Process_StandardFileExtensions_ConvertsToTags(string input, string expected)
    {
        string result = _stage.Process(input, _context);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Process_WithSpokenUnderscore_ProducesUnderscoreInFilename()
    {
        string input = "inspect at user underscore profile dot cs";
        string result = _stage.Process(input, _context);
        Assert.Equal("inspect @user_profile.cs", result);
    }

    [Fact]
    public void Process_WithSpokenHyphen_ProducesHyphenInFilename()
    {
        string input = "check at utils hyphen helper dot py";
        string result = _stage.Process(input, _context);
        Assert.Equal("check @utils-helper.py", result);
    }

    [Fact]
    public void Process_WithNumbersInFilename_PreservesNumbers()
    {
        string input = "open at migration 001 dot sql";
        string result = _stage.Process(input, _context);
        Assert.Equal("open @migration001.sql", result);
    }

    [Theory]
    [InlineData("meet me at two dot five")]
    [InlineData("we arrived at five PM")]
    [InlineData("look at the whiteboard")]
    [InlineData("aim at three dot one four")]
    public void Process_OrdinaryProseWithAt_PreservesOriginalText(string input)
    {
        string result = _stage.Process(input, _context);
        Assert.Equal(input, result);
    }

    [Fact]
    public void FullPipeline_FileTagging_PreservesZeroEnterInvariant()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "open at app dot ts\r\ncheck at config dot json";
        string output = pipeline.Format(input);

        Assert.False(output.Contains('\r'), "Output contained \\r carriage return!");
        Assert.False(output.Contains('\n'), "Output contained \\n newline!");
        Assert.Contains("@app.ts", output);
        Assert.Contains("@config.json", output);
    }
}
