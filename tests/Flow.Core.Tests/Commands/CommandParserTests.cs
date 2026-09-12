using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandParserTests
{
    private readonly DeterministicCommandParser _parser = new();

    [Theory]
    [InlineData("copy", "copy")]
    [InlineData("copy this", "copy")]
    [InlineData("copy that", "copy")]
    [InlineData("copy selection", "copy")]
    [InlineData("paste", "paste")]
    [InlineData("paste that", "paste")]
    [InlineData("insert clipboard", "paste")]
    [InlineData("undo", "undo")]
    [InlineData("undo that", "undo")]
    [InlineData("revert", "undo")]
    [InlineData("redo", "redo")]
    [InlineData("redo that", "redo")]
    [InlineData("select all", "select_all")]
    [InlineData("highlight all", "select_all")]
    [InlineData("deselect", "deselect")]
    [InlineData("clear selection", "deselect")]
    public void Parse_EditorActions_RecognizedCorrectly(string input, string expectedAction)
    {
        var intent = _parser.Parse(input);
        var editorIntent = Assert.IsType<EditorCommandIntent>(intent);
        Assert.Equal(expectedAction, editorIntent.ActionName);
    }

    [Theory]
    [InlineData("ரத்து செய்", "undo")]
    [InlineData("தயவுசெய்து ரத்து செய்", "undo")]
    [InlineData("पूर्ववत करो", "undo")]
    [InlineData("कृपया पूर्ववत करो", "undo")]
    public void Parse_MultilingualUndo_RecognizedCorrectly(string input, string expectedAction)
    {
        var intent = _parser.Parse(input);
        var editorIntent = Assert.IsType<EditorCommandIntent>(intent);
        Assert.Equal(expectedAction, editorIntent.ActionName);
    }

    [Theory]
    [InlineData("make bullet points", TransformType.BulletList)]
    [InlineData("bullet points", TransformType.BulletList)]
    [InlineData("bullets", TransformType.BulletList)]
    [InlineData("format as bullets", TransformType.BulletList)]
    [InlineData("make numbered list", TransformType.NumberedList)]
    [InlineData("number this", TransformType.NumberedList)]
    [InlineData("make uppercase", TransformType.Uppercase)]
    [InlineData("all caps", TransformType.Uppercase)]
    [InlineData("make lowercase", TransformType.Lowercase)]
    [InlineData("make title case", TransformType.TitleCase)]
    [InlineData("make camel case", TransformType.CamelCase)]
    [InlineData("to camel case", TransformType.CamelCase)]
    [InlineData("make snake case", TransformType.SnakeCase)]
    [InlineData("make pascal case", TransformType.PascalCase)]
    [InlineData("make kebab case", TransformType.KebabCase)]
    [InlineData("wrap in quotes", TransformType.WrapQuotes)]
    [InlineData("wrap in backticks", TransformType.WrapBackticks)]
    [InlineData("make code block", TransformType.WrapCodeBlock)]
    [InlineData("trim whitespace", TransformType.TrimWhitespace)]
    [InlineData("make concise", TransformType.MakeConcise)]
    [InlineData("make formal", TransformType.MakeFormal)]
    [InlineData("fix whitespace", TransformType.FixWhitespace)]
    [InlineData("fix punctuation", TransformType.FixPunctuation)]
    [InlineData("normalize spacing", TransformType.NormalizeSpacing)]
    [InlineData("normalize quotes", TransformType.NormalizeQuotes)]
    public void Parse_TextTransforms_RecognizedCorrectly(string input, TransformType expectedTransform)
    {
        var intent = _parser.Parse(input);
        var transformIntent = Assert.IsType<TransformCommandIntent>(intent);
        Assert.Equal(expectedTransform, transformIntent.Transform);
    }

    [Theory]
    [InlineData("open notepad", "Notepad")]
    [InlineData("open vs code", "Visual Studio Code")]
    [InlineData("launch vscode", "Visual Studio Code")]
    [InlineData("open terminal", "Windows Terminal")]
    [InlineData("open calculator", "Calculator")]
    [InlineData("open file explorer", "File Explorer")]
    public void Parse_AllowlistedApplications_RecognizedCorrectly(string input, string expectedApp)
    {
        var intent = _parser.Parse(input);
        var appIntent = Assert.IsType<ApplicationCommandIntent>(intent);
        Assert.Equal(expectedApp, appIntent.ApplicationName);
    }

    [Theory]
    [InlineData("open website https://github.com", "https://github.com/")]
    [InlineData("go to https://flow.ai", "https://flow.ai/")]
    [InlineData("browse to www.bing.com", "https://www.bing.com/")]
    public void Parse_SafeUrls_RecognizedCorrectly(string input, string expectedUrl)
    {
        var intent = _parser.Parse(input);
        var urlIntent = Assert.IsType<UrlCommandIntent>(intent);
        Assert.Equal(expectedUrl, urlIntent.ValidatedUrl.ToString());
    }

    [Theory]
    [InlineData("delete this")]
    [InlineData("delete selection")]
    [InlineData("remove selection")]
    public void Parse_DeleteSelection_RequiresConfirmation(string input)
    {
        var intent = _parser.Parse(input);
        Assert.IsType<DeleteSelectionIntent>(intent);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("never mind")]
    [InlineData("stop")]
    [InlineData("abort")]
    public void Parse_Cancellation_RecognizedCorrectly(string input)
    {
        var intent = _parser.Parse(input);
        Assert.IsType<CancelCommandIntent>(intent);
    }

    [Theory]
    [InlineData("open random_malware")]
    [InlineData("open powershell")]
    [InlineData("launch cmd.exe")]
    [InlineData("run C:\\virus.exe")]
    public void Parse_UnapprovedOrDangerousApplications_FailClosed(string input)
    {
        var intent = _parser.Parse(input);
        Assert.IsType<UnknownCommandIntent>(intent);
    }

    [Theory]
    [InlineData("I want to copy the paragraph")]
    [InlineData("The meeting is cancelled")]
    [InlineData("Please tell me what copy means")]
    [InlineData("Tell me how to undo the change")]
    [InlineData("What is camel case")]
    [InlineData("We should delete this later")]
    [InlineData("This is a bullet point")]
    public void Parse_ProseStatements_FailClosedToUnknown(string input)
    {
        var intent = _parser.Parse(input);
        Assert.IsType<UnknownCommandIntent>(intent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ReturnsUnknownCommand(string? input)
    {
        var intent = _parser.Parse(input!);
        Assert.IsType<UnknownCommandIntent>(intent);
    }
}
