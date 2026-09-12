using System;
using Flow.Core.Commands;
using Flow.Core.Context;
using Xunit;

namespace Flow.Core.Tests.Commands;

/// <summary>
/// Section 45: Mandatory 300-case Command Corpus.
/// Verifies explicit command recognition, typed intent mapping, and policy decisions.
/// </summary>
public class CommandCorpusTests
{
    private readonly DeterministicCommandParser _parser = new();
    private readonly DeterministicCommandPolicy _policy = new();

    private static CommandExecutionContext CreateContext() => new(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        new IntPtr(100),
        1234,
        "notepad",
        FocusedControlInfo.Empty,
        "Sample active selection",
        ApplicationCategory.GeneralProse
    );

    [Theory]
    [InlineData("copy", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy.", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy!", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy, please", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy now", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy this", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy this.", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy this!", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy this, please", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy this now", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy that", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy that.", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy that!", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy that, please", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy that now", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy selection", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy selection.", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy selection!", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy selection, please", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("copy selection now", CommandIntentType.Copy, CommandPolicyDecision.Allow)]
    [InlineData("paste", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste.", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste!", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste, please", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste now", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste that", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste that.", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste that!", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste that, please", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("paste that now", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("insert clipboard", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("insert clipboard.", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("insert clipboard!", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("insert clipboard, please", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("insert clipboard now", CommandIntentType.Paste, CommandPolicyDecision.Allow)]
    [InlineData("undo", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo.", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo!", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo, please", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo now", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo that", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo that.", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo that!", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo that, please", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("undo that now", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("revert", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("revert.", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("revert!", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("revert, please", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("revert now", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("redo", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo.", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo!", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo, please", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo now", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo that", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo that.", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo that!", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo that, please", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("redo that now", CommandIntentType.Redo, CommandPolicyDecision.Allow)]
    [InlineData("select all", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select all.", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select all!", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select all, please", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select all now", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("highlight all", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("highlight all.", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("highlight all!", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("highlight all, please", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("highlight all now", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select everything", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select everything.", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select everything!", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select everything, please", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("select everything now", CommandIntentType.SelectAll, CommandPolicyDecision.Allow)]
    [InlineData("deselect", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("deselect.", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("deselect!", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("deselect, please", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("deselect now", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("clear selection", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("clear selection.", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("clear selection!", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("clear selection, please", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("clear selection now", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("unselect", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("unselect.", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("unselect!", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("unselect, please", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("unselect now", CommandIntentType.Deselect, CommandPolicyDecision.Allow)]
    [InlineData("ரத்து செய்", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("ரத்து செய்.", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("ரத்து செய்!", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("ரத்து செய், please", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("ரத்து செய் now", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("पूर्ववत करो", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("पूर्ववत करो.", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("पूर्ववत करो!", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("पूर्ववत करो, please", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("पूर्ववत करो now", CommandIntentType.Undo, CommandPolicyDecision.Allow)]
    [InlineData("make bullet points", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make bullet points.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make bullet points!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make bullet points, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make bullet points now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullet points", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullet points.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullet points!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullet points, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullet points now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullets", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullets.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullets!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullets, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("bullets now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("format as bullets", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("format as bullets.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("format as bullets!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("format as bullets, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("format as bullets now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("turn into bullets", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("turn into bullets.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("turn into bullets!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("turn into bullets, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("turn into bullets now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make numbered list", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make numbered list.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make numbered list!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make numbered list, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make numbered list now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("numbered list", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("numbered list.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("numbered list!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("numbered list, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("numbered list now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number this", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number this.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number this!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number this, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number this now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number list", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number list.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number list!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number list, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("number list now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make uppercase", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make uppercase.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make uppercase!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make uppercase, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make uppercase now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("uppercase", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("uppercase.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("uppercase!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("uppercase, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("uppercase now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all caps", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all caps.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all caps!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all caps, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all caps now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to uppercase", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to uppercase.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to uppercase!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to uppercase, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to uppercase now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make lowercase", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make lowercase.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make lowercase!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make lowercase, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make lowercase now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("lowercase", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("lowercase.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("lowercase!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("lowercase, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("lowercase now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all lowercase", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all lowercase.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all lowercase!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all lowercase, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("all lowercase now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make title case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make title case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make title case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make title case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make title case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("title case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("title case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("title case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("title case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("title case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("capitalize words", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("capitalize words.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("capitalize words!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("capitalize words, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("capitalize words now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make camel case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make camel case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make camel case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make camel case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make camel case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to camel case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to camel case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to camel case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to camel case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to camel case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make snake case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make snake case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make snake case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make snake case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make snake case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to snake case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to snake case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to snake case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to snake case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to snake case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make pascal case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make pascal case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make pascal case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make pascal case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make pascal case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to pascal case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to pascal case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to pascal case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to pascal case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to pascal case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make kebab case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make kebab case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make kebab case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make kebab case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make kebab case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to kebab case", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to kebab case.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to kebab case!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to kebab case, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("to kebab case now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in quotes", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in quotes.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in quotes!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in quotes, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in quotes now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in quotes", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in quotes.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in quotes!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in quotes, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in quotes now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("surround with quotes", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("surround with quotes.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("surround with quotes!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("surround with quotes, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("surround with quotes now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in backticks", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in backticks.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in backticks!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in backticks, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in backticks now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("add backticks", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("add backticks.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("add backticks!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("add backticks, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("add backticks now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in backticks", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in backticks.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in backticks!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in backticks, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("put in backticks now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("inline code", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("inline code.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("inline code!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("inline code, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("inline code now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make code block", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make code block.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make code block!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make code block, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make code block now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in code block", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in code block.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in code block!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in code block, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("wrap in code block now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("trim whitespace", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("trim whitespace.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("trim whitespace!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("trim whitespace, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("trim whitespace now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("clean up whitespace", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("clean up whitespace.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("clean up whitespace!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("clean up whitespace, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("clean up whitespace now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("remove extra spaces", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("remove extra spaces.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("remove extra spaces!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("remove extra spaces, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("remove extra spaces now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make concise", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make concise.", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make concise!", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make concise, please", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    [InlineData("make concise now", CommandIntentType.TransformSelection, CommandPolicyDecision.Allow)]
    public void CommandCorpus_ParsedAndEvaluatedAccurately(string phrase, CommandIntentType expectedIntent, CommandPolicyDecision expectedDecision)
    {
        var intent = _parser.Parse(phrase);
        Assert.False(intent is UnknownCommandIntent, $"Failed to parse known command: '{phrase}'");

        AssertIntentMatchesExpected(intent, expectedIntent);

        var context = CreateContext();
        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(expectedDecision, evaluation.Decision);
    }

    private static void AssertIntentMatchesExpected(CommandIntent intent, CommandIntentType expectedIntent)
    {
        switch (expectedIntent)
        {
            case CommandIntentType.TransformSelection:
                Assert.IsType<TransformCommandIntent>(intent);
                break;
            case CommandIntentType.Copy:
                Assert.True(intent is EditorCommandIntent { ActionName: "copy" });
                break;
            case CommandIntentType.Paste:
                Assert.True(intent is EditorCommandIntent { ActionName: "paste" });
                break;
            case CommandIntentType.Undo:
                Assert.True(intent is EditorCommandIntent { ActionName: "undo" });
                break;
            case CommandIntentType.Redo:
                Assert.True(intent is EditorCommandIntent { ActionName: "redo" });
                break;
            case CommandIntentType.SelectAll:
                Assert.True(intent is EditorCommandIntent { ActionName: "select_all" });
                break;
            case CommandIntentType.Deselect:
                Assert.True(intent is EditorCommandIntent { ActionName: "deselect" });
                break;
            case CommandIntentType.DeleteSelection:
                Assert.IsType<DeleteSelectionIntent>(intent);
                break;
            case CommandIntentType.CancelCommand:
                Assert.IsType<CancelCommandIntent>(intent);
                break;
            case CommandIntentType.OpenApplication:
                Assert.IsType<ApplicationCommandIntent>(intent);
                break;
            case CommandIntentType.OpenUrl:
                Assert.IsType<UrlCommandIntent>(intent);
                break;
            default:
                Assert.NotEqual(CommandIntentType.Unknown, expectedIntent);
                break;
        }
    }
}