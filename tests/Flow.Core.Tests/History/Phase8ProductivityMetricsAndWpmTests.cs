using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8ProductivityMetricsAndWpmTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly ProductivityStatisticsService _statsService;

    public Phase8ProductivityMetricsAndWpmTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_stats_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
        _statsService = new ProductivityStatisticsService(_repository);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task WpmCalculation_RealFormula_WordsDividedByActiveMinutes()
    {
        // 120 words in 60,000ms (1 minute) = 120.0 WPM
        var e1 = new DictationEntry(
            Id: "wpm_1",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 60000,
            CharacterCount: 600,
            WordCount: 120,
            Language: "en",
            Application: "code.exe",
            ApplicationCategory: "Development",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Text"
        );

        Assert.Equal(120.0, e1.Wpm);

        await _repository.InsertAsync(e1);
        var stats = await _statsService.GetStatisticsAsync(TimeRangeWindow.Today);
        Assert.Equal(120.0, stats.AverageWpm);
    }

    [Fact]
    public async Task WpmCalculation_ShortDurationUnder10s_IsBounded()
    {
        // Duration < 10 seconds does not produce outlier spikes in aggregate
        var e = new DictationEntry(
            Id: "wpm_short",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 3000, // 3 seconds
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "slack.exe",
            ApplicationCategory: "Chat",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Quick chat message"
        );

        await _repository.InsertAsync(e);
        var stats = await _statsService.GetStatisticsAsync(TimeRangeWindow.Today);
        Assert.Equal(0.0, stats.AverageWpm); // Bounded to 0 to prevent skewed division
    }

    [Fact]
    public async Task DailyStreak_ConsecutiveDays_CalculatesAccurately()
    {
        var tz = TimeZoneInfo.Utc;
        var today = DateTimeOffset.UtcNow;

        // Insert sessions on today, yesterday, and 2 days ago
        for (int day = 0; day < 3; day++)
        {
            await _repository.InsertAsync(new DictationEntry(
                Id: $"streak_{day}",
                SessionId: Guid.NewGuid(),
                CreatedAt: today.AddDays(-day),
                DurationMs: 30000,
                CharacterCount: 150,
                WordCount: 30,
                Language: "en",
                Application: "devenv.exe",
                ApplicationCategory: "Dev",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: $"Work done on day -{day}"
            ));
        }

        var streak = await _statsService.GetDailyStreakAsync(tz);
        Assert.Equal(3, streak.CurrentStreak);
        Assert.True(streak.LongestStreak >= 3);
        Assert.Equal(3, streak.ActiveDays.Count);
    }

    [Fact]
    public async Task DailyStreak_GapDay_ResetsCurrentStreak()
    {
        var tz = TimeZoneInfo.Utc;
        var today = DateTimeOffset.UtcNow;

        // Activity today and 3 days ago, but gap on yesterday
        await _repository.InsertAsync(new DictationEntry(
            Id: "s_today",
            SessionId: Guid.NewGuid(),
            CreatedAt: today,
            DurationMs: 30000,
            CharacterCount: 150,
            WordCount: 30,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Today"
        ));

        await _repository.InsertAsync(new DictationEntry(
            Id: "s_old",
            SessionId: Guid.NewGuid(),
            CreatedAt: today.AddDays(-3),
            DurationMs: 30000,
            CharacterCount: 150,
            WordCount: 30,
            Language: "en",
            Application: "app.exe",
            ApplicationCategory: "General",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Three days ago"
        ));

        var streak = await _statsService.GetDailyStreakAsync(tz);
        Assert.Equal(1, streak.CurrentStreak); // Gap resets streak to 1
    }

    [Fact]
    public async Task ProductivityInsights_CalculatesDeterministicValues()
    {
        await _repository.InsertAsync(new DictationEntry(
            Id: "ins_1",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 45000,
            CharacterCount: 300,
            WordCount: 60,
            Language: "en",
            Application: "Visual Studio",
            ApplicationCategory: "Dev",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Developing native voice application"
        ));

        var insights = await _statsService.GetProductivityInsightsAsync(TimeRangeWindow.AllTime);
        Assert.NotNull(insights);
        Assert.Equal("Visual Studio", insights.MostUsedApplication);
        Assert.Equal("en", insights.MostUsedLanguage);
        Assert.Equal(60, insights.TotalWords);
        Assert.True(insights.LongestSessionDuration.TotalSeconds >= 40);
    }
}
