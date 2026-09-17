using System;
using System.IO;
using System.Linq;
using Flow.Inference;
using Xunit;

namespace Flow.Windows.Tests;

public sealed class HardwareAccelerationDetectorTests
{
    [Fact]
    public void HardwareAccelerationDetector_Detect_ReturnsValidProfile()
    {
        var profile = HardwareAccelerationDetector.Detect();

        Assert.NotNull(profile);
        Assert.False(string.IsNullOrWhiteSpace(profile.PrimaryGpuName));
        Assert.InRange(profile.OptimalCpuThreads, 2, Math.Max(2, Environment.ProcessorCount));
        Assert.False(string.IsNullOrWhiteSpace(profile.CpuArchitecture));
        Assert.NotEmpty(profile.AllDetectedGpus);
        Assert.True(Enum.IsDefined(typeof(HardwareAccelerationType), profile.RecommendedBackend));
        Assert.True(profile.DedicatedVramMB >= 0);
    }

    [Fact]
    public void WhisperModelProfile_AllProfiles_AreWellFormedAndVerified()
    {
        Assert.Equal(4, WhisperModelProfile.AllProfiles.Count);

        foreach (var profile in WhisperModelProfile.AllProfiles)
        {
            Assert.False(string.IsNullOrWhiteSpace(profile.Name), "Profile name must not be empty.");
            Assert.False(string.IsNullOrWhiteSpace(profile.DisplayName), "Profile display name must not be empty.");
            Assert.StartsWith("https://", profile.Url, StringComparison.OrdinalIgnoreCase);
            Assert.True(profile.ExpectedBytes > 10_000_000, "Model file size must be > 10MB.");
            Assert.Equal(64, profile.Sha256.Length);
            Assert.All(profile.Sha256, c => Assert.True(Uri.IsHexDigit(c), $"Character '{c}' in SHA-256 must be hex."));
            Assert.NotEmpty(profile.SupportedLanguages);
        }

        // Verify specific known models
        Assert.Contains(WhisperModelProfile.AllProfiles, p => p.Name == "ggml-tiny.en.bin");
        Assert.Contains(WhisperModelProfile.AllProfiles, p => p.Name == "ggml-tiny.bin");
        Assert.Contains(WhisperModelProfile.AllProfiles, p => p.Name == "ggml-base.en.bin");
        Assert.Contains(WhisperModelProfile.AllProfiles, p => p.Name == "ggml-small.bin");
    }

    [Theory]
    [InlineData("ggml-tiny.en.bin", "ggml-tiny.en.bin")]
    [InlineData("ggml-tiny.bin", "ggml-tiny.bin")]
    [InlineData("ggml-base.en.bin", "ggml-base.en.bin")]
    [InlineData("ggml-small.bin", "ggml-small.bin")]
    [InlineData("Tiny (English Only)", "ggml-tiny.en.bin")]
    [InlineData("Base (English Only)", "ggml-base.en.bin")]
    [InlineData("Small (Multilingual - High Accuracy)", "ggml-small.bin")]
    public void WhisperModelProfile_FindProfile_LocatesProfilesAccurately(string query, string expectedName)
    {
        var found = WhisperModelProfile.FindProfile(query);
        Assert.NotNull(found);
        Assert.Equal(expectedName, found.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ggml-nonexistent.bin")]
    public void WhisperModelProfile_FindProfile_ReturnsNullForInvalidInput(string? query)
    {
        var found = WhisperModelProfile.FindProfile(query);
        Assert.Null(found);
    }

    [Fact]
    public void WhisperModelManager_ModelStatuses_ReflectAllProfiles()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "flow_test_models_" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = new WhisperModelManager(tempDir);
            var statuses = manager.GetAllModelStatuses();

            Assert.Equal(4, statuses.Count);
            Assert.All(statuses, s =>
            {
                Assert.False(string.IsNullOrWhiteSpace(s.Name));
                Assert.False(string.IsNullOrWhiteSpace(s.DisplayName));
                Assert.True(s.ExpectedBytes > 0);
                Assert.True(s.SizeMB > 0);
            });

            // Default active is TinyEn
            Assert.True(manager.ActiveProfile == WhisperModelProfile.TinyEn);
            var activeStatus = statuses.First(s => s.Name == "ggml-tiny.en.bin");
            Assert.True(activeStatus.IsActive);

            // Change active model
            bool switched = manager.SelectModel("ggml-base.en.bin");
            Assert.True(switched);
            Assert.Equal("ggml-base.en.bin", manager.ActiveProfile.Name);
            Assert.EndsWith("ggml-base.en.bin", manager.ModelPath);

            var updatedStatuses = manager.GetAllModelStatuses();
            var baseStatus = updatedStatuses.First(s => s.Name == "ggml-base.en.bin");
            Assert.True(baseStatus.IsActive);

            // Invalid model select returns false
            Assert.False(manager.SelectModel("unknown-model"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void ModelDownloadProgress_RecordContract_MaintainsIntegrity()
    {
        var progress = new ModelDownloadProgress(
            ModelName: "ggml-base.en.bin",
            BytesDownloaded: 50_000_000,
            TotalBytes: 147_964_211,
            Percent: 33.8,
            SpeedMBps: 14.5,
            Status: "Downloading",
            IsActive: true
        );

        Assert.Equal("ggml-base.en.bin", progress.ModelName);
        Assert.Equal(50_000_000, progress.BytesDownloaded);
        Assert.Equal(147_964_211, progress.TotalBytes);
        Assert.Equal(33.8, progress.Percent);
        Assert.Equal(14.5, progress.SpeedMBps);
        Assert.Equal("Downloading", progress.Status);
        Assert.True(progress.IsActive);
        Assert.Null(progress.ErrorMessage);
    }
}
