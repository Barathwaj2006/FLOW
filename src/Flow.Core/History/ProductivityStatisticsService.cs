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

    public Task<ProductivityMetrics> GetStatisticsAsync(TimeRangeWindow window, TimeZoneInfo? timeZone = null, CancellationToken ct = default)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset? fromDate = GetWindowBoundary(window, now, tz);
        return _repository.GetMetricsAsync(window, fromDate, tz, ct);
    }

    public async Task<DailyStreakInfo> GetDailyStreakAsync(TimeZoneInfo? timeZone = null, int minWordsThreshold = 1, CancellationToken ct = default)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        var activeDays = await _repository.GetActiveDaysAsync(minWordsThreshold, tz, ct);

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

        long longestDurationMs = await _repository.GetMaxDurationMsAsync(GetWindowBoundary(window, DateTimeOffset.UtcNow, timeZone ?? TimeZoneInfo.Local), ct);

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
