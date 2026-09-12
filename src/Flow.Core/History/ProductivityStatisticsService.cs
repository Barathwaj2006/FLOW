using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.History;

/// <summary>
/// Calculates accurate productivity metrics, WPM, streaks, and deterministic insights (WF-042, WF-044).
/// STRICT RULE: All metrics derive directly from real history records. Zero fabricated numbers.
/// </summary>
public sealed class ProductivityStatisticsService : IProductivityStatisticsService
{
    private readonly IHistoryRepository _repository;

    public ProductivityStatisticsService(IHistoryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<ProductivityMetrics> GetStatisticsAsync(TimeRangeWindow window, TimeZoneInfo? timeZone = null, CancellationToken ct = default)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? fromDate = GetWindowBoundary(window, now, tz);

        var filter = new HistoryFilter(
            IncludeDeleted: false,
            FromDate: fromDate,
            State: HistoryState.Completed
        );

        var allEntries = await _repository.GetAllForExportAsync(filter, ct);

        int totalWords = 0;
        int totalChars = 0;
        long totalDurationMs = 0;
        var appCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var langCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dailyDict = new Dictionary<DateOnly, (int words, int chars, int sessions, long duration)>();

        foreach (var entry in allEntries)
        {
            totalWords += entry.WordCount;
            totalChars += entry.CharacterCount;
            totalDurationMs += entry.DurationMs;

            if (!string.IsNullOrWhiteSpace(entry.Application))
            {
                appCounts[entry.Application] = appCounts.GetValueOrDefault(entry.Application) + 1;
            }

            if (!string.IsNullOrWhiteSpace(entry.Language))
            {
                langCounts[entry.Language] = langCounts.GetValueOrDefault(entry.Language) + 1;
            }

            DateOnly localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.CreatedAt, tz).DateTime);
            if (!dailyDict.TryGetValue(localDate, out var cur))
            {
                dailyDict[localDate] = (entry.WordCount, entry.CharacterCount, 1, entry.DurationMs);
            }
            else
            {
                dailyDict[localDate] = (cur.words + entry.WordCount, cur.chars + entry.CharacterCount, cur.sessions + 1, cur.duration + entry.DurationMs);
            }
        }

        int totalSessions = allEntries.Count;
        double avgWordsPerSession = totalSessions > 0 ? Math.Round((double)totalWords / totalSessions, 1) : 0.0;
        double avgDurationSec = totalSessions > 0 ? Math.Round((double)totalDurationMs / (totalSessions * 1000.0), 1) : 0.0;

        // WPM formula: TotalWords / ActiveMinutes (strictly bounded, safe against division by zero)
        double activeMinutes = totalDurationMs / 60000.0;
        double avgWpm = 0.0;
        if (totalDurationMs >= 10000 && totalWords > 0) // Minimum 10 seconds active
        {
            avgWpm = Math.Round(totalWords / activeMinutes, 1);
        }

        var topApps = appCounts.OrderByDescending(kv => kv.Value).Take(10).ToDictionary(kv => kv.Key, kv => kv.Value);
        var topLangs = langCounts.OrderByDescending(kv => kv.Value).Take(5).ToDictionary(kv => kv.Key, kv => kv.Value);

        var dailyList = dailyDict.OrderBy(kv => kv.Key)
            .Select(kv => new DailyUsageMetric(kv.Key, kv.Value.words, kv.Value.chars, kv.Value.sessions, kv.Value.duration))
            .ToList();

        return new ProductivityMetrics(
            Window: window,
            TotalWords: totalWords,
            TotalCharacters: totalChars,
            TotalSessions: totalSessions,
            TotalActiveDurationMs: totalDurationMs,
            AverageWpm: avgWpm,
            AverageWordsPerSession: avgWordsPerSession,
            AverageSessionDurationSeconds: avgDurationSec,
            TopApplications: topApps,
            TopLanguages: topLangs,
            DailyUsage: dailyList
        );
    }

    public async Task<DailyStreakInfo> GetDailyStreakAsync(TimeZoneInfo? timeZone = null, int minWordsThreshold = 1, CancellationToken ct = default)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        var filter = new HistoryFilter(IncludeDeleted: false, State: HistoryState.Completed);
        var entries = await _repository.GetAllForExportAsync(filter, ct);

        var wordsPerDay = new Dictionary<DateOnly, int>();
        foreach (var e in entries)
        {
            DateOnly localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(e.CreatedAt, tz).DateTime);
            wordsPerDay[localDate] = wordsPerDay.GetValueOrDefault(localDate) + e.WordCount;
        }

        var activeDays = wordsPerDay
            .Where(kv => kv.Value >= Math.Max(1, minWordsThreshold))
            .Select(kv => kv.Key)
            .OrderBy(d => d)
            .ToList();

        if (activeDays.Count == 0)
        {
            return new DailyStreakInfo(0, 0, null, Array.Empty<DateOnly>());
        }

        DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime);
        DateOnly yesterday = today.AddDays(-1);

        // Calculate current streak
        int currentStreak = 0;
        var activeSet = new HashSet<DateOnly>(activeDays);

        DateOnly streakCheckDay = activeSet.Contains(today) ? today : (activeSet.Contains(yesterday) ? yesterday : DateOnly.MinValue);

        if (streakCheckDay != DateOnly.MinValue)
        {
            DateOnly d = streakCheckDay;
            while (activeSet.Contains(d))
            {
                currentStreak++;
                d = d.AddDays(-1);
            }
        }

        // Calculate longest streak across history
        int longestStreak = 0;
        int runningStreak = 0;
        DateOnly? prevDay = null;

        foreach (var d in activeDays)
        {
            if (prevDay.HasValue && d == prevDay.Value.AddDays(1))
            {
                runningStreak++;
            }
            else
            {
                runningStreak = 1;
            }

            if (runningStreak > longestStreak)
            {
                longestStreak = runningStreak;
            }

            prevDay = d;
        }

        return new DailyStreakInfo(
            CurrentStreak: currentStreak,
            LongestStreak: Math.Max(longestStreak, currentStreak),
            LastActiveDate: activeDays[^1],
            ActiveDays: activeDays
        );
    }

    public async Task<ProductivityInsights> GetProductivityInsightsAsync(TimeRangeWindow window, TimeZoneInfo? timeZone = null, CancellationToken ct = default)
    {
        var stats = await GetStatisticsAsync(window, timeZone, ct);
        var streak = await GetDailyStreakAsync(timeZone, 1, ct);

        string? mostActiveDay = null;
        if (stats.DailyUsage.Count > 0)
        {
            mostActiveDay = stats.DailyUsage.OrderByDescending(d => d.WordCount).First().Date.DayOfWeek.ToString();
        }

        string? mostUsedApp = stats.TopApplications.Count > 0 ? stats.TopApplications.First().Key : null;
        string? mostUsedLang = stats.TopLanguages.Count > 0 ? stats.TopLanguages.First().Key : null;

        long longestDurationMs = 0;
        var allEntries = await _repository.GetAllForExportAsync(new HistoryFilter(FromDate: GetWindowBoundary(window, DateTimeOffset.UtcNow, timeZone ?? TimeZoneInfo.Local)), ct);
        if (allEntries.Count > 0)
        {
            longestDurationMs = allEntries.Max(e => e.DurationMs);
        }

        return new ProductivityInsights(
            MostActiveDay: mostActiveDay,
            MostUsedApplication: mostUsedApp,
            MostUsedLanguage: mostUsedLang,
            AverageWpm: stats.AverageWpm,
            TotalWords: stats.TotalWords,
            LongestSessionDuration: TimeSpan.FromMilliseconds(longestDurationMs),
            CurrentStreak: streak.CurrentStreak
        );
    }

    private static DateTimeOffset? GetWindowBoundary(TimeRangeWindow window, DateTimeOffset now, TimeZoneInfo tz)
    {
        DateTime localNow = TimeZoneInfo.ConvertTime(now, tz).DateTime;

        return window switch
        {
            TimeRangeWindow.Today => new DateTimeOffset(localNow.Date, tz.GetUtcOffset(localNow)),
            TimeRangeWindow.SevenDays => new DateTimeOffset(localNow.Date.AddDays(-7), tz.GetUtcOffset(localNow)),
            TimeRangeWindow.ThirtyDays => new DateTimeOffset(localNow.Date.AddDays(-30), tz.GetUtcOffset(localNow)),
            TimeRangeWindow.AllTime => null,
            _ => null
        };
    }
}
