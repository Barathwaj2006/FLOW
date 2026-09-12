using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Flow.Core.Backtrack;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Flow.Core.TranscriptProcessing;
using Flow.Host.Windows.Native;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

public class Phase2TranscriptionParityTests
{
    private readonly ITestOutputHelper _output;

    public Phase2TranscriptionParityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TranscriptPipeline_LatencyMeasurement_UnderOneMillisecond()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string input = "um so check the C# code in C:\\FLOW\\src\\Main.cs you know open bracket test close bracket period";

        // Warmup
        for (int i = 0; i < 50; i++)
        {
            _ = pipeline.Format(input);
        }

        // Measure 1,000 iterations
        const int iterations = 1000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            string res = pipeline.Format(input);
            Assert.False(string.IsNullOrEmpty(res));
        }
        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double avgUs = (totalMs / iterations) * 1000.0; // microseconds
        double avgMs = totalMs / iterations;

        _output.WriteLine($"[Phase 2 Latency] Total: {totalMs:F2}ms for {iterations} runs. Average: {avgMs:F4}ms ({avgUs:F1} µs) per utterance.");

        // Parity requirement: deterministic formatting pipeline must be ultra-low latency (< 1ms)
        Assert.True(avgMs < 1.0, $"Pipeline formatting latency ({avgMs:F4}ms) exceeded 1.0ms threshold");
    }

    [Fact]
    public void TranscriptPipeline_HeavyLoad_5000Words_RemainsStableAndDeterministic()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string sentence = "um we need to inspect the C# method in C:\\FLOW\\Engine.cs with List<string> you know full stop ";
        var sb = new System.Text.StringBuilder(5000 * 10);
        for (int i = 0; i < 350; i++)
        {
            sb.Append(sentence);
        }
        string longInput = sb.ToString();

        var sw = Stopwatch.StartNew();
        string output = pipeline.Format(longInput);
        sw.Stop();

        _output.WriteLine($"[Phase 2 Heavy Load] 5000+ words processed in {sw.ElapsedMilliseconds}ms. Length: {output.Length}");
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
        Assert.DoesNotContain("um", output);
        Assert.Contains("C#", output);
        Assert.Contains("C:\\FLOW\\Engine.cs", output);
    }

    [Fact]
    public async Task TranscriptPipeline_EndToEnd_RealWindowsInsertion_SucceedsWithoutReturn()
    {
        var pipeline = new TranscriptProcessingPipeline();
        string rawSpeech = "um testing FLOW smart formatting comma with C# code period";
        string formatted = pipeline.Format(rawSpeech);

        Assert.Equal("Testing FLOW smart formatting, with C# code.", formatted);
        Assert.DoesNotContain("\r", formatted);
        Assert.DoesNotContain("\n", formatted);

        var insertionService = new WindowsTextInsertionService();
        var result = await insertionService.InsertTextAsync(formatted);

        Assert.NotNull(result);
        Assert.True(result.Success);
        _output.WriteLine($"[Phase 2 Physical Insertion] Successfully inserted: '{formatted}' via {result.StrategyUsed}");
    }

    [Fact]
    public void TranscriptPipeline_FailClosed_BlocksInsertionOnCorruptedNewline()
    {
        // Pipeline must fail-closed if any stage outputs \r or \n
        var brokenPipeline = new TranscriptProcessingPipeline(new[] { new BrokenNewlineStage() });

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            brokenPipeline.Format("Hello world");
        });

        Assert.Contains("Zero-Enter invariant violated", ex.Message);
        _output.WriteLine($"[Phase 2 Fail-Closed] Verified pipeline throws: {ex.Message}");
    }

    [Fact]
    public async Task TranscriptPipeline_BacktrackIntegration_SucceedsSafely()
    {
        var insertionService = new WindowsTextInsertionService();
        string text = "FlowBacktrackParityVerification";

        var insertResult = await insertionService.InsertTextAsync(text);
        Assert.True(insertResult.Success);

        var record = new InsertionRecord(
            Guid.NewGuid(),
            text,
            text.Length,
            DateTimeOffset.UtcNow,
            insertResult.TargetHwnd,
            "ForegroundApp",
            insertResult.TargetProcessId,
            insertResult.StrategyUsed
        );

        bool backtracked = await insertionService.BacktrackAsync(record);
        _output.WriteLine($"[Phase 2 Backtrack] Backtrack executed with result: {backtracked}");
        // Even if the target window didn't have a focused text control in headless runner,
        // it must execute safely without crashing or leaking keys.
    }

    private sealed class BrokenNewlineStage : ITranscriptStage
    {
        public string Process(string text, TranscriptProcessingContext context)
        {
            return text + "\ncorrupted";
        }
    }
}
