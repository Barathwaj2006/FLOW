using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Flow.Host.Windows.Native;
using Flow.Inference;
using Xunit;

namespace Flow.Windows.Tests;

public sealed class LocalApiServerTests
{
    [Fact]
    public void AudioDecoder_AccuratelyDecodesRawPcmAndWav()
    {
        // 1. Raw 16-bit PCM (1 second of 16kHz silence / 0s)
        byte[] rawPcm = new byte[32000];
        float[] samplesPcm = LocalApiServer.DecodeAudioToSamples(rawPcm);
        Assert.Equal(16000, samplesPcm.Length);
        Assert.All(samplesPcm, s => Assert.Equal(0f, s));

        // 2. Simulated 16-bit PCM Sine Wave
        short[] pcmShorts = new short[1600];
        for (int i = 0; i < pcmShorts.Length; i++)
        {
            pcmShorts[i] = (short)(Math.Sin(2 * Math.PI * 440 * i / 16000) * 16000);
        }
        byte[] pcmBytes = new byte[pcmShorts.Length * 2];
        Buffer.BlockCopy(pcmShorts, 0, pcmBytes, 0, pcmBytes.Length);

        float[] decoded = LocalApiServer.DecodeAudioToSamples(pcmBytes);
        Assert.Equal(pcmShorts.Length, decoded.Length);
        Assert.InRange(decoded[0], -0.01f, 0.01f);
        Assert.InRange(decoded[10], 0.1f, 0.5f);

        // 3. Valid WAV file container
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + pcmBytes.Length);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        // fmt chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // chunkSize
        writer.Write((short)1); // PCM
        writer.Write((short)1); // Mono
        writer.Write(16000); // 16kHz
        writer.Write(32000); // ByteRate
        writer.Write((short)2); // BlockAlign
        writer.Write((short)16); // BitsPerSample
        // data chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmBytes.Length);
        writer.Write(pcmBytes);

        byte[] wavBytes = ms.ToArray();
        float[] wavDecoded = LocalApiServer.DecodeAudioToSamples(wavBytes);
        Assert.Equal(pcmShorts.Length, wavDecoded.Length);
        Assert.InRange(wavDecoded[0], -0.01f, 0.01f);
        Assert.InRange(wavDecoded[10], 0.1f, 0.5f);
    }

    [Fact]
    public void AudioDecoder_ReturnsEmptyOnNullOrEmptyInput()
    {
        Assert.Empty(LocalApiServer.DecodeAudioToSamples(Array.Empty<byte>()));
        Assert.Empty(LocalApiServer.DecodeAudioToSamples(null!));
    }

    [Fact]
    public async Task SessionStream_StreamsSseHeadersAndInitialState()
    {
        var modelManager = new WhisperModelManager();
        var whisperInference = new WhisperNetInferenceEngine(modelManager);
        var pipeline = new TranscriptProcessingPipeline();
        var ringBuffer = new Flow.Core.Audio.AudioRingBuffer(10.0);
        var vad = new Flow.Core.Audio.EnergyVAD();
        var asrReg = new Flow.Core.ASR.ASREngineRegistry();
        var sanitizer = new Flow.Core.Language.DeterministicTextSanitizer();
        var insertion = new Flow.Host.Windows.Native.WindowsTextInsertionService();
        var coordinator = new Flow.Core.Session.VoiceSessionCoordinator(ringBuffer, vad, asrReg, sanitizer, insertion);

        using var server = new LocalApiServer(
            whisperInference,
            pipeline,
            coordinator: coordinator,
            preferredPort: 5019
        );

        server.Start();
        if (!server.IsRunning)
        {
            return;
        }

        try
        {
            using var client = new HttpClient();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3));

            var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{server.Port}/api/session/stream");
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);

            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.StartsWith("text/event-stream", resp.Content.Headers.ContentType?.ToString());

            using var stream = await resp.Content.ReadAsStreamAsync(cts.Token);
            using var reader = new StreamReader(stream);

            string? eventLine = await reader.ReadLineAsync(cts.Token);
            string? dataLine = await reader.ReadLineAsync(cts.Token);

            Assert.Equal("event: state", eventLine);
            Assert.Contains("Idle", dataLine);
        }
        finally
        {
            server.Stop();
        }
    }
}
