using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Audio;

namespace Flow.Core.ASR;

/// <summary>
/// Registry and fallback coordinator for speech recognition backends.
/// </summary>
public sealed class ASREngineRegistry : IAsyncDisposable
{
    private record RegisteredEngineEntry(IASREngine Engine, int Priority);

    private readonly ConcurrentDictionary<string, RegisteredEngineEntry> _engines = new(StringComparer.OrdinalIgnoreCase);
    private string? _defaultEngineId;
    private readonly object _lock = new();

    /// <summary>
    /// Gets all currently registered ASR engine descriptors.
    /// </summary>
    public IReadOnlyList<ASREngineInfo> RegisteredEngines =>
        _engines.Values.Select(e => e.Engine.Info).ToArray();

    /// <summary>
    /// Registers an engine with an optional priority and default flag.
    /// </summary>
    public void Register(IASREngine engine, bool isDefault = false, int priority = 0)
    {
        ArgumentNullException.ThrowIfNull(engine);

        var entry = new RegisteredEngineEntry(engine, priority);
        _engines[engine.Info.Id] = entry;

        lock (_lock)
        {
            if (isDefault || _defaultEngineId == null)
            {
                _defaultEngineId = engine.Info.Id;
            }
        }
    }

    /// <summary>
    /// Retrieves an engine by its identifier.
    /// </summary>
    public IASREngine? GetEngine(string id)
    {
        return _engines.TryGetValue(id, out var entry) ? entry.Engine : null;
    }

    /// <summary>
    /// Gets the preferred or default ASR engine.
    /// </summary>
    public IASREngine? GetPreferredEngine()
    {
        lock (_lock)
        {
            if (_defaultEngineId != null && _engines.TryGetValue(_defaultEngineId, out var defaultEntry) && defaultEntry.Engine.Info.IsAvailable)
            {
                return defaultEntry.Engine;
            }

            return _engines.Values
                .Where(e => e.Engine.Info.IsAvailable)
                .OrderByDescending(e => e.Priority)
                .Select(e => e.Engine)
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Executes transcription using the preferred engine, falling back to secondary engines on failure.
    /// </summary>
    public async Task<ASRResult> TranscribeWithFallbackAsync(
        AudioBuffer audio,
        ASROptions? options = null,
        IProgress<ASRSegment>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = _engines.Values
            .Where(e => e.Engine.Info.IsAvailable)
            .OrderByDescending(e => e.Priority)
            .Select(e => e.Engine)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No speech recognition engines are available in the registry.");
        }

        List<Exception> errors = new();

        foreach (var engine in candidates)
        {
            try
            {
                return await engine.TranscribeAsync(audio, options, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(new ASRException(engine.Info.Id, $"Engine failed during fallback execution.", ex));
            }
        }

        throw new AggregateException("All speech recognition engines in the fallback chain failed.", errors);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var entry in _engines.Values)
        {
            await entry.Engine.DisposeAsync();
        }
        _engines.Clear();
    }
}
