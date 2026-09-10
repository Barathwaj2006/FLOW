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

        Thread.Sleep(250);

        capture.Stop();
        Assert.False(capture.IsCapturing);

        lock (capturedChunks)
        {
            Assert.NotEmpty(capturedChunks);
        }
    }
}
