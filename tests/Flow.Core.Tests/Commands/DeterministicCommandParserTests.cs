using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class DeterministicCommandParserTests
{
    private readonly DeterministicCommandParser _parser = new();

    [Theory]
    [InlineData("make bullet points", TransformType.BulletList)]
    [InlineData("turn into bullets", TransformType.BulletList)]
    [InlineData("bullet list", TransformType.BulletList)]
    [InlineData("bullet points", TransformType.BulletList)]
    [InlineData("bullets", TransformType.BulletList)]
    [InlineData("format as bullets", TransformType.BulletList)]
    [InlineData("add bullets", TransformType.BulletList)]
    [InlineData("convert to bullets", TransformType.BulletList)]
    [InlineData("Make bullet points.", TransformType.BulletList)]
    public void Parse_BulletListCommands_Recognized(string input, TransformType expected)
    {
        var intent = _parser.Parse(input);
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(expected, transformIntent.Transform);
        Assert.Equal(CommandType.TransformSelection, transformIntent.Type);
    }

    [Theory]
    [InlineData("numbered list", TransformType.NumberedList)]
    [InlineData("number this", TransformType.NumberedList)]
    [InlineData("make numbered list", TransformType.NumberedList)]
    [InlineData("format as a numbered list", TransformType.NumberedList)]
    [InlineData("convert to numbers", TransformType.NumberedList)]
    public void Parse_NumberedListCommands_Recognized(string input, TransformType expected)
    {
        var intent = _parser.Parse(input);
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(expected, transformIntent.Transform);
    }

    [Theory]
    [InlineData("make uppercase", TransformType.Uppercase)]
    [InlineData("all caps", TransformType.Uppercase)]
    [InlineData("uppercase", TransformType.Uppercase)]
    [InlineData("capitalize all", TransformType.Uppercase)]
    [InlineData("make lowercase", TransformType.Lowercase)]
    [InlineData("all lowercase", TransformType.Lowercase)]
    [InlineData("lowercase", TransformType.Lowercase)]
    [InlineData("title case", TransformType.TitleCase)]
    [InlineData("make title case", TransformType.TitleCase)]
    [InlineData("capitalize words", TransformType.TitleCase)]
    public void Parse_StandardCasingCommands_Recognized(string input, TransformType expected)
    {
        var intent = _parser.Parse(input);
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(expected, transformIntent.Transform);
    }

    [Theory]
    [InlineData("camel case", TransformType.CamelCase)]
    [InlineData("make camel case", TransformType.CamelCase)]
    [InlineData("snake case", TransformType.SnakeCase)]
    [InlineData("make snake case", TransformType.SnakeCase)]
    [InlineData("pascal case", TransformType.PascalCase)]
    [InlineData("make pascal case", TransformType.PascalCase)]
    [InlineData("kebab case", TransformType.KebabCase)]
    [InlineData("make kebab case", TransformType.KebabCase)]
    public void Parse_DeveloperCasingCommands_Recognized(string input, TransformType expected)
    {
        var intent = _parser.Parse(input);
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(expected, transformIntent.Transform);
    }

    [Theory]
    [InlineData("wrap in quotes", TransformType.WrapQuotes)]
    [InlineData("put in quotes", TransformType.WrapQuotes)]
    [InlineData("add quotes", TransformType.WrapQuotes)]
    [InlineData("wrap in backticks", TransformType.WrapBackticks)]
    [InlineData("inline code", TransformType.WrapBackticks)]
    [InlineData("code block", TransformType.WrapCodeBlock)]
    [InlineData("make code block", TransformType.WrapCodeBlock)]
    [InlineData("trim whitespace", TransformType.TrimWhitespace)]
    [InlineData("clean up spaces", TransformType.TrimWhitespace)]
    [InlineData("make concise", TransformType.MakeConcise)]
    [InlineData("shorten this", TransformType.MakeConcise)]
    [InlineData("make formal", TransformType.MakeFormal)]
    [InlineData("expand contractions", TransformType.MakeFormal)]
    public void Parse_FormattingAndWrappingCommands_Recognized(string input, TransformType expected)
    {
        var intent = _parser.Parse(input);
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(expected, transformIntent.Transform);
    }

    [Theory]
    [InlineData("select all", "select_all")]
    [InlineData("highlight all", "select_all")]
    [InlineData("undo", "undo")]
    [InlineData("undo that", "undo")]
    [InlineData("revert", "undo")]
    [InlineData("redo", "redo")]
    [InlineData("copy", "copy")]
    [InlineData("cut", "cut")]
    [InlineData("paste", "paste")]
    [InlineData("deselect", "deselect")]
    [InlineData("clear selection", "deselect")]
    public void Parse_EditorActions_Recognized(string input, string expectedAction)
    {
        var intent = _parser.Parse(input);
        var editorIntent = Assert.IsType<EditorCommandIntent>(intent);
        Assert.Equal(expectedAction, editorIntent.ActionName);
        Assert.Equal(CommandType.EditorAction, editorIntent.Type);
    }

    [Theory]
    [InlineData("order pizza from domino's")]
    [InlineData("send email to boss")]
    [InlineData("random gibberish phrase")]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_UnrecognizedPhrases_FailsClosedToUnknownIntent(string input)
    {
        var intent = _parser.Parse(input);
        var unknown = Assert.IsType<UnknownCommandIntent>(intent);
        Assert.Equal(CommandType.Unknown, unknown.Type);
        Assert.False(string.IsNullOrWhiteSpace(unknown.FailureReason));
    }
}
