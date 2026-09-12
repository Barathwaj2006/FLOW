using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Context;

namespace Flow.Core.History;

public interface IHistoryRepository
{
    Task<string> InsertAsync(DictationEntry entry, CancellationToken ct = default);
    Task<DictationEntry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<HistoryPage> GetPagedAsync(HistoryFilter filter, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<bool> SetFavoriteAsync(string id, bool isFavorite, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(string id, CancellationToken ct = default);
    Task<bool> RestoreAsync(string id, CancellationToken ct = default);
    Task<bool> DeletePermanentlyAsync(string id, CancellationToken ct = default);
    Task<int> DeleteAllAsync(bool confirm, CancellationToken ct = default);
    Task<int> ApplyRetentionAsync(TimeSpan? maxAge, int? maxCount, CancellationToken ct = default);
    Task<int> GetCountAsync(HistoryFilter? filter = null, CancellationToken ct = default);
    Task<HistorySettings> GetSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(HistorySettings settings, CancellationToken ct = default);
    Task<IReadOnlyList<DictationEntry>> GetAllForExportAsync(HistoryFilter? filter = null, CancellationToken ct = default);
}

public interface IHistorySearchService
{
    Task<HistoryPage> SearchAsync(string query, HistoryFilter? filter = null, int pageIndex = 0, int pageSize = 50, CancellationToken ct = default);
}

public interface IProductivityStatisticsService
{
    Task<ProductivityMetrics> GetStatisticsAsync(TimeRangeWindow window, TimeZoneInfo? timeZone = null, CancellationToken ct = default);
    Task<DailyStreakInfo> GetDailyStreakAsync(TimeZoneInfo? timeZone = null, int minWordsThreshold = 1, CancellationToken ct = default);
    Task<ProductivityInsights> GetProductivityInsightsAsync(TimeRangeWindow window, TimeZoneInfo? timeZone = null, CancellationToken ct = default);
}

public interface IHistoryRetentionService
{
    Task<int> EnforceRetentionAsync(CancellationToken ct = default);
}

public interface IHistoryExportService
{
    Task<string> ExportAsync(string destinationFilePath, HistoryExportFormat format, HistoryFilter? filter = null, bool overwrite = false, CancellationToken ct = default);
}

public interface IHistoryPrivacyService
{
    bool IsSafeToPersist(ContextSnapshot? context);
    bool ShouldSaveTranscriptText(HistorySettings settings, ContextSnapshot? context);
    string? ComputeTextHash(string? text);
}

public interface IHistoryService
{
    IHistoryRepository Repository { get; }
    IHistorySearchService Search { get; }
    IProductivityStatisticsService Statistics { get; }
    IHistoryRetentionService Retention { get; }
    IHistoryExportService Export { get; }
    IHistoryPrivacyService Privacy { get; }

    Task<DictationEntry?> RecordDictationAsync(
        Guid sessionId,
        string text,
        TimeSpan duration,
        string language,
        ContextSnapshot? context,
        string mode = "Dictation",
        HistoryState state = HistoryState.Completed,
        string? metadataJson = null,
        CancellationToken ct = default
    );

    Task<HistorySettings> GetSettingsAsync(CancellationToken ct = default);
    Task UpdateSettingsAsync(HistorySettings settings, CancellationToken ct = default);
}
