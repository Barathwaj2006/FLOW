using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Backtrack;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;

namespace Flow.Core.Tests.Backtrack;

public class BacktrackTests
{
    private class SafeMockInsertionService : ITextInsertionService
    {
        public List<string> InsertedList { get; } = new();
        public IntPtr SimulatedCurrentHwnd { get; set; } = (IntPtr)1001;
        public uint SimulatedCurrentPid { get; set; } = 2002;

        public Task<InsertionResult> InsertTextAsync(string text, CancellationToken cancellationToken = default)
        {
            InsertedList.Add(text);
            return Task.FromResult(new InsertionResult(
                true,
                InsertionStrategy.UiaDirect,
                "TestEditor",
                TimeSpan.FromMilliseconds(5),
                null,
                SimulatedCurrentHwnd,
                SimulatedCurrentPid,
                text.Length
            ));
        }

        public Task<bool> BacktrackAsync(InsertionRecord record, CancellationToken cancellationToken = default)
        {
            // Safety Check: Active window mismatch simulation
            if (record.TargetHwnd != IntPtr.Zero && record.TargetHwnd != SimulatedCurrentHwnd)
            {
                return Task.FromResult(false);
            }

            if (record.TargetProcessId != 0 && record.TargetProcessId != SimulatedCurrentPid)
            {
                return Task.FromResult(false);
            }

            if (InsertedList.Count > 0)
            {
                InsertedList.RemoveAt(InsertedList.Count - 1);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    [Fact]
    public void HistoryTracker_FollowsLifoOrder()
    {
        var tracker = new InsertionHistoryTracker(maxCapacity: 10);
        var r1 = new InsertionRecord(Guid.NewGuid(), "one", 3, DateTimeOffset.UtcNow, (IntPtr)1, "app", 10, InsertionStrategy.UiaDirect);
        var r2 = new InsertionRecord(Guid.NewGuid(), "two", 3, DateTimeOffset.UtcNow, (IntPtr)1, "app", 10, InsertionStrategy.UiaDirect);

        tracker.RecordInsertion(r1);
        tracker.RecordInsertion(r2);

        Assert.Equal(2, tracker.Count);
        Assert.Equal("two", tracker.PeekLastInsertion()?.InsertedText);

        var popped1 = tracker.PopLastInsertion();
        Assert.Equal("two", popped1?.InsertedText);
        Assert.Equal(1, tracker.Count);

        var popped2 = tracker.PopLastInsertion();
        Assert.Equal("one", popped2?.InsertedText);
        Assert.Equal(0, tracker.Count);

        Assert.Null(tracker.PopLastInsertion());
    }

    [Fact]
    public void HistoryTracker_BoundedCapacity_EvictsOldest()
    {
        var tracker = new InsertionHistoryTracker(maxCapacity: 3);
        for (int i = 1; i <= 5; i++)
        {
            tracker.RecordInsertion(new InsertionRecord(Guid.NewGuid(), $"item{i}", 5, DateTimeOffset.UtcNow, (IntPtr)1, "app", 10, InsertionStrategy.UiaDirect));
        }

        Assert.Equal(3, tracker.Count);
        // The last item should be item5, then item4, then item3
        Assert.Equal("item5", tracker.PopLastInsertion()?.InsertedText);
        Assert.Equal("item4", tracker.PopLastInsertion()?.InsertedText);
        Assert.Equal("item3", tracker.PopLastInsertion()?.InsertedText);
        Assert.Null(tracker.PopLastInsertion());
    }

    [Fact]
    public async Task SessionCoordinator_Backtrack_RevertsSingleInsertion()
    {
        var insertionService = new SafeMockInsertionService();
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 5.0);
        var vad = new EnergyVAD(energyThreshold: 0.01f);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine { DefaultTranscript = "hello world" };
        registry.Register(mockAsr, isDefault: true);
        var languageEngine = new DeterministicTextSanitizer();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer, vad, registry, languageEngine, insertionService);

        // Simulate dictation
        await coordinator.StartSessionAsync();
        float[] fakeSpeech = new float[16000];
        Array.Fill(fakeSpeech, 0.1f);
        coordinator.ProcessAudioChunk(fakeSpeech);
        bool endSuccess = await coordinator.EndSessionAsync();

        Assert.True(endSuccess);
        Assert.Single(insertionService.InsertedList);
        Assert.Equal(1, coordinator.HistoryTracker.Count);

        // Execute backtrack
        bool backtrackSuccess = await coordinator.BacktrackAsync();

        Assert.True(backtrackSuccess);
        Assert.Empty(insertionService.InsertedList);
        Assert.Equal(0, coordinator.HistoryTracker.Count);
    }

    [Fact]
    public async Task SessionCoordinator_Backtrack_EmptyHistory_ReturnsFalse()
    {
        var insertionService = new SafeMockInsertionService();
        var ringBuffer = new AudioRingBuffer();
        var vad = new EnergyVAD();
        var registry = new ASREngineRegistry();
        var languageEngine = new DeterministicTextSanitizer();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer, vad, registry, languageEngine, insertionService);

        bool backtrackSuccess = await coordinator.BacktrackAsync();

        Assert.False(backtrackSuccess);
    }

    [Fact]
    public async Task SessionCoordinator_Backtrack_FocusChanged_SafelyAborts()
    {
        var insertionService = new SafeMockInsertionService();
        var ringBuffer = new AudioRingBuffer(capacitySeconds: 5.0);
        var vad = new EnergyVAD(energyThreshold: 0.01f);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine { DefaultTranscript = "sensitive text" };
        registry.Register(mockAsr, isDefault: true);
        var languageEngine = new DeterministicTextSanitizer();

        var coordinator = new VoiceSessionCoordinator(
            ringBuffer, vad, registry, languageEngine, insertionService);

        // Simulate dictation into window 1001
        await coordinator.StartSessionAsync();
        float[] fakeSpeech = new float[16000];
        Array.Fill(fakeSpeech, 0.1f);
        coordinator.ProcessAudioChunk(fakeSpeech);
        await coordinator.EndSessionAsync();

        Assert.Single(insertionService.InsertedList);

        // User switches to a different window (e.g. 9999 - Outlook or Terminal)
        insertionService.SimulatedCurrentHwnd = (IntPtr)9999;

        // Backtrack should be rejected for safety!
        bool backtrackSuccess = await coordinator.BacktrackAsync();

        Assert.False(backtrackSuccess);
        // Original text was NOT deleted because of target window mismatch
        Assert.Single(insertionService.InsertedList);
    }
}
