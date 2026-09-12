using System;
using Flow.Core.Commands;
using Flow.Core.Context;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandPolicyTests
{
    private readonly DeterministicCommandPolicy _policy = new();

    private static CommandExecutionContext CreateContext(
        string? selection = "Hello world",
        bool isPassword = false,
        ApplicationCategory category = ApplicationCategory.GeneralProse)
    {
        return new CommandExecutionContext(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new IntPtr(100),
            1234,
            "notepad",
            new FocusedControlInfo("Edit", "Text", "", "", isPassword, false, false),
            selection,
            category
        );
    }

    [Theory]
    [InlineData("shutdown /s /t 0")]
    [InlineData("reboot")]
    [InlineData("cmd.exe /c dir")]
    [InlineData("powershell.exe -Command Get-Process")]
    [InlineData("pwsh -c Clear-Host")]
    [InlineData("rm -rf /")]
    [InlineData("del C:\\Windows\\System32")]
    [InlineData("format C:")]
    [InlineData("diskpart")]
    [InlineData("taskkill /f /im explorer.exe")]
    [InlineData("kill -9 1234")]
    [InlineData("drop table users")]
    [InlineData("sudo rm -rf /etc")]
    [InlineData("set-executionpolicy unrestricted")]
    public void Policy_DangerousShellCommands_PermanentlyBlocked(string dangerousCommand)
    {
        var context = CreateContext();
        var intent = new EditorCommandIntent(dangerousCommand, "unknown");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Block, evaluation.Decision);
        Assert.Equal(CommandRisk.Blocked, evaluation.Risk);
        Assert.False(string.IsNullOrWhiteSpace(evaluation.Reason));
    }

    [Fact]
    public void Policy_PasswordField_PermanentlyBlocked()
    {
        var context = CreateContext(isPassword: true);
        var intent = new TransformCommandIntent("make uppercase", TransformType.Uppercase);

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Block, evaluation.Decision);
        Assert.Contains("Password", evaluation.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Policy_SensitiveCategory_PermanentlyBlocked()
    {
        var context = CreateContext(category: ApplicationCategory.Sensitive);
        var intent = new EditorCommandIntent("copy", "copy");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Block, evaluation.Decision);
    }

    [Fact]
    public void Policy_DeleteSelection_RequiresConfirmation()
    {
        var context = CreateContext(selection: "Text to be deleted");
        var intent = new DeleteSelectionIntent("delete this");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Confirm, evaluation.Decision);
        Assert.Equal(CommandRisk.ConfirmRequired, evaluation.Risk);
    }

    [Fact]
    public void Policy_DeleteSelection_NoSelection_Rejected()
    {
        var context = CreateContext(selection: null);
        var intent = new DeleteSelectionIntent("delete this");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Reject, evaluation.Decision);
    }

    [Fact]
    public void Policy_SmallSelectionTransform_AllowedImmediately()
    {
        var context = CreateContext(selection: "small snippet");
        var intent = new TransformCommandIntent("make uppercase", TransformType.Uppercase);

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Allow, evaluation.Decision);
        Assert.Equal(CommandRisk.Safe, evaluation.Risk);
    }

    [Fact]
    public void Policy_LargeSelectionTransform_RequiresConfirmation()
    {
        // 15,000 characters: Large selection tier
        string largeText = new string('a', 15000);
        var context = CreateContext(selection: largeText);
        var intent = new TransformCommandIntent("make uppercase", TransformType.Uppercase);

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Confirm, evaluation.Decision);
        Assert.Equal(CommandRisk.ConfirmRequired, evaluation.Risk);
    }

    [Fact]
    public void Policy_ExtremeSelectionTransform_Blocked()
    {
        // 120,000 characters: Extreme selection tier
        string extremeText = new string('a', 120000);
        var context = CreateContext(selection: extremeText);
        var intent = new TransformCommandIntent("make uppercase", TransformType.Uppercase);

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Block, evaluation.Decision);
        Assert.Equal(CommandRisk.Blocked, evaluation.Risk);
    }

    [Fact]
    public void Policy_AllowlistedApp_Allowed()
    {
        var context = CreateContext();
        var intent = new ApplicationCommandIntent("open notepad", "Notepad");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Allow, evaluation.Decision);
    }

    [Fact]
    public void Policy_NonAllowlistedApp_Blocked()
    {
        var context = CreateContext();
        var intent = new ApplicationCommandIntent("open malware", "malware");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Block, evaluation.Decision);
    }

    [Fact]
    public void Policy_SafeHttpsUrl_Allowed()
    {
        var context = CreateContext();
        var intent = new UrlCommandIntent("open url", new Uri("https://github.com/"));

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Allow, evaluation.Decision);
    }

    [Fact]
    public void Policy_CancelCommand_AlwaysAllowed()
    {
        var context = CreateContext();
        var intent = new CancelCommandIntent("cancel");

        var evaluation = _policy.Evaluate(intent, context);

        Assert.Equal(CommandPolicyDecision.Allow, evaluation.Decision);
    }
}
