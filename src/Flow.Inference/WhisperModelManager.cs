using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Flow.Inference;

public enum ModelState
{
    NotInstalled,
    Downloading,
    Validating,
    Loading,
    Ready,
    Failed
}

/// <summary>
/// Specification profile for a local Whisper model weight distribution.
/// </summary>
public sealed record WhisperModelProfile(
    string Name,
    string Url,
    string Sha256,
    long ExpectedBytes,
    bool IsMultilingual,
    IReadOnlyList<string> SupportedLanguages
)
{
    /// <summary>
    /// Official Whisper Tiny English-only model (~77.7 MB).
    /// Optimized for low-latency English dictation.
    /// </summary>
    public static readonly WhisperModelProfile TinyEn = new(
        Name: "ggml-tiny.en.bin",
        Url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin",
        Sha256: "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f",
        ExpectedBytes: 77704715,
        IsMultilingual: false,
        SupportedLanguages: new[] { "en" }
    );

    /// <summary>
    /// Official Whisper Tiny Multilingual model (~77.7 MB).
    /// Supports 99 languages including English ("en") and Tamil ("ta"),
    /// language auto-detection ("auto"), and code-switching vocabulary biasing.
    /// </summary>
    public static readonly WhisperModelProfile TinyMultilingual = new(
        Name: "ggml-tiny.bin",
        Url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin",
        Sha256: "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21",
        ExpectedBytes: 77691713,
        IsMultilingual: true,
        SupportedLanguages: new[] { "auto", "en", "ta", "es", "fr", "de", "hi", "zh", "ja", "ko", "it", "pt", "ru", "ar" }
    );
}

/// <summary>
/// Manages local Whisper GGML model acquisition, SHA-256 verification, and lifecycle.
/// Strictly downloads from official repositories with hash verification and atomic file installation.
/// Supports both English-only and Multilingual model profiles.
/// </summary>
public sealed class WhisperModelManager
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    public const string DefaultModelName = "ggml-tiny.en.bin";
    public const string DefaultModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin";
    public const string DefaultModelSha256 = "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f";
    public const long DefaultModelExpectedBytes = 77704715;

    public const string MultilingualModelName = "ggml-tiny.bin";
    public const string MultilingualModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";
    public const string MultilingualModelSha256 = "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21";
    public const long MultilingualModelExpectedBytes = 77691713;

    private readonly string _modelsDirectory;
    private readonly ILogger<WhisperModelManager>? _logger;
    private ModelState _state = ModelState.NotInstalled;
    private readonly object _stateLock = new();

    public ModelState State
    {
        get
        {
            lock (_stateLock) return _state;
        }
        private set
        {
            lock (_stateLock) _state = value;
            StateChanged?.Invoke(value);
        }
    }

    public event Action<ModelState>? StateChanged;

    /// <summary>
    /// Currently active model profile. Defaults to TinyEn for backward compatibility.
    /// </summary>
    public WhisperModelProfile ActiveProfile { get; set; } = WhisperModelProfile.TinyEn;

    public string ModelPath => GetModelPath(ActiveProfile);

    public WhisperModelManager(string? modelsDirectory = null, ILogger<WhisperModelManager>? logger = null)
    {
        _logger = logger;
        _modelsDirectory = modelsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FLOW",
            "models"
        );

        Directory.CreateDirectory(_modelsDirectory);
        UpdateInitialState();
    }

    public string GetModelPath(WhisperModelProfile? profile = null)
    {
        return Path.Combine(_modelsDirectory, (profile ?? ActiveProfile).Name);
    }

    private void UpdateInitialState()
    {
        if (File.Exists(ModelPath))
        {
            var info = new FileInfo(ModelPath);
            if (info.Length == ActiveProfile.ExpectedBytes)
            {
                State = ModelState.Ready;
                return;
            }
        }
        State = ModelState.NotInstalled;
    }

    /// <summary>
    /// Verifies whether the specified or active model exists and passes integrity checks.
    /// </summary>
    public bool IsModelInstalledAndValid(WhisperModelProfile? profile = null)
    {
        var targetProfile = profile ?? ActiveProfile;
        string path = GetModelPath(targetProfile);
        if (!File.Exists(path)) return false;

        var info = new FileInfo(path);
        if (info.Length != targetProfile.ExpectedBytes) return false;

        return VerifySha256(path, targetProfile.Sha256);
    }

    /// <summary>
    /// Ensures the model for the requested profile is downloaded and verified, with progress reporting.
    /// </summary>
    public async Task<string> EnsureModelAvailableAsync(
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default,
        WhisperModelProfile? profile = null)
    {
        var targetProfile = profile ?? ActiveProfile;
        string path = GetModelPath(targetProfile);

        if (IsModelInstalledAndValid(targetProfile))
        {
            State = ModelState.Ready;
            return path;
        }

        State = ModelState.Downloading;
        _logger?.LogInformation("Downloading Whisper model ({Name}) from {Url} to {Path}...", targetProfile.Name, targetProfile.Url, path);

        string tempFile = path + ".download.tmp";

        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalRead = 0;
            long totalBytes = targetProfile.ExpectedBytes;

            using (var response = await HttpClient.GetAsync(targetProfile.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                totalBytes = response.Content.Headers.ContentLength ?? targetProfile.ExpectedBytes;

                await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        hasher.AppendData(buffer, 0, read);
                        totalRead += read;

                        if (totalBytes > 0)
                        {
                            double percentage = (double)totalRead / totalBytes;
                            progress?.Report(percentage);
                        }
                    }

                    await fileStream.FlushAsync(cancellationToken);
                }
            }

            State = ModelState.Validating;
            _logger?.LogInformation("Validating downloaded model checksum for {Name}...", targetProfile.Name);

            byte[] hashBytes = hasher.GetHashAndReset();
            string actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (!string.Equals(actualHash, targetProfile.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Downloaded model failed SHA-256 integrity verification. Expected: {targetProfile.Sha256}, Actual: {actualHash}, Bytes: {totalRead}/{totalBytes}");
            }

            // Atomic file replacement
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(tempFile, path);

            State = ModelState.Ready;
            _logger?.LogInformation("Whisper model validated and ready at {Path}.", path);
            return path;
        }
        catch (Exception ex)
        {
            State = ModelState.Failed;
            _logger?.LogError(ex, "Failed to download or validate Whisper model.");
            if (File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
            throw;
        }
    }

    /// <summary>
    /// Resolves and ensures the appropriate Whisper model based on requested language.
    /// Uses TinyMultilingual for "ta", "auto", or non-English speech, and TinyEn for "en".
    /// </summary>
    public async Task<string> EnsureModelForLanguageAsync(
        string? language,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(language) || string.Equals(language, "en", StringComparison.OrdinalIgnoreCase))
        {
            // If multilingual model is already installed, it works for English too
            if (IsModelInstalledAndValid(WhisperModelProfile.TinyMultilingual))
            {
                return GetModelPath(WhisperModelProfile.TinyMultilingual);
            }
            return await EnsureModelAvailableAsync(progress, cancellationToken, WhisperModelProfile.TinyEn);
        }

        // Multilingual or auto-detect requested
        return await EnsureModelAvailableAsync(progress, cancellationToken, WhisperModelProfile.TinyMultilingual);
    }

    private static bool VerifySha256(string filePath, string expectedHash)
    {
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] hashBytes = sha256.ComputeHash(stream);
            string actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
