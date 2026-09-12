using System;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Context;

namespace Flow.Core.History;

/// <summary>
/// Central coordinator for FLOW's History & Productivity subsystem (WF-039 through WF-045).
/// Coordinates repository, search, retention, statistics, export, and privacy gates.
/// </summary>
public sealed class HistoryService : IHistoryService
{
    public IHistoryRepository Repository { get; }
    public IHistorySearchService Search { get; }
    public IProductivityStatisticsService Statistics { get; }
    public IHistoryRetentionService Retention { get; }
    public IHistoryExportService Export { get; }
    public IHistoryPrivacyService Privacy { get; }

    public HistoryService(
        IHistoryRepository repository,
        IHistorySearchService search,
        IProductivityStatisticsService statistics,
        IHistoryRetentionService retention,
        IHistoryExportService export,
        IHistoryPrivacyService privacy)
    {
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        Search = search ?? throw new ArgumentNullException(nameof(search));
        Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));
        Retention = retention ?? throw new ArgumentNullException(nameof(retention));
        Export = export ?? throw new ArgumentNullException(nameof(export));
        Privacy = privacy ?? throw new ArgumentNullException(nameof(privacy));
    }

    public async Task<DictationEntry?> RecordDictationAsync(
        Guid sessionId,
        string text,
        TimeSpan duration,
        string language,
        ContextSnapshot? context,
        string mode = "Dictation",
        HistoryState state = HistoryState.Completed,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var settings = await Repository.GetSettingsAsync(ct);
        if (!settings.HistoryEnabled && !settings.StatisticsCollectionEnabled)
        {
            return null;
        }

        bool safe = Privacy.IsSafeToPersist(context);
        bool saveText = safe && settings.SaveTranscriptText && settings.HistoryEnabled;

        string? persistText = saveText ? text : null;
        HistoryState effectiveState = safe ? state : HistoryState.Excluded;

        int charCount = text?.Length ?? 0;
        int wordCount = CountWords(text);

        var entry = new DictationEntry(
            Id: Guid.NewGuid().ToString("N"),
            SessionId: sessionId,
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: (long)duration.TotalMilliseconds,
            CharacterCount: charCount,
            WordCount: wordCount,
            Language: language ?? "en",
            Application: context?.TargetInfo?.ProcessName ?? "Unknown",
            ApplicationCategory: context?.Category.ToString() ?? "Unknown",
            Mode: mode,
            State: effectiveState,
            WasEdited: false,
            IsFavorite: false,
            Text: persistText,
            TextHash: Privacy.ComputeTextHash(persistText),
            MetadataJson: metadataJson
        );

        string id = await Repository.InsertAsync(entry, ct);
        return entry with { Id = id };
    }

    public Task<HistorySettings> GetSettingsAsync(CancellationToken ct = default)
    {
        return Repository.GetSettingsAsync(ct);
    }

    public Task UpdateSettingsAsync(HistorySettings settings, CancellationToken ct = default)
    {
        return Repository.SaveSettingsAsync(settings, ct);
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
