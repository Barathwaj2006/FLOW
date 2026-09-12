using System;
using System.Collections.Generic;
using System.Threading;
using Flow.Host.Windows.Native;
using Xunit;
using Xunit.Abstractions;

namespace Flow.Windows.Tests;

public class WasapiAudioCaptureTests
{
    private readonly ITestOutputHelper _output;

    public WasapiAudioCaptureTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void StartAndStop_ExecutesCleanly()
    {
        var capturedChunks = new List<float[]>();
        using var capture = new WasapiAudioCapture(chunk =>
        {
            lock (capturedChunks)
            {
                capturedChunks.Add(chunk);
            }
        });

        Assert.False(capture.IsCapturing);

        capture.Start();
        Assert.True(capture.IsCapturing);

        bool started = capture.WaitForStart(5000);
        Assert.True(started, "WASAPI capture thread failed to signal start within 5000ms.");

        Thread.Sleep(500);

        capture.Stop();
        Assert.False(capture.IsCapturing);

        if (capture.LastError != null)
        {
            throw new Exception($"Capture loop failed: {capture.LastError.Message}", capture.LastError);
        }

        lock (capturedChunks)
        {
            Assert.True(capturedChunks.Count > 0,
                $"Expected captured chunks > 0, but got {capturedChunks.Count}. Diagnostics: Waits={capture.DiagnosticWaitCount}, Timeouts={capture.DiagnosticTimeoutCount}, Packets={capture.DiagnosticPacketsReceived}, Rate={capture.NativeSampleRate}, Ch={capture.NativeChannels}");
        }
    }

    [Fact]
    public void PhysicalMicrophone_CapturesNonZeroAudio_MeasuresSignalMetrics()
    {
        var allSamples = new List<float>();
        using var capture = new WasapiAudioCapture(chunk =>
        {
            lock (allSamples)
            {
                allSamples.AddRange(chunk);
            }
        });

        capture.Start();
        bool started = capture.WaitForStart(5000);
        Assert.True(started, "Failed to start WASAPI capture within 5s.");

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Thread.Sleep(2000);
        sw.Stop();

        capture.Stop();
        Assert.Null(capture.LastError);

        float[] samples;
        lock (allSamples)
        {
            samples = allSamples.ToArray();
        }

        Assert.NotEmpty(samples);

        double sumSquares = 0.0;
        float peak = 0.0f;
        int nonZeroCount = 0;

        foreach (var sample in samples)
        {
            float abs = Math.Abs(sample);
            if (abs > peak) peak = abs;
            sumSquares += sample * sample;
            if (abs > 1e-6f) nonZeroCount++;
        }

        double rms = Math.Sqrt(sumSquares / samples.Length);
        double nonZeroPct = (double)nonZeroCount / samples.Length * 100.0;

        _output.WriteLine($"=== PHYSICAL MICROPHONE VALIDATION EVIDENCE ===");
        _output.WriteLine($"Endpoint Device: {capture.ActiveDeviceName}");
        _output.WriteLine($"Native Format: {capture.SampleFormat} {capture.NativeSampleRate}Hz, {capture.NativeChannels} ch");
        _output.WriteLine($"Buffer Duration: {capture.BufferDurationMs:F2} ms");
        _output.WriteLine($"Capture Duration: {sw.ElapsedMilliseconds} ms");
        _output.WriteLine($"Resampled 16kHz Samples: {samples.Length}");
        _output.WriteLine($"RMS Amplitude: {rms:F6}");
        _output.WriteLine($"Peak Amplitude: {peak:F6}");
        _output.WriteLine($"Non-Zero Sample Percentage: {nonZeroPct:F2}%");

        Assert.True(samples.Length >= 16000,
            $"Expected >=16000 resampled 16kHz samples for 2s, got {samples.Length}");

        // Fail if all samples are pure zero
        Assert.True(peak > 0f, "Microphone capture returned pure zeros. No physical audio detected.");
    }
}
