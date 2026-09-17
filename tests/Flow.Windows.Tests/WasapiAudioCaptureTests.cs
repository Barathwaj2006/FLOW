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
        using var devMgr = new WasapiDeviceManager();
        bool hasPhysicalMic = devMgr.EnumerateCaptureDevices().Count > 0;

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
        bool started = capture.WaitForStart(5000);
        Assert.True(started, "WASAPI capture thread failed to signal start within 5000ms.");

        Thread.Sleep(500);

        capture.Stop();
        Assert.False(capture.IsCapturing);

        if (hasPhysicalMic)
        {
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

    [Fact]
    public void PhysicalMicrophone_CapturesNonZeroAudio_MeasuresSignalMetrics()
    {
        using var devMgr = new WasapiDeviceManager();
        if (devMgr.EnumerateCaptureDevices().Count == 0)
        {
            _output.WriteLine("No physical microphone available on this host (headless CI). Skipping live sample metrics.");
            return;
        }

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

    [Fact]
    public void ConvertAndResample_Float32Stereo_ResamplesTo16kHzMonoCorrectly()
    {
        int nativeSampleRate = 48000;
        int channels = 2;
        int bitsPerSample = 32;
        uint numFrames = 480;

        float[] nativeData = new float[numFrames * channels];
        for (int i = 0; i < numFrames; i++)
        {
            nativeData[i * channels] = 0.5f;
            nativeData[i * channels + 1] = 0.5f;
        }

        unsafe
        {
            fixed (float* p = nativeData)
            {
                float[] resampled = WasapiAudioCapture.ConvertAndResample(
                    (IntPtr)p, numFrames, nativeSampleRate, channels, bitsPerSample, isFloat: true, isSilent: false);

                Assert.NotNull(resampled);
                Assert.Equal(160, resampled.Length);
                for (int i = 0; i < resampled.Length; i++)
                {
                    Assert.InRange(resampled[i], 0.49f, 0.51f);
                }
            }
        }
    }

    [Fact]
    public void ConvertAndResample_Pcm16Stereo_ConvertsAndResamplesCorrectly()
    {
        int nativeSampleRate = 48000;
        int channels = 2;
        int bitsPerSample = 16;
        uint numFrames = 480;

        short[] nativeData = new short[numFrames * channels];
        for (int i = 0; i < numFrames; i++)
        {
            nativeData[i * channels] = 16384;
            nativeData[i * channels + 1] = 16384;
        }

        unsafe
        {
            fixed (short* p = nativeData)
            {
                float[] resampled = WasapiAudioCapture.ConvertAndResample(
                    (IntPtr)p, numFrames, nativeSampleRate, channels, bitsPerSample, isFloat: false, isSilent: false);

                Assert.NotNull(resampled);
                Assert.Equal(160, resampled.Length);
                for (int i = 0; i < resampled.Length; i++)
                {
                    Assert.InRange(resampled[i], 0.49f, 0.51f);
                }
            }
        }
    }

    [Fact]
    public void ConvertAndResample_SilentBuffer_ReturnsPureSilence()
    {
        int nativeSampleRate = 16000;
        int channels = 1;
        int bitsPerSample = 32;
        uint numFrames = 160;

        float[] nativeData = new float[numFrames];
        Array.Fill(nativeData, 0.99f);

        unsafe
        {
            fixed (float* p = nativeData)
            {
                float[] resampled = WasapiAudioCapture.ConvertAndResample(
                    (IntPtr)p, numFrames, nativeSampleRate, channels, bitsPerSample, isFloat: true, isSilent: true);

                Assert.NotNull(resampled);
                Assert.Equal(160, resampled.Length);
                foreach (var sample in resampled)
                {
                    Assert.Equal(0.0f, sample);
                }
            }
        }
    }

    [Fact]
    public void ConvertAndResample_16kHzMonoFloat_DirectPassthrough()
    {
        int nativeSampleRate = 16000;
        int channels = 1;
        int bitsPerSample = 32;
        uint numFrames = 160;

        float[] nativeData = new float[numFrames];
        for (int i = 0; i < numFrames; i++)
        {
            nativeData[i] = (float)i / numFrames;
        }

        unsafe
        {
            fixed (float* p = nativeData)
            {
                float[] resampled = WasapiAudioCapture.ConvertAndResample(
                    (IntPtr)p, numFrames, nativeSampleRate, channels, bitsPerSample, isFloat: true, isSilent: false);

                Assert.NotNull(resampled);
                Assert.Equal(160, resampled.Length);
                for (int i = 0; i < numFrames; i++)
                {
                    Assert.Equal(nativeData[i], resampled[i]);
                }
            }
        }
    }
}
