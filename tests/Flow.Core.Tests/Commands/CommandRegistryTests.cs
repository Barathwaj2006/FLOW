using System;
using Flow.Core.Commands;
using Flow.Core.Context;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandRegistryTests
{
    private readonly CommandRegistry _registry = new();

    [Fact]
    public void BuiltInCommands_AreRegistered()
    {
        var all = _registry.GetAllCommands();
        Assert.NotEmpty(all);
        Assert.Contains(all, c => c.Id == "cmd.copy");
        Assert.Contains(all, c => c.Id == "cmd.undo");
        Assert.Contains(all, c => c.Id == "cmd.delete_selection");
        Assert.Contains(all, c => c.Id == "transform.bullets");
    }

    [Theory]
    [InlineData("copy", "cmd.copy")]
    [InlineData("copy selection", "cmd.copy")]
    [InlineData("undo", "cmd.undo")]
    [InlineData("revert", "cmd.undo")]
    [InlineData("make bullet points", "transform.bullets")]
    [InlineData("make uppercase", "transform.uppercase")]
    [InlineData("delete this", "cmd.delete_selection")]
    [InlineData("cancel", "cmd.cancel")]
    public void TryFindCommand_RecognizesAliases(string phrase, string expectedId)
    {
        bool found = _registry.TryFindCommand(phrase, out var cmd);
        Assert.True(found);
        Assert.NotNull(cmd);
        Assert.Equal(expectedId, cmd!.Id);
    }

    [Fact]
    public void TryFindCommand_UnknownPhrase_ReturnsFalse()
    {
        bool found = _registry.TryFindCommand("some random text", out var cmd);
        Assert.False(found);
        Assert.Null(cmd);
    }

    [Fact]
    public void RegisterCommand_AddsNewCommandAndAliases()
    {
        var custom = new CommandDefinition(
            "custom.test",
            "Custom Test",
            new[] { "run test custom", "test alias" },
            CommandIntentType.EditorAction,
            CommandRisk.Safe,
            CommandPermission.None,
            new[] { ApplicationCategory.Code },
            IsReversible: true,
            RequiresConfirmation: false
        );

        _registry.RegisterCommand(custom);

        Assert.True(_registry.TryFindCommand("run test custom", out var found));
        Assert.Equal("custom.test", found!.Id);
    }
}
