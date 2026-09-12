using System;
using System.Collections.Generic;

namespace Flow.Core.History;

/// <summary>
/// Lifecycle state of a dictation session history record (WF-039).
/// </summary>
public enum HistoryState
{
    Completed,
    Cancelled,
    Failed,
    Excluded,
    Deleted
}

/// <summary>
/// Retention policy for automatic history cleanup (WF-040).
/// </summary>
public enum RetentionPolicy
{
    Unlimited,
    ThirtyDays,
    NinetyDays,
    OneHundredEightyDays,
    OneYear
}

/// <summary>
/// Export file format for local history export (WF-041).
/// </summary>
public enum HistoryExportFormat
{
    Json,
    Csv,
    PlainText
}

/// <summary>
/// Time window for productivity statistics aggregation (WF-042).
/// </summary>
public enum TimeRangeWindow
{
    Today,
    SevenDays,
    ThirtyDays,
    AllTime
}

/// <summary>
/// Immutable record representing a persistent dictation entry in local history (WF-039).
/// STRICT PRIVACY CONTRACT: Contains only what is required. Never contains passwords,
/// credential text, or sensitive control content.
/// </summary>
public sealed record DictationEntry(
    string Id,
    Guid SessionId,
    DateTimeOffset CreatedAt,
    long DurationMs,
    int CharacterCount,
    int WordCount,
    string Language,
    string Application,
    string ApplicationCategory,
    string Mode,
    HistoryState State,
    bool WasEdited = false,
    bool IsFavorite = false,
    string? Text = null,
    string? TextHash = null,
    string? MetadataJson = null,
    bool IsDeleted = false,
    DateTimeOffset? DeletedAt = null
)
{
    /// <summary>
    /// Calculated words per minute for this individual entry.
    /// </summary>
    public double Wpm
    {
        get
        {
            if (DurationMs < 500 || WordCount == 0) return 0.0;
            double minutes = DurationMs / 60000.0;
            return minutes > 0 ? Math.Round(WordCount / minutes, 1) : 0.0;
        }
    }
}

/// <summary>
/// Multi-attribute filter criteria for querying history records.
/// </summary>
public sealed record HistoryFilter(
    string? SearchQuery = null,
    string? Application = null,
    string? Language = null,
    string? Mode = null,
    HistoryState? State = null,
    bool? IsFavorite = null,
    bool IncludeDeleted = false,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null
);

/// <summary>
/// Paginated query result for history browsing.
/// </summary>
public sealed record HistoryPage(
    IReadOnlyList<DictationEntry> Items,
    int PageIndex,
    int PageSize,
    int TotalCount
)
{
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => PageIndex + 1 < TotalPages;
    public bool HasPreviousPage => PageIndex > 0;
}

/// <summary>
/// User-configurable history and privacy settings (WF-043).
/// </summary>
public sealed record HistorySettings(
    bool HistoryEnabled = true,
    bool SaveTranscriptText = true,
    bool StatisticsCollectionEnabled = true,
    RetentionPolicy Retention = RetentionPolicy.Unlimited,
    int MaxHistoryEntries = 10000,
    string ExportPermissions = "LocalOnly"
);

/// <summary>
/// Daily usage summary metric.
/// </summary>
public sealed record DailyUsageMetric(
    DateOnly Date,
    int WordCount,
    int CharacterCount,
    int SessionCount,
    long ActiveDurationMs
);

/// <summary>
/// Weekly usage summary metric.
/// </summary>
public sealed record WeeklyUsageMetric(
    int Year,
    int WeekNumber,
    int WordCount,
    int SessionCount,
    long ActiveDurationMs
);

/// <summary>
/// Comprehensive productivity statistics derived strictly from real history records (WF-042).
/// Zero fabricated metrics.
/// </summary>
public sealed record ProductivityMetrics(
    TimeRangeWindow Window,
    int TotalWords,
    int TotalCharacters,
    int TotalSessions,
    long TotalActiveDurationMs,
    double AverageWpm,
    double AverageWordsPerSession,
    double AverageSessionDurationSeconds,
    IReadOnlyDictionary<string, int> TopApplications,
    IReadOnlyDictionary<string, int> TopLanguages,
    IReadOnlyList<DailyUsageMetric> DailyUsage
);

/// <summary>
/// Daily dictation streak statistics (WF-042).
/// </summary>
public sealed record DailyStreakInfo(
    int CurrentStreak,
    int LongestStreak,
    DateOnly? LastActiveDate,
    IReadOnlyList<DateOnly> ActiveDays
);

/// <summary>
/// Deterministic productivity insights derived strictly from history (WF-044).
/// </summary>
public sealed record ProductivityInsights(
    string? MostActiveDay,
    string? MostUsedApplication,
    string? MostUsedLanguage,
    double AverageWpm,
    int TotalWords,
    TimeSpan LongestSessionDuration,
    int CurrentStreak
);
