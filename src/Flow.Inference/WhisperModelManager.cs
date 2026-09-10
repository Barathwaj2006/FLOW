using System;
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
/// Manages local Whisper GGML/ONNX model acquisition, SHA-256 verification, and lifecycle.
/// Strictly downloads from official repositories with hash verification and atomic file installation.
/// </summary>
public sealed class WhisperModelManager
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(10) };

    public const string DefaultModelName = "ggml-tiny.en.bin";
    public const string DefaultModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin";
    public const string DefaultModelSha256 = "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f";
    public const long DefaultModelExpectedBytes = 77704715;

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

    public string ModelPath => Path.Combine(_modelsDirectory, DefaultModelName);

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

    private void UpdateInitialState()
    {
        if (File.Exists(ModelPath))
        {
            var info = new FileInfo(ModelPath);
            if (info.Length == DefaultModelExpectedBytes)
            {
                State = ModelState.Ready;
                return;
            }
        }
        State = ModelState.NotInstalled;
    }

    /// <summary>
    /// Verifies whether the default model exists and passes integrity checks.
    /// </summary>
    public bool IsModelInstalledAndValid()
    {
        if (!File.Exists(ModelPath)) return false;

        var info = new FileInfo(ModelPath);
        if (info.Length != DefaultModelExpectedBytes) return false;

        return VerifySha256(ModelPath, DefaultModelSha256);
    }

    /// <summary>
    /// Ensures model is downloaded and verified, with progress reporting.
    /// </summary>
    public async Task<string> EnsureModelAvailableAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (IsModelInstalledAndValid())
        {
            State = ModelState.Ready;
            return ModelPath;
        }

        State = ModelState.Downloading;
        _logger?.LogInformation("Downloading Whisper model from {Url} to {Path}...", DefaultModelUrl, ModelPath);

        string tempFile = ModelPath + ".download.tmp";

        try
        {
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long totalRead = 0;
            long totalBytes = DefaultModelExpectedBytes;

            using (var response = await HttpClient.GetAsync(DefaultModelUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                totalBytes = response.Content.Headers.ContentLength ?? DefaultModelExpectedBytes;

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
            _logger?.LogInformation("Validating downloaded model checksum...");

            byte[] hashBytes = hasher.GetHashAndReset();
            string actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (!string.Equals(actualHash, DefaultModelSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Downloaded model failed SHA-256 integrity verification. Expected: {DefaultModelSha256}, Actual: {actualHash}, Bytes: {totalRead}/{totalBytes}");
            }

            // Atomic file replacement
            if (File.Exists(ModelPath))
            {
                File.Delete(ModelPath);
            }
            File.Move(tempFile, ModelPath);

            State = ModelState.Ready;
            _logger?.LogInformation("Whisper model validated and ready at {Path}.", ModelPath);
            return ModelPath;
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
