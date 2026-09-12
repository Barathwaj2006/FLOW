using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandAuditPrivacyTests
{
    [Fact]
    public void AuditTrail_StoresMetadataOnly_NoSensitiveContent()
    {
        var audit = new CommandAuditTrail(capacity: 100);

        var entry = new CommandAuditEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "cmd.copy",
            CommandIntentType.Copy,
            CommandResultStatus.Success,
            CommandRisk.Safe,
            "notepad",
            TimeSpan.FromMilliseconds(5)
        );

        audit.Record(entry);

        Assert.Equal(1, audit.Count);
        var recent = audit.GetRecentEntries();
        Assert.Single(recent);
        Assert.Equal("cmd.copy", recent[0].CommandId);
        Assert.Equal(CommandResultStatus.Success, recent[0].Result);
    }

    [Fact]
    public void AuditTrail_EnforcesCapacityBound_NeverGrowsUnbounded()
    {
        var audit = new CommandAuditTrail(capacity: 10);

        for (int i = 0; i < 25; i++)
        {
            audit.Record(new CommandAuditEntry(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                $"cmd_{i}",
                CommandIntentType.Copy,
                CommandResultStatus.Success,
                CommandRisk.Safe,
                "notepad",
                TimeSpan.FromMilliseconds(1)
            ));
        }

        Assert.Equal(10, audit.Count);
        var entries = audit.GetRecentEntries();
        Assert.Equal("cmd_15", entries[0].CommandId);
        Assert.Equal("cmd_24", entries[^1].CommandId);
    }

    [Fact]
    public void AuditTrail_Clear_EmptiesAllEntries()
    {
        var audit = new CommandAuditTrail();
        audit.Record(new CommandAuditEntry(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "test",
            CommandIntentType.Copy,
            CommandResultStatus.Success,
            CommandRisk.Safe,
            "app",
            TimeSpan.Zero
        ));

        Assert.Equal(1, audit.Count);
        audit.Clear();
        Assert.Equal(0, audit.Count);
    }
}
