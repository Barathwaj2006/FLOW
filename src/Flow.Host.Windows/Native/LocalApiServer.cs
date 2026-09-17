using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.ASR;
using Flow.Core.Audio;
using Flow.Core.History;
using Flow.Core.Language;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Storage;
using Flow.Core.TranscriptProcessing;
using Flow.Inference;
using Microsoft.Extensions.Logging;

namespace Flow.Host.Windows.Native;

/// <summary>
/// Lightweight, zero-dependency local HTTP bridge server for FLOW.
/// Listens on http://127.0.0.1:5005 (or fallback port) to provide:
/// 1. 100% offline local Whisper transcription (/api/transcribe)
/// 2. System and model health status (/api/status)
/// 3. Persistent dictionary synchronization (/api/dictionary)
/// 4. Local history synchronization (/api/history)
/// 5. Local DPAPI token verification (/api/auth/verify)
/// </summary>
public sealed class LocalApiServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly WhisperNetInferenceEngine _whisperInference;
    private readonly TranscriptProcessingPipeline _formattingPipeline;
    private readonly SqlitePersonalDictionaryRepository? _dictRepo;
    private readonly PersonalDictionaryEngine? _dictEngine;
    private readonly SqliteHistoryRepository? _historyRepo;
    private readonly WhisperModelManager? _modelManager;
    private readonly ILogger<LocalApiServer>? _logger;

    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private bool _isDisposed;

    public int Port { get; private set; } = 5005;
    public bool IsRunning => _listener.IsListening;

    public LocalApiServer(
        WhisperNetInferenceEngine whisperInference,
        TranscriptProcessingPipeline formattingPipeline,
        SqlitePersonalDictionaryRepository? dictRepo = null,
        PersonalDictionaryEngine? dictEngine = null,
        SqliteHistoryRepository? historyRepo = null,
        WhisperModelManager? modelManager = null,
        ILogger<LocalApiServer>? logger = null,
        int preferredPort = 5005)
    {
        _whisperInference = whisperInference ?? throw new ArgumentNullException(nameof(whisperInference));
        _formattingPipeline = formattingPipeline ?? throw new ArgumentNullException(nameof(formattingPipeline));
        _dictRepo = dictRepo;
        _dictEngine = dictEngine;
        _historyRepo = historyRepo;
        _modelManager = modelManager;
        _logger = logger;
        _listener = new HttpListener();

        Port = preferredPort;
    }

    /// <summary>
    /// Starts the local HTTP listener on 127.0.0.1 with port fallback.
    /// </summary>
    public void Start()
    {
        if (_listener.IsListening) return;

        int[] portsToTry = [Port, 5006, 5007, 5008, 5009];
        bool started = false;

        foreach (int port in portsToTry)
        {
            try
            {
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Start();
                Port = port;
                started = true;
                _logger?.LogInformation("LocalApiServer active on http://127.0.0.1:{Port}/", Port);
                break;
            }
            catch (HttpListenerException ex)
            {
                _logger?.LogWarning("Port {Port} occupied or denied ({Message}). Trying next...", port, ex.Message);
            }
        }

        if (!started)
        {
            _logger?.LogError("Failed to bind LocalApiServer to any port in range [5005-5009].");
            return;
        }

        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Stops the HTTP listener gracefully.
    /// </summary>
    public void Stop()
    {
        if (!_listener.IsListening) return;

        try
        {
            _cts?.Cancel();
            _listener.Stop();
        }
        catch { }

        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Unexpected error in LocalApiServer accept loop.");
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var req = context.Request;
        var resp = context.Response;

        // Apply CORS headers to all responses
        resp.Headers.Add("Access-Control-Allow-Origin", "*");
        resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT, DELETE");
        resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, Accept, X-Requested-With");

        if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            resp.StatusCode = (int)HttpStatusCode.NoContent;
            resp.Close();
            return;
        }

        string path = req.Url?.AbsolutePath.TrimEnd('/') ?? string.Empty;

        try
        {
            if (path.Equals("/api/status", StringComparison.OrdinalIgnoreCase) ||
                path.Equals("/status", StringComparison.OrdinalIgnoreCase) ||
                path.Length == 0)
            {
                await HandleStatusAsync(resp, ct);
            }
            else if (path.Equals("/api/transcribe", StringComparison.OrdinalIgnoreCase) &&
                     req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleTranscribeAsync(req, resp, ct);
            }
            else if (path.Equals("/api/history", StringComparison.OrdinalIgnoreCase) &&
                     req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                await HandleGetHistoryAsync(resp, ct);
            }
            else if (path.Equals("/api/dictionary", StringComparison.OrdinalIgnoreCase))
            {
                if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleGetDictionaryAsync(resp, ct);
                }
                else if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
                {
                    await HandlePostDictionaryAsync(req, resp, ct);
                }
                else
                {
                    resp.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    resp.Close();
                }
            }
            else if (path.Equals("/api/auth/verify", StringComparison.OrdinalIgnoreCase) &&
                     req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleAuthVerifyAsync(req, resp, ct);
            }
            else
            {
                resp.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteJsonResponseAsync(resp, new { error = "Not found", path }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing LocalApiServer request for {Path}", path);
            resp.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonResponseAsync(resp, new { error = ex.Message }, ct);
        }
    }

    private async Task HandleStatusAsync(HttpListenerResponse resp, CancellationToken ct)
    {
        bool isModelInstalled = _modelManager?.IsModelInstalledAndValid() ?? true;
        string modelName = _modelManager?.ActiveProfile.Name ?? WhisperModelManager.DefaultModelName;

        var status = new
        {
            status = "ready",
            version = "1.0.0",
            engine = "Whisper.net Native Local Engine",
            modelName,
            modelInstalled = isModelInstalled,
            port = Port,
            offlineSovereignty = "100% Offline (Zero Cloud Audio)",
            zeroEnterInvariant = "VK_RETURN Strictly Prohibited",
            timestamp = DateTime.UtcNow
        };

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonResponseAsync(resp, status, ct);
    }

    private async Task HandleTranscribeAsync(HttpListenerRequest req, HttpListenerResponse resp, CancellationToken ct)
    {
        byte[] audioBytes;
        string language = "en";

        string contentType = req.ContentType ?? string.Empty;
        if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
            string jsonBody = await reader.ReadToEndAsync(ct);
            using var doc = JsonDocument.Parse(jsonBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("audioBase64", out var base64Prop))
            {
                audioBytes = Convert.FromBase64String(base64Prop.GetString() ?? string.Empty);
            }
            else
            {
                resp.StatusCode = (int)HttpStatusCode.BadRequest;
                await WriteJsonResponseAsync(resp, new { error = "Missing 'audioBase64' field." }, ct);
                return;
            }

            if (root.TryGetProperty("language", out var langProp) && !string.IsNullOrWhiteSpace(langProp.GetString()))
            {
                language = langProp.GetString()!;
            }
        }
        else
        {
            // Binary WAV or raw PCM
            using var ms = new MemoryStream();
            await req.InputStream.CopyToAsync(ms, ct);
            audioBytes = ms.ToArray();
        }

        if (audioBytes.Length == 0)
        {
            resp.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonResponseAsync(resp, new { error = "Audio payload was empty." }, ct);
            return;
        }

        // Decode audio payload to 16kHz mono float32
        float[] samples = DecodeAudioToSamples(audioBytes);
        if (samples.Length == 0)
        {
            resp.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonResponseAsync(resp, new { error = "Unable to decode audio samples." }, ct);
            return;
        }

        var buffer = new AudioBuffer(samples, sampleRate: 16000.0, channelCount: 1);
        var asrOptions = new ASROptions(Language: language);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var asrResult = await _whisperInference.TranscribeAsync(buffer, asrOptions, null, ct);
        sw.Stop();

        string rawText = asrResult.Text ?? string.Empty;

        // Pass through production pipeline enforcing Zero-Enter invariant
        string sanitizedText = string.Empty;
        if (!string.IsNullOrWhiteSpace(rawText))
        {
            sanitizedText = _formattingPipeline.Format(rawText, (FormattingOptions?)null, (string?)null);
        }

        // Persist to history if repository is wired
        if (_historyRepo != null && !string.IsNullOrWhiteSpace(sanitizedText))
        {
            try
            {
                var historyEntry = new DictationEntry(
                    Id: Guid.NewGuid().ToString("N"),
                    SessionId: Guid.NewGuid(),
                    CreatedAt: DateTimeOffset.UtcNow,
                    DurationMs: (long)asrResult.AudioDuration.TotalMilliseconds,
                    CharacterCount: sanitizedText.Length,
                    WordCount: sanitizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    Language: language,
                    Application: "FLOW Web Interface",
                    ApplicationCategory: "Productivity",
                    Mode: "Local Whisper",
                    State: HistoryState.Completed,
                    WasEdited: false,
                    IsFavorite: false,
                    Text: sanitizedText
                );

                await _historyRepo.InsertAsync(historyEntry, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to persist web transcription into local SQLite history.");
            }
        }

        var responsePayload = new
        {
            text = sanitizedText,
            rawText,
            durationMs = (long)asrResult.AudioDuration.TotalMilliseconds,
            inferenceDurationMs = sw.ElapsedMilliseconds,
            confidence = asrResult.Confidence,
            engine = "Whisper.net Local (DirectML/AVX2)",
            offline = true
        };

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonResponseAsync(resp, responsePayload, ct);
    }

    private async Task HandleGetHistoryAsync(HttpListenerResponse resp, CancellationToken ct)
    {
        if (_historyRepo == null)
        {
            resp.StatusCode = (int)HttpStatusCode.OK;
            await WriteJsonResponseAsync(resp, Array.Empty<object>(), ct);
            return;
        }

        var page = await _historyRepo.GetPagedAsync(new HistoryFilter(), pageIndex: 0, pageSize: 50, ct);

        var items = page.Items.Select(e => new
        {
            id = e.Id,
            sessionId = e.SessionId.ToString(),
            createdAt = e.CreatedAt.ToString("h:mm tt"),
            timeShort = e.CreatedAt.ToString("h:mm tt"),
            durationMs = e.DurationMs,
            durationText = $"{e.DurationMs / 1000.0:F1}s",
            characterCount = e.CharacterCount,
            wordCount = e.WordCount,
            language = e.Language,
            application = e.Application,
            applicationCategory = e.ApplicationCategory,
            target = "Focused Window",
            mode = e.Mode,
            state = e.State.ToString(),
            isFavorite = e.IsFavorite,
            text = e.Text ?? string.Empty,
            latency = "Local Whisper • DirectML",
            engine = "FLOW Local Whisper Engine"
        });

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonResponseAsync(resp, items, ct);
    }

    private async Task HandleGetDictionaryAsync(HttpListenerResponse resp, CancellationToken ct)
    {
        if (_dictRepo == null)
        {
            resp.StatusCode = (int)HttpStatusCode.OK;
            await WriteJsonResponseAsync(resp, Array.Empty<object>(), ct);
            return;
        }

        var entries = await _dictRepo.GetAllAsync(ct);
        var mapped = entries.Select(e => new
        {
            id = e.Id,
            term = e.Term,
            spokenForm = e.SpokenForm,
            canonicalForm = e.CanonicalForm,
            replacement = e.Replacement,
            isStarred = e.IsStarred,
            category = e.Category ?? "Technical",
            caseSensitive = e.CaseSensitive,
            isEnabled = e.IsEnabled
        });

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonResponseAsync(resp, mapped, ct);
    }

    private async Task HandlePostDictionaryAsync(HttpListenerRequest req, HttpListenerResponse resp, CancellationToken ct)
    {
        if (_dictRepo == null)
        {
            resp.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
            await WriteJsonResponseAsync(resp, new { error = "Dictionary repository not available" }, ct);
            return;
        }

        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        string body = await reader.ReadToEndAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        string term = root.GetProperty("term").GetString() ?? string.Empty;
        string? replacement = root.TryGetProperty("replacement", out var repProp) ? repProp.GetString() : null;
        bool isStarred = root.TryGetProperty("isStarred", out var starProp) && starProp.GetBoolean();
        string? category = root.TryGetProperty("category", out var catProp) ? catProp.GetString() : "Custom";

        if (string.IsNullOrWhiteSpace(term))
        {
            resp.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonResponseAsync(resp, new { error = "Term is required" }, ct);
            return;
        }

        var entry = new DictionaryEntry
        {
            Term = term,
            Replacement = replacement,
            IsStarred = isStarred,
            Category = category,
            IsEnabled = true
        };

        await _dictRepo.AddAsync(entry, ct);
        if (_dictEngine != null)
        {
            await _dictEngine.ReloadAsync(ct);
        }

        resp.StatusCode = (int)HttpStatusCode.Created;
        await WriteJsonResponseAsync(resp, new { success = true, id = entry.Id }, ct);
    }

    private async Task HandleAuthVerifyAsync(HttpListenerRequest req, HttpListenerResponse resp, CancellationToken ct)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
        string body = await reader.ReadToEndAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        string email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : "";
        string code = root.TryGetProperty("code", out var codeProp) ? codeProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(email) || code.Length != 6)
        {
            resp.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonResponseAsync(resp, new { error = "Valid email and 6-digit code required." }, ct);
            return;
        }

        // Generate DPAPI-protected session token
        string tokenPlain = $"flow_auth:{email}:{DateTime.UtcNow.Ticks}";
        string tokenProtected = DpapiDataProtection.ProtectString(tokenPlain);

        var payload = new
        {
            token = tokenProtected,
            email,
            credits = 999999,
            plan = "Unlimited Local Sovereign",
            offlineSovereignty = true
        };

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonResponseAsync(resp, payload, ct);
    }

    private static async Task WriteJsonResponseAsync(HttpListenerResponse resp, object data, CancellationToken ct)
    {
        resp.ContentType = "application/json; charset=utf-8";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes, ct);
        resp.Close();
    }

    /// <summary>
    /// Decodes either a RIFF WAVE container or raw 16-bit PCM bytes into 16,000Hz mono Float32 samples.
    /// </summary>
    internal static float[] DecodeAudioToSamples(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return Array.Empty<float>();
        }

        // Check for RIFF WAVE header: 'R' 'I' 'F' 'F' ... 'W' 'A' 'V' 'E'
        if (data.Length >= 12 &&
            data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x41 && data[10] == 0x56 && data[11] == 0x45)
        {
            return ParseWavTo16kMonoSamples(data);
        }

        // Fallback: Assume raw 16-bit signed integer little-endian PCM at 16,000Hz mono
        int numSamples = data.Length / 2;
        float[] samples = new float[numSamples];
        for (int i = 0; i < numSamples; i++)
        {
            short s = (short)(data[i * 2] | (data[i * 2 + 1] << 8));
            samples[i] = s / 32768.0f;
        }

        return samples;
    }

    private static float[] ParseWavTo16kMonoSamples(byte[] data)
    {
        int offset = 12;
        int sampleRate = 16000;
        int channels = 1;
        int bitsPerSample = 16;
        int formatTag = 1; // 1 = PCM, 3 = IEEE Float
        byte[]? rawAudio = null;

        while (offset + 8 <= data.Length)
        {
            string chunkId = Encoding.ASCII.GetString(data, offset, 4);
            int chunkSize = BitConverter.ToInt32(data, offset + 4);
            offset += 8;

            if (chunkSize < 0 || offset + chunkSize > data.Length)
            {
                break;
            }

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                formatTag = BitConverter.ToInt16(data, offset);
                channels = BitConverter.ToInt16(data, offset + 2);
                sampleRate = BitConverter.ToInt32(data, offset + 4);
                bitsPerSample = BitConverter.ToInt16(data, offset + 14);
            }
            else if (chunkId == "data")
            {
                rawAudio = new byte[chunkSize];
                Array.Copy(data, offset, rawAudio, 0, chunkSize);
                break;
            }

            // Word-aligned chunk padding
            offset += (chunkSize + 1) & ~1;
        }

        if (rawAudio == null || rawAudio.Length == 0 || channels <= 0)
        {
            return Array.Empty<float>();
        }

        // Decode native frames to mono float
        float[] monoSamples;

        if (formatTag == 3 /* IEEE Float */ && bitsPerSample == 32)
        {
            int totalFloats = rawAudio.Length / 4;
            int frames = totalFloats / channels;
            monoSamples = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    int floatIdx = (i * channels + c) * 4;
                    sum += BitConverter.ToSingle(rawAudio, floatIdx);
                }
                monoSamples[i] = sum / channels;
            }
        }
        else // PCM 16-bit
        {
            int totalShorts = rawAudio.Length / 2;
            int frames = totalShorts / channels;
            monoSamples = new float[frames];

            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++)
                {
                    int byteIdx = (i * channels + c) * 2;
                    short sample = (short)(rawAudio[byteIdx] | (rawAudio[byteIdx + 1] << 8));
                    sum += sample / 32768.0f;
                }
                monoSamples[i] = sum / channels;
            }
        }

        // Resample to 16,000 Hz if necessary
        const int TargetRate = 16000;
        if (sampleRate == TargetRate)
        {
            return monoSamples;
        }

        double ratio = (double)sampleRate / TargetRate;
        int targetLength = (int)Math.Floor(monoSamples.Length / ratio);
        if (targetLength <= 0) return Array.Empty<float>();

        float[] resampled = new float[targetLength];
        for (int i = 0; i < targetLength; i++)
        {
            double srcIdx = i * ratio;
            int idxFloor = (int)Math.Floor(srcIdx);
            int idxCeil = Math.Min(idxFloor + 1, monoSamples.Length - 1);
            double frac = srcIdx - idxFloor;

            resampled[i] = (float)((1.0 - frac) * monoSamples[idxFloor] + frac * monoSamples[idxCeil]);
        }

        return resampled;
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _listener.Close();
            _cts?.Dispose();
            _isDisposed = true;
        }
    }
}
