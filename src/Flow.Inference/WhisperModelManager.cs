using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
/// Progress reporting record for local Whisper model downloads.
/// </summary>
public sealed record ModelDownloadProgress(
    string ModelName,
    long BytesDownloaded,
    long TotalBytes,
    double Percent,
    double SpeedMBps,
    string Status,
    bool IsActive = false,
    string? ErrorMessage = null
);

/// <summary>
/// Detailed status report for an available Whisper model profile.
/// </summary>
public sealed record ModelStatusInfo(
    string Name,
    string DisplayName,
    long ExpectedBytes,
    double SizeMB,
    bool IsMultilingual,
    bool IsInstalled,
    bool IsValid,
    bool IsActive,
    string Sha256,
    IReadOnlyList<string> SupportedLanguages
);

/// <summary>
/// Specification profile for a local Whisper model weight distribution.
/// </summary>
public sealed record WhisperModelProfile(
    string Name,
    string Url,
    string Sha256,
    long ExpectedBytes,
    bool IsMultilingual,
    IReadOnlyList<string> SupportedLanguages,
    string DisplayName = ""
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
        SupportedLanguages: new[] { "en" },
        DisplayName: "Tiny (English Only)"
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
        SupportedLanguages: new[] { "auto", "en", "ta", "es", "fr", "de", "hi", "zh", "ja", "ko", "it", "pt", "ru", "ar" },
        DisplayName: "Tiny (Multilingual)"
    );

    /// <summary>
    /// Official Whisper Base English-only model (~148 MB).
    /// Balanced accuracy and speed for English dictation on standard PCs.
    /// </summary>
    public static readonly WhisperModelProfile BaseEn = new(
        Name: "ggml-base.en.bin",
        Url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin",
        Sha256: "137c40403d78c540180860dc29d3b3f1643661d529099e1b2502d021e25e369b",
        ExpectedBytes: 147964211,
        IsMultilingual: false,
        SupportedLanguages: new[] { "en" },
        DisplayName: "Base (English Only)"
    );

    /// <summary>
    /// Official Whisper Small Multilingual model (~488 MB).
    /// High-precision transcription across 99 languages with deep contextual understanding.
    /// </summary>
    public static readonly WhisperModelProfile SmallMultilingual = new(
        Name: "ggml-small.bin",
        Url: "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
        Sha256: "553563db5c3b3026aa7ae48a5cdd5b4082212b6b66aa040a93a24a11d8646b2d",
        ExpectedBytes: 488154857,
        IsMultilingual: true,
        SupportedLanguages: new[] { "auto", "en", "ta", "es", "fr", "de", "hi", "zh", "ja", "ko", "it", "pt", "ru", "ar" },
        DisplayName: "Small (Multilingual - High Accuracy)"
    );

    public static readonly IReadOnlyList<WhisperModelProfile> AllProfiles = new[]
    {
        TinyEn,
        TinyMultilingual,
        BaseEn,
        SmallMultilingual
    };

    public static WhisperModelProfile? FindProfile(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        string clean = Path.GetFileName(name.Trim());
        return Array.Find((WhisperModelProfile[])AllProfiles, p =>
            string.Equals(p.Name, clean, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(p.DisplayName, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }
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
    private readonly SemaphoreSlim _downloadLock = new(1, 1);
    private CancellationTokenSource? _activeDownloadCts;

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
    /// Live progress of an active model download.
    /// </summary>
    public ModelDownloadProgress? CurrentProgress { get; private set; }

    /// <summary>
    /// Event fired whenever download progress or speed updates.
    /// </summary>
    public event Action<ModelDownloadProgress>? ProgressUpdated;

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
    /// Selects the active model profile by filename or display name.
    /// </summary>
    public bool SelectModel(string modelName)
    {
        var profile = WhisperModelProfile.FindProfile(modelName);
        if (profile == null) return false;

        ActiveProfile = profile;
        UpdateInitialState();
        return true;
    }

    /// <summary>
    /// Retrieves status for all available Whisper model profiles.
    /// </summary>
    public IReadOnlyList<ModelStatusInfo> GetAllModelStatuses()
    {
        var list = new List<ModelStatusInfo>();
        foreach (var p in WhisperModelProfile.AllProfiles)
        {
            string pPath = GetModelPath(p);
            bool exists = File.Exists(pPath);
            long fileLen = exists ? new FileInfo(pPath).Length : 0;
            bool isLenMatch = exists && fileLen == p.ExpectedBytes;
            bool valid = isLenMatch && VerifySha256(pPath, p.Sha256);
            bool active = string.Equals(ActiveProfile.Name, p.Name, StringComparison.OrdinalIgnoreCase);

            list.Add(new ModelStatusInfo(
                Name: p.Name,
                DisplayName: string.IsNullOrEmpty(p.DisplayName) ? p.Name : p.DisplayName,
                ExpectedBytes: p.ExpectedBytes,
                SizeMB: Math.Round(p.ExpectedBytes / (1024.0 * 1024.0), 1),
                IsMultilingual: p.IsMultilingual,
                IsInstalled: isLenMatch,
                IsValid: valid,
                IsActive: active,
                Sha256: p.Sha256,
                SupportedLanguages: p.SupportedLanguages
            ));
        }
        return list;
    }

    /// <summary>
    /// Cancels any currently active model download.
    /// </summary>
    public void CancelActiveDownload()
    {
        _activeDownloadCts?.Cancel();
    }

    /// <summary>
    /// Ensures the model for the requested profile is downloaded and verified, with resumable HTTP Range support.
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
            CurrentProgress = new ModelDownloadProgress(
                ModelName: targetProfile.Name,
                BytesDownloaded: targetProfile.ExpectedBytes,
                TotalBytes: targetProfile.ExpectedBytes,
                Percent: 100.0,
                SpeedMBps: 0.0,
                Status: "Ready",
                IsActive: false
            );
            return path;
        }

        await _downloadLock.WaitAsync(cancellationToken);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeDownloadCts = linkedCts;

        State = ModelState.Downloading;
        _logger?.LogInformation("Acquiring Whisper model ({Name}) from {Url} to {Path}...", targetProfile.Name, targetProfile.Url, path);

        string tempFile = path + ".download.tmp";

        try
        {
            long existingBytes = 0;
            if (File.Exists(tempFile))
            {
                var tmpInfo = new FileInfo(tempFile);
                if (tmpInfo.Length >= targetProfile.ExpectedBytes)
                {
                    try { File.Delete(tempFile); } catch { }
                }
                else
                {
                    existingBytes = tmpInfo.Length;
                }
            }

            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            // If resuming, seed hasher with existing file content
            if (existingBytes > 0)
            {
                _logger?.LogInformation("Resuming existing download for {Name} from byte offset {Offset}...", targetProfile.Name, existingBytes);
                await using var existingStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] seedBuffer = new byte[81920];
                int seedRead;
                long totalSeeded = 0;
                while (totalSeeded < existingBytes && (seedRead = await existingStream.ReadAsync(seedBuffer.AsMemory(0, (int)Math.Min(seedBuffer.Length, existingBytes - totalSeeded)), linkedCts.Token)) > 0)
                {
                    hasher.AppendData(seedBuffer, 0, seedRead);
                    totalSeeded += seedRead;
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, targetProfile.Url);
            if (existingBytes > 0)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
            }

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

            bool isPartial = response.StatusCode == HttpStatusCode.PartialContent;
            if (!isPartial && response.StatusCode != HttpStatusCode.OK)
            {
                response.EnsureSuccessStatusCode();
            }

            FileMode fileMode = FileMode.Create;
            long totalRead = 0;
            long totalBytes = targetProfile.ExpectedBytes;

            if (isPartial && existingBytes > 0)
            {
                fileMode = FileMode.Append;
                totalRead = existingBytes;
                totalBytes = response.Content.Headers.ContentRange?.Length ??
                             (response.Content.Headers.ContentLength.HasValue ? existingBytes + response.Content.Headers.ContentLength.Value : targetProfile.ExpectedBytes);
            }
            else
            {
                // Reset hasher if server returned full 200 OK
                if (existingBytes > 0)
                {
                    hasher.GetHashAndReset();
                }
                totalBytes = response.Content.Headers.ContentLength ?? targetProfile.ExpectedBytes;
                totalRead = 0;
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long bytesDownloadedThisSession = 0;
            long lastProgressReportMs = 0;

            await using (var contentStream = await response.Content.ReadAsStreamAsync(linkedCts.Token))
            await using (var fileStream = new FileStream(tempFile, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                int read;

                while ((read = await contentStream.ReadAsync(buffer, linkedCts.Token)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), linkedCts.Token);
                    hasher.AppendData(buffer, 0, read);
                    totalRead += read;
                    bytesDownloadedThisSession += read;

                    long elapsedMs = sw.ElapsedMilliseconds;
                    if (elapsedMs - lastProgressReportMs >= 150 || totalRead >= totalBytes)
                    {
                        lastProgressReportMs = elapsedMs;
                        double elapsedSec = Math.Max(0.1, elapsedMs / 1000.0);
                        double speedMBps = (bytesDownloadedThisSession / (1024.0 * 1024.0)) / elapsedSec;
                        double percent = totalBytes > 0 ? Math.Min(100.0, ((double)totalRead / totalBytes) * 100.0) : 0.0;

                        var prog = new ModelDownloadProgress(
                            ModelName: targetProfile.Name,
                            BytesDownloaded: totalRead,
                            TotalBytes: totalBytes,
                            Percent: Math.Round(percent, 1),
                            SpeedMBps: Math.Round(speedMBps, 2),
                            Status: "Downloading",
                            IsActive: true
                        );

                        CurrentProgress = prog;
                        ProgressUpdated?.Invoke(prog);
                        progress?.Report(percent / 100.0);
                    }
                }

                await fileStream.FlushAsync(linkedCts.Token);
            }

            State = ModelState.Validating;
            var valProg = new ModelDownloadProgress(
                ModelName: targetProfile.Name,
                BytesDownloaded: totalRead,
                TotalBytes: totalBytes,
                Percent: 100.0,
                SpeedMBps: 0.0,
                Status: "Validating SHA-256",
                IsActive: true
            );
            CurrentProgress = valProg;
            ProgressUpdated?.Invoke(valProg);

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
            var readyProg = new ModelDownloadProgress(
                ModelName: targetProfile.Name,
                BytesDownloaded: totalBytes,
                TotalBytes: totalBytes,
                Percent: 100.0,
                SpeedMBps: 0.0,
                Status: "Ready",
                IsActive: false
            );
            CurrentProgress = readyProg;
            ProgressUpdated?.Invoke(readyProg);

            _logger?.LogInformation("Whisper model validated and ready at {Path}.", path);
            return path;
        }
        catch (OperationCanceledException)
        {
            State = ModelState.NotInstalled;
            var cancelProg = new ModelDownloadProgress(
                ModelName: targetProfile.Name,
                BytesDownloaded: 0,
                TotalBytes: targetProfile.ExpectedBytes,
                Percent: 0,
                SpeedMBps: 0,
                Status: "Cancelled",
                IsActive: false
            );
            CurrentProgress = cancelProg;
            ProgressUpdated?.Invoke(cancelProg);
            throw;
        }
        catch (Exception ex)
        {
            State = ModelState.Failed;
            var errProg = new ModelDownloadProgress(
                ModelName: targetProfile.Name,
                BytesDownloaded: 0,
                TotalBytes: targetProfile.ExpectedBytes,
                Percent: 0,
                SpeedMBps: 0,
                Status: "Failed",
                IsActive: false,
                ErrorMessage: ex.Message
            );
            CurrentProgress = errProg;
            ProgressUpdated?.Invoke(errProg);

            _logger?.LogError(ex, "Failed to download or validate Whisper model {Name}.", targetProfile.Name);
            throw;
        }
        finally
        {
            _activeDownloadCts = null;
            _downloadLock.Release();
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
            if (IsModelInstalledAndValid(WhisperModelProfile.TinyEn))
            {
                return GetModelPath(WhisperModelProfile.TinyEn);
            }
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
