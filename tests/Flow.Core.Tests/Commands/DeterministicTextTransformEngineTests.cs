using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class DeterministicTextTransformEngineTests
{
    private readonly DeterministicTextTransformEngine _engine = new();

    [Fact]
    public void Transform_BulletList_MultilineText_Formatted()
    {
        string input = "First task\r\nSecond task\r\nThird task";
        string result = _engine.Transform(input, TransformType.BulletList);

        Assert.Equal("• First task • Second task • Third task", result);
    }

    [Fact]
    public void Transform_BulletList_CommaSeparated_Formatted()
    {
        string input = "apples, bananas, oranges";
        string result = _engine.Transform(input, TransformType.BulletList);

        Assert.Equal("• Apples • Bananas • Oranges", result);
    }

    [Fact]
    public void Transform_NumberedList_MultilineText_Formatted()
    {
        string input = "First item\nSecond item\nThird item";
        string result = _engine.Transform(input, TransformType.NumberedList);

        Assert.Equal("1. First item 2. Second item 3. Third item", result);
    }

    [Fact]
    public void Transform_NumberedList_CommaSeparated_Formatted()
    {
        string input = "review code, run tests, deploy service";
        string result = _engine.Transform(input, TransformType.NumberedList);

        Assert.Equal("1. Review code 2. Run tests 3. Deploy service", result);
    }

    [Fact]
    public void Transform_Uppercase_ConvertsAllCharacters()
    {
        string input = "Flow Windows Desktop Voice Engine";
        string result = _engine.Transform(input, TransformType.Uppercase);

        Assert.Equal("FLOW WINDOWS DESKTOP VOICE ENGINE", result);
    }

    [Fact]
    public void Transform_Lowercase_ConvertsAllCharacters()
    {
        string input = "FLOW Windows Desktop Voice Engine";
        string result = _engine.Transform(input, TransformType.Lowercase);

        Assert.Equal("flow windows desktop voice engine", result);
    }

    [Fact]
    public void Transform_TitleCase_CapitalizesWords()
    {
        string input = "flow windows voice engine";
        string result = _engine.Transform(input, TransformType.TitleCase);

        Assert.Equal("Flow Windows Voice Engine", result);
    }

    [Theory]
    [InlineData("get user name", "getUserName")]
    [InlineData("active process id", "activeProcessId")]
    public void Transform_CamelCase_ProducesIdentifier(string input, string expected)
    {
        string result = _engine.Transform(input, TransformType.CamelCase);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("get user name", "get_user_name")]
    [InlineData("active process id", "active_process_id")]
    public void Transform_SnakeCase_ProducesIdentifier(string input, string expected)
    {
        string result = _engine.Transform(input, TransformType.SnakeCase);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user profile manager", "UserProfileManager")]
    [InlineData("auth token service", "AuthTokenService")]
    public void Transform_PascalCase_ProducesIdentifier(string input, string expected)
    {
        string result = _engine.Transform(input, TransformType.PascalCase);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("user profile manager", "user-profile-manager")]
    [InlineData("build output dir", "build-output-dir")]
    public void Transform_KebabCase_ProducesIdentifier(string input, string expected)
    {
        string result = _engine.Transform(input, TransformType.KebabCase);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Transform_WrapQuotes_WrapsUnquotedText()
    {
        string input = "important quote";
        string result = _engine.Transform(input, TransformType.WrapQuotes);

        Assert.Equal("\"important quote\"", result);
    }

    [Fact]
    public void Transform_WrapQuotes_AlreadyQuoted_DoesNotDoubleQuote()
    {
        string input = "\"already quoted\"";
        string result = _engine.Transform(input, TransformType.WrapQuotes);

        Assert.Equal("\"already quoted\"", result);
    }

    [Fact]
    public void Transform_WrapBackticks_WrapsText()
    {
        string input = "npm install whisper.net";
        string result = _engine.Transform(input, TransformType.WrapBackticks);

        Assert.Equal("`npm install whisper.net`", result);
    }

    [Fact]
    public void Transform_WrapCodeBlock_WrapsText()
    {
        string input = "Console.WriteLine(42);";
        string result = _engine.Transform(input, TransformType.WrapCodeBlock);

        Assert.Equal("``` Console.WriteLine(42); ```", result);
    }

    [Fact]
    public void Transform_TrimWhitespace_CollapsesSpaces()
    {
        string input = "   Too   many    spaces   between    words   ";
        string result = _engine.Transform(input, TransformType.TrimWhitespace);

        Assert.Equal("Too many spaces between words", result);
    }

    [Fact]
    public void Transform_MakeConcise_RemovesFillersAndDuplicates()
    {
        string input = "um basically we should the the fix this actually";
        string result = _engine.Transform(input, TransformType.MakeConcise);

        Assert.Equal("We should the fix this", result);
    }

    [Fact]
    public void Transform_MakeFormal_ExpandsContractions()
    {
        string input = "We can't do that, it's not ready and they won't agree.";
        string result = _engine.Transform(input, TransformType.MakeFormal);

        Assert.Equal("We cannot do that, it is not ready and they will not agree.", result);
    }

    [Fact]
    public void Transform_EmptyOrWhitespace_ReturnsUnchanged()
    {
        Assert.Equal("", _engine.Transform("", TransformType.BulletList));
        Assert.Equal("   ", _engine.Transform("   ", TransformType.Uppercase));
    }
}
