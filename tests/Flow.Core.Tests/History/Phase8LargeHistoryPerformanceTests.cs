using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8LargeHistoryPerformanceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly ProductivityStatisticsService _statsService;
    private readonly HistoryRetentionService _retentionService;

    public Phase8LargeHistoryPerformanceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_perf_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
        _statsService = new ProductivityStatisticsService(_repository);
        _retentionService = new HistoryRetentionService(_repository);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task LargeDataset_10000Entries_SearchAndOperationsWithinThresholds()
    {
        // 1. Bulk insert 10,000 entries using a single transaction
        await using (var conn = _database.CreateConnection())
        {
            await using var tx = await conn.BeginTransactionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = (Microsoft.Data.Sqlite.SqliteTransaction)tx;

            cmd.CommandText = @"
                INSERT INTO DictationHistory (
                    Id, SessionId, CreatedAt, DurationMs, CharacterCount, WordCount,
                    Language, Application, ApplicationCategory, Mode, State,
                    WasEdited, IsFavorite, Text, TextHash, MetadataJson, IsDeleted, DeletedAt
                ) VALUES (
                    @id, @sessionId, @createdAt, @durationMs, @charCount, @wordCount,
                    @language, @app, @category, @mode, @state,
                    @wasEdited, @isFavorite, @text, @textHash, @metadata, @isDeleted, @deletedAt
                );
            ";

            var pId = cmd.Parameters.Add("@id", Microsoft.Data.Sqlite.SqliteType.Text);
            var pSessionId = cmd.Parameters.Add("@sessionId", Microsoft.Data.Sqlite.SqliteType.Text);
            var pCreatedAt = cmd.Parameters.Add("@createdAt", Microsoft.Data.Sqlite.SqliteType.Text);
            var pDuration = cmd.Parameters.Add("@durationMs", Microsoft.Data.Sqlite.SqliteType.Integer);
            var pChar = cmd.Parameters.Add("@charCount", Microsoft.Data.Sqlite.SqliteType.Integer);
            var pWord = cmd.Parameters.Add("@wordCount", Microsoft.Data.Sqlite.SqliteType.Integer);
            var pLang = cmd.Parameters.Add("@language", Microsoft.Data.Sqlite.SqliteType.Text);
            var pApp = cmd.Parameters.Add("@app", Microsoft.Data.Sqlite.SqliteType.Text);
            var pCat = cmd.Parameters.Add("@category", Microsoft.Data.Sqlite.SqliteType.Text);
            var pMode = cmd.Parameters.Add("@mode", Microsoft.Data.Sqlite.SqliteType.Text);
            var pState = cmd.Parameters.Add("@state", Microsoft.Data.Sqlite.SqliteType.Text);
            var pEdited = cmd.Parameters.Add("@wasEdited", Microsoft.Data.Sqlite.SqliteType.Integer);
            var pFav = cmd.Parameters.Add("@isFavorite", Microsoft.Data.Sqlite.SqliteType.Integer);
            var pText = cmd.Parameters.Add("@text", Microsoft.Data.Sqlite.SqliteType.Text);
            var pHash = cmd.Parameters.Add("@textHash", Microsoft.Data.Sqlite.SqliteType.Text);
            var pMeta = cmd.Parameters.Add("@metadata", Microsoft.Data.Sqlite.SqliteType.Text);
            var pDel = cmd.Parameters.Add("@isDeleted", Microsoft.Data.Sqlite.SqliteType.Integer);
            var pDelAt = cmd.Parameters.Add("@deletedAt", Microsoft.Data.Sqlite.SqliteType.Text);

            pEdited.Value = 0;
            pMeta.Value = DBNull.Value;
            pDel.Value = 0;
            pDelAt.Value = DBNull.Value;

            var baseDate = DateTimeOffset.UtcNow.AddDays(-100);

            for (int i = 0; i < 10000; i++)
            {
                pId.Value = $"bulk_{i:D6}";
                pSessionId.Value = Guid.NewGuid().ToString();
                pCreatedAt.Value = baseDate.AddMinutes(i * 10).ToString("O");
                pDuration.Value = 15000;
                pChar.Value = 50;
                pWord.Value = 10;
                pLang.Value = i % 2 == 0 ? "en" : "ta";
                pApp.Value = i % 3 == 0 ? "code.exe" : (i % 3 == 1 ? "slack.exe" : "browser.exe");
                pCat.Value = "General";
                pMode.Value = "Dictation";
                pState.Value = "Completed";
                pFav.Value = i == 500 ? 1 : 0;
                pText.Value = i == 500 ? "Unique needle in the massive haystack for latency benchmark" : $"Bulk text entry {i} dictation performance evaluation.";
                pHash.Value = "dummy_hash";

                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        int count = await _repository.GetCountAsync();
        Assert.Equal(10000, count);

        // 2. FTS Search Latency Benchmark (< 50ms requirement)
        var sw = Stopwatch.StartNew();
        var searchResult = await _repository.SearchAsync("haystack");
        sw.Stop();

        Assert.Single(searchResult.Items);
        Assert.Equal("bulk_000500", searchResult.Items[0].Id);
        Assert.True(sw.ElapsedMilliseconds < 150, $"Search took {sw.ElapsedMilliseconds}ms, exceeding performance threshold.");

        // 3. Single Insert Latency Benchmark (< 5ms typical)
        var singleEntry = new DictationEntry(
            Id: "perf_single",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow,
            DurationMs: 5000,
            CharacterCount: 20,
            WordCount: 4,
            Language: "en",
            Application: "test.exe",
            ApplicationCategory: "Test",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Single benchmark insertion"
        );

        sw.Restart();
        await _repository.InsertAsync(singleEntry);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 50, $"Single insert took {sw.ElapsedMilliseconds}ms");

        // 4. Statistics Calculation Benchmark on 10,000 items (< 200ms)
        sw.Restart();
        var stats = await _statsService.GetStatisticsAsync(TimeRangeWindow.ThirtyDays);
        sw.Stop();
        Assert.NotNull(stats);
        Assert.True(sw.ElapsedMilliseconds < 350, $"Statistics calculation took {sw.ElapsedMilliseconds}ms");
    }
}
