using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.Language;
using Flow.Core.Session;
using Flow.Core.TextInsertion;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Core.Tests.Benchmarks;

public class LatencyBenchmarkTests
{
    private readonly ITestOutputHelper _output;

    public LatencyBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private class BenchmarkInsertionService : ITextInsertionService
    {
        public Task<InsertionResult> InsertTextAsync(string text, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InsertionResult(true, InsertionStrategy.UiaDirect, "BenchmarkTarget", TimeSpan.FromMicroseconds(100)));
        }

        public Task<bool> BacktrackAsync(Flow.Core.Backtrack.InsertionRecord record, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
    }

    [Fact]
    public async Task Benchmark_EndToEndPipelineLatency_RecordsP50P95P99()
    {
        const int iterations = 100;
        var latencies = new List<double>(iterations);

        var ringBuffer = new AudioRingBuffer(capacitySeconds: 10.0);
        var vad = new EnergyVAD(sampleRate: 16000.0, energyThreshold: 0.01f, minSpeechDurationSeconds: 0.05);
        var registry = new ASREngineRegistry();
        var mockAsr = new MockASREngine
        {
            DefaultTranscript = "Production-grade Windows AI voice dictation with zero enter simulation.",
            SimulatedLatency = TimeSpan.FromMilliseconds(5) // Fast local model simulation
        };
        registry.Register(mockAsr, isDefault: true);
        var sanitizer = new DeterministicTextSanitizer();
        var insertion = new BenchmarkInsertionService();

        var coordinator = new VoiceSessionCoordinator(ringBuffer, vad, registry, sanitizer, insertion);

        float[] speechChunk = new float[1600];
        Array.Fill(speechChunk, 0.15f);

        // Warmup
        for (int i = 0; i < 5; i++)
        {
            await coordinator.StartSessionAsync();
            coordinator.ProcessAudioChunk(speechChunk);
            coordinator.ProcessAudioChunk(speechChunk);
            await coordinator.EndSessionAsync();
        }

        // Benchmark runs
        for (int iter = 0; iter < iterations; iter++)
        {
            await coordinator.StartSessionAsync();
            coordinator.ProcessAudioChunk(speechChunk);
            coordinator.ProcessAudioChunk(speechChunk);

            var sw = Stopwatch.StartNew();
            bool result = await coordinator.EndSessionAsync();
            sw.Stop();

            Assert.True(result);
            latencies.Add(sw.Elapsed.TotalMilliseconds);
        }

        latencies.Sort();

        double p50 = latencies[(int)(iterations * 0.50)];
        double p95 = latencies[(int)(iterations * 0.95)];
        double p99 = latencies[(int)(iterations * 0.99)];
        double min = latencies[0];
        double max = latencies[^1];
        double avg = latencies.Average();

        _output.WriteLine("================ FLOW VOICE CORE BENCHMARK ================");
        _output.WriteLine($"Iterations: {iterations}");
        _output.WriteLine($"Min Latency: {min:F2} ms");
        _output.WriteLine($"Avg Latency: {avg:F2} ms");
        _output.WriteLine($"P50 Latency: {p50:F2} ms");
        _output.WriteLine($"P95 Latency: {p95:F2} ms");
        _output.WriteLine($"P99 Latency: {p99:F2} ms");
        _output.WriteLine($"Max Latency: {max:F2} ms");
        _output.WriteLine("===========================================================");

        // Empirical assertion: In-process coordinator overhead + fast inference is low
        Assert.True(p50 < 100.0, $"P50 latency must be under 100ms, actual: {p50:F2}ms");
        Assert.True(p99 < 1000.0, $"P99 latency must be under 1000ms under parallel suite load, actual: {p99:F2}ms");
    }

    [Fact]
    public void Benchmark_DeterministicSanitizer_Under1Millisecond()
    {
        var sanitizer = new DeterministicTextSanitizer();
        string testInput = "Um hello world period this is a test of the emergency broadcast system new line never execute code without review";

        // Warmup
        for (int i = 0; i < 100; i++)
        {
            sanitizer.Format(testInput);
        }

        const int iterations = 1000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            sanitizer.Format(testInput);
        }
        sw.Stop();

        double totalMs = sw.Elapsed.TotalMilliseconds;
        double perOpMicroseconds = (totalMs / iterations) * 1000.0;

        _output.WriteLine($"Sanitizer 1000 ops: {totalMs:F3}ms total, {perOpMicroseconds:F2} µs/op");
        Assert.True(perOpMicroseconds < 1000.0, "Sanitization must take under 1ms per operation.");
    }
}
