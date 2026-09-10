using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Inference;
using Xunit;

namespace Flow.Windows.Tests;

public class WhisperInferenceTests
{
    [Fact]
    public void WhisperModelManager_DefaultConfiguration_HasCorrectOfficialUrlAndSha256()
    {
        var manager = new WhisperModelManager();
        Assert.Equal("ggml-tiny.en.bin", WhisperModelManager.DefaultModelName);
        Assert.StartsWith("https://huggingface.co/ggerganov/whisper.cpp", WhisperModelManager.DefaultModelUrl);
        Assert.Equal("921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f", WhisperModelManager.DefaultModelSha256);
        Assert.Equal(77704715, WhisperModelManager.DefaultModelExpectedBytes);
    }

    [Fact]
    public async Task WhisperNetInferenceEngine_EmptyAudio_ReturnsEmptyResult()
    {
        var manager = new WhisperModelManager();
        var engine = new WhisperNetInferenceEngine(manager);

        var emptyBuffer = new AudioBuffer(Array.Empty<float>(), 16000.0);
        var result = await engine.TranscribeAsync(emptyBuffer);

        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.Text);
        Assert.Equal("whisper-net-local", result.EngineId);
    }

    [Fact]
    public async Task WhisperModelManager_EnsureModelAvailable_DownloadsAndVerifiesModel()
    {
        var manager = new WhisperModelManager();
        string modelPath = await manager.EnsureModelAvailableAsync();

        Assert.True(File.Exists(modelPath));
        Assert.True(manager.IsModelInstalledAndValid());
        Assert.Equal(ModelState.Ready, manager.State);
    }

    [Fact]
    public async Task WhisperNetInferenceEngine_RealAudio_TranscribesWithoutThrowing()
    {
        var manager = new WhisperModelManager();
        await manager.EnsureModelAvailableAsync();

        await using var engine = new WhisperNetInferenceEngine(manager);
        bool initialized = await engine.InitializeAsync();
        Assert.True(initialized);
        Assert.True(engine.Info.IsAvailable);

        // Generate 1 second of 440Hz sine wave at 16kHz
        float[] samples = new float[16000];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(0.2 * Math.Sin(2.0 * Math.PI * 440.0 * i / 16000.0));
        }

        var audio = new AudioBuffer(samples, 16000.0);
        var result = await engine.TranscribeAsync(audio);

        Assert.NotNull(result);
        Assert.Equal("whisper-net-local", result.EngineId);
        Assert.True(result.InferenceDuration.TotalMilliseconds >= 0);
    }
}
