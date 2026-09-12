using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class CommandModeStateMachineTests
{
    [Fact]
    public void InitialState_IsDisabled()
    {
        var sm = new CommandModeStateMachine();
        Assert.Equal(CommandModeState.Disabled, sm.CurrentState);
        Assert.False(sm.IsActive);
    }

    [Fact]
    public void ValidTransitions_FollowStandardLifecycle()
    {
        var sm = new CommandModeStateMachine();

        // Disabled -> Arming
        Assert.True(sm.CanTransitionTo(CommandModeState.Arming));
        Assert.True(sm.TryTransitionTo(CommandModeState.Arming, "Hotkey pressed"));
        Assert.Equal(CommandModeState.Arming, sm.CurrentState);

        // Arming -> Active
        Assert.True(sm.CanTransitionTo(CommandModeState.Active));
        Assert.True(sm.TryTransitionTo(CommandModeState.Active, "Listening for commands"));
        Assert.Equal(CommandModeState.Active, sm.CurrentState);
        Assert.True(sm.IsActive);

        // Active -> Executing
        Assert.True(sm.CanTransitionTo(CommandModeState.Executing));
        Assert.True(sm.TryTransitionTo(CommandModeState.Executing, "Executing command"));
        Assert.Equal(CommandModeState.Executing, sm.CurrentState);

        // Executing -> Completed
        Assert.True(sm.CanTransitionTo(CommandModeState.Completed));
        Assert.True(sm.TryTransitionTo(CommandModeState.Completed, "Success"));
        Assert.Equal(CommandModeState.Completed, sm.CurrentState);

        // Completed -> Disabled
        Assert.True(sm.CanTransitionTo(CommandModeState.Disabled));
        Assert.True(sm.TryTransitionTo(CommandModeState.Disabled, "Session finished"));
        Assert.Equal(CommandModeState.Disabled, sm.CurrentState);
    }

    [Fact]
    public void ConfirmationLifecycle_FollowsExpectedPath()
    {
        var sm = new CommandModeStateMachine();

        sm.TryTransitionTo(CommandModeState.Arming);
        sm.TryTransitionTo(CommandModeState.Active);

        // Active -> AwaitingConfirmation
        Assert.True(sm.CanTransitionTo(CommandModeState.AwaitingConfirmation));
        Assert.True(sm.TryTransitionTo(CommandModeState.AwaitingConfirmation, "Dangerous action recognized"));
        Assert.Equal(CommandModeState.AwaitingConfirmation, sm.CurrentState);
        Assert.True(sm.IsActive);

        // AwaitingConfirmation -> Executing (confirmed)
        Assert.True(sm.CanTransitionTo(CommandModeState.Executing));
        Assert.True(sm.TryTransitionTo(CommandModeState.Executing, "User confirmed"));
        Assert.Equal(CommandModeState.Executing, sm.CurrentState);

        // Executing -> Completed
        Assert.True(sm.TryTransitionTo(CommandModeState.Completed));
        Assert.Equal(CommandModeState.Completed, sm.CurrentState);
    }

    [Fact]
    public void InvalidTransitions_AreRejected()
    {
        var sm = new CommandModeStateMachine();

        // Cannot skip from Disabled directly to Executing
        Assert.False(sm.CanTransitionTo(CommandModeState.Executing));
        Assert.False(sm.TryTransitionTo(CommandModeState.Executing));
        Assert.Equal(CommandModeState.Disabled, sm.CurrentState);

        // Cannot skip from Disabled directly to Completed
        Assert.False(sm.CanTransitionTo(CommandModeState.Completed));
        Assert.False(sm.TryTransitionTo(CommandModeState.Completed));

        // Move to Arming
        sm.TryTransitionTo(CommandModeState.Arming);

        // Cannot skip from Arming directly to Completed
        Assert.False(sm.CanTransitionTo(CommandModeState.Completed));
        Assert.False(sm.TryTransitionTo(CommandModeState.Completed));
    }

    [Theory]
    [InlineData(CommandModeState.Arming)]
    [InlineData(CommandModeState.Active)]
    [InlineData(CommandModeState.AwaitingConfirmation)]
    [InlineData(CommandModeState.Executing)]
    public void Cancellation_IsPermittedFromAnyActiveState(CommandModeState state)
    {
        var sm = new CommandModeStateMachine();

        // Drive to the desired state
        if (state == CommandModeState.Arming)
        {
            sm.TryTransitionTo(CommandModeState.Arming);
        }
        else if (state == CommandModeState.Active)
        {
            sm.TryTransitionTo(CommandModeState.Arming);
            sm.TryTransitionTo(CommandModeState.Active);
        }
        else if (state == CommandModeState.AwaitingConfirmation)
        {
            sm.TryTransitionTo(CommandModeState.Arming);
            sm.TryTransitionTo(CommandModeState.Active);
            sm.TryTransitionTo(CommandModeState.AwaitingConfirmation);
        }
        else if (state == CommandModeState.Executing)
        {
            sm.TryTransitionTo(CommandModeState.Arming);
            sm.TryTransitionTo(CommandModeState.Active);
            sm.TryTransitionTo(CommandModeState.Executing);
        }

        Assert.Equal(state, sm.CurrentState);
        Assert.True(sm.CanTransitionTo(CommandModeState.Cancelled));
        Assert.True(sm.TryTransitionTo(CommandModeState.Cancelled, "User pressed Escape"));
        Assert.Equal(CommandModeState.Cancelled, sm.CurrentState);
    }

    [Fact]
    public void ForceReset_ResetsStateToDisabled()
    {
        var sm = new CommandModeStateMachine();
        sm.TryTransitionTo(CommandModeState.Arming);
        sm.TryTransitionTo(CommandModeState.Active);

        sm.ForceReset("Emergency abort");
        Assert.Equal(CommandModeState.Disabled, sm.CurrentState);
        Assert.Equal("Emergency abort", sm.LastReason);
    }

    [Fact]
    public void StateChangedEvent_FiresOnEveryTransition()
    {
        var sm = new CommandModeStateMachine();
        int eventCount = 0;
        CommandModeState lastPrev = CommandModeState.Disabled;
        CommandModeState lastNext = CommandModeState.Disabled;

        sm.StateChanged += (prev, next, reason) =>
        {
            eventCount++;
            lastPrev = prev;
            lastNext = next;
        };

        sm.TryTransitionTo(CommandModeState.Arming, "arm");
        Assert.Equal(1, eventCount);
        Assert.Equal(CommandModeState.Disabled, lastPrev);
        Assert.Equal(CommandModeState.Arming, lastNext);

        sm.TryTransitionTo(CommandModeState.Active, "activate");
        Assert.Equal(2, eventCount);
        Assert.Equal(CommandModeState.Arming, lastPrev);
        Assert.Equal(CommandModeState.Active, lastNext);
    }
}
