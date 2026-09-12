using Flow.Core.TranscriptProcessing;
using Flow.Core.TranscriptProcessing.Stages;
using Xunit;

namespace Flow.Core.Tests.TranscriptProcessing;

public class SpokenCasingStageTests
{
    private readonly SpokenCasingStage _stage = new();
    private readonly TranscriptProcessingContext _context = new();

    [Fact]
    public void Process_CamelCaseTrigger_TransformsTargetPhrase()
    {
        string input = "camel case get user name";
        string result = _stage.Process(input, _context);
        Assert.Equal("getUserName", result);
    }

    [Fact]
    public void Process_SnakeCaseTrigger_TransformsTargetPhrase()
    {
        string input = "snake case get user name";
        string result = _stage.Process(input, _context);
        Assert.Equal("get_user_name", result);
    }

    [Fact]
    public void Process_PascalCaseTrigger_TransformsTargetPhrase()
    {
        string input = "pascal case user profile manager";
        string result = _stage.Process(input, _context);
        Assert.Equal("UserProfileManager", result);
    }

    [Fact]
    public void Process_KebabCaseTrigger_TransformsTargetPhrase()
    {
        string input = "kebab case user profile manager";
        string result = _stage.Process(input, _context);
        Assert.Equal("user-profile-manager", result);
    }

    [Fact]
    public void Process_ConstantCaseTrigger_TransformsTargetPhrase()
    {
        string input = "constant case max retry count";
        string result = _stage.Process(input, _context);
        Assert.Equal("MAX_RETRY_COUNT", result);
    }

    [Fact]
    public void Process_EmbeddedInProse_TransformsOnlyTargetClause()
    {
        string input = "please implement camel case get user name and return it";
        string result = _stage.Process(input, _context);
        Assert.Equal("please implement getUserName and return it", result);
    }

    [Fact]
    public void Process_FollowedByPunctuation_PreservesPunctuation()
    {
        string input = "create snake case user profile, then run tests.";
        string result = _stage.Process(input, _context);
        Assert.Equal("create user_profile, then run tests.", result);
    }

    [Fact]
    public void Process_WithoutTrigger_PreservesOriginalProse()
    {
        string input = "this is an ordinary discussion about corner cases and edge cases";
        string result = _stage.Process(input, _context);
        Assert.Equal(input, result);
    }

    [Fact]
    public void FullPipeline_SpokenCasing_PreservesZeroEnterInvariant()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "camel case get user name\r\nsnake case set user name";
        string output = pipeline.Format(input);

        Assert.False(output.Contains('\r'), "Output contained \\r carriage return!");
        Assert.False(output.Contains('\n'), "Output contained \\n newline!");
        Assert.Contains("getUserName", output);
    }
}
