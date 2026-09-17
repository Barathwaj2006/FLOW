using System;
using Flow.Core.Commands;
using Flow.Core.Context;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandConfirmationTests
{
    private readonly CommandConfirmationService _confirmationService = new();

    private static CommandExecutionContext CreateContext(
        IntPtr hwnd = default,
        uint pid = 1000,
        string selection = "delete me")
    {
        return new CommandExecutionContext(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            hwnd == default ? new IntPtr(1234) : hwnd,
            pid,
            "notepad",
            FocusedControlInfo.Empty,
            selection,
            ApplicationCategory.GeneralProse
        );
    }

    [Fact]
    public void CreateToken_BindsAllTargetProperties()
    {
        var context = CreateContext();
        var token = _confirmationService.CreateToken(context, "delete_selection");

        Assert.Equal(context.SessionId, token.SessionId);
        Assert.Equal("delete_selection", token.CommandId);
        Assert.Equal(context.TargetHwnd, token.TargetHwnd);
        Assert.Equal(context.TargetProcessId, token.TargetProcessId);
        Assert.Equal(context.SelectionText!.Length, token.SelectionLength);
        Assert.False(token.IsExpired);
    }

    [Theory]
    [InlineData("confirm")]
    [InlineData("yes")]
    [InlineData("proceed")]
    [InlineData("go ahead")]
    [InlineData("do it")]
    public void ValidateConfirmation_ApprovedPhrases_Accepted(string phrase)
    {
        var context = CreateContext();
        var token = _confirmationService.CreateToken(context, "delete_selection");

        bool valid = _confirmationService.ValidateConfirmation(token, context, phrase, out var failure);

        Assert.True(valid);
        Assert.Null(failure);
    }

    [Fact]
    public void ValidateConfirmation_TargetHwndChanged_Invalidated()
    {
        var context1 = CreateContext(hwnd: new IntPtr(1111));
        var token = _confirmationService.CreateToken(context1, "delete_selection");

        var context2 = CreateContext(hwnd: new IntPtr(2222)); // Focus switched to another window!

        bool valid = _confirmationService.ValidateConfirmation(token, context2, "confirm", out var failure);

        Assert.False(valid);
        Assert.Contains("Target window changed", failure);
    }

    [Fact]
    public void ValidateConfirmation_TargetProcessChanged_Invalidated()
    {
        var context1 = CreateContext(pid: 1001);
        var token = _confirmationService.CreateToken(context1, "delete_selection");

        var context2 = CreateContext(pid: 9999); // Process switched!

        bool valid = _confirmationService.ValidateConfirmation(token, context2, "confirm", out var failure);

        Assert.False(valid);
        Assert.Contains("Target process changed", failure);
    }

    [Fact]
    public void ValidateConfirmation_SelectionModified_Invalidated()
    {
        var context1 = CreateContext(selection: "initial selection");
        var token = _confirmationService.CreateToken(context1, "delete_selection");

        var context2 = CreateContext(selection: "different selection"); // Selection changed!

        bool valid = _confirmationService.ValidateConfirmation(token, context2, "confirm", out var failure);

        Assert.False(valid);
        Assert.Contains("selection modified", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateConfirmation_ExpiredToken_Invalidated()
    {
        var context = CreateContext();
        // Zero / negative lifetime -> immediately expired
        var token = _confirmationService.CreateToken(context, "delete_selection", TimeSpan.FromMilliseconds(-1));

        bool valid = _confirmationService.ValidateConfirmation(token, context, "confirm", out var failure);

        Assert.False(valid);
        Assert.Contains("timed out", failure, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("what did you say")]
    [InlineData("hello world")]
    [InlineData("maybe later")]
    [InlineData("")]
    public void ValidateConfirmation_UnapprovedPhrase_Invalidated(string unapprovedPhrase)
    {
        var context = CreateContext();
        var token = _confirmationService.CreateToken(context, "delete_selection");

        bool valid = _confirmationService.ValidateConfirmation(token, context, unapprovedPhrase, out var failure);

        Assert.False(valid);
        Assert.NotNull(failure);
    }
}
