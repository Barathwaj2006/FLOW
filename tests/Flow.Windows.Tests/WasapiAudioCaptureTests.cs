using System;
using System.Collections.Generic;
using System.Threading;
using Flow.Host.Windows.Native;
using Xunit;

namespace Flow.Windows.Tests;

public class WasapiAudioCaptureTests
{
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
}
