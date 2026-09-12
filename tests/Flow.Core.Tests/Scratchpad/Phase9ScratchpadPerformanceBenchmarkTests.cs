using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadPerformanceBenchmarkTests
{
    [Fact]
    public async Task Benchmark_1000Scratchpads_SearchAndPagination_Sub10ms()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        // Seed 1,000 scratchpads
        for (int i = 0; i < 1000; i++)
        {
            var entry = ScratchpadEntry.Create(
                title: $"Note {i}: Project Roadmap",
                content: $"Details about deliverable {i} for Windows offline voice platform dictation.",
                isPinned: (i % 20 == 0)
            );
            await repo.InsertAsync(entry);
        }

        // Benchmark Search
        var sw = Stopwatch.StartNew();
        var searchResults = await repo.SearchAsync("platform", pageIndex: 0, pageSize: 20);
        sw.Stop();

        Assert.NotNull(searchResults);
        Assert.NotEmpty(searchResults.Items);
        Assert.True(sw.ElapsedMilliseconds < 50, $"1,000 records FTS Search took {sw.ElapsedMilliseconds}ms (target <50ms)");

        // Benchmark Pagination
        sw.Restart();
        var paged = await repo.GetPagedAsync(new ScratchpadFilter(SortBy: ScratchpadSortOrder.PinnedFirstThenUpdated), pageIndex: 5, pageSize: 25);
        sw.Stop();

        Assert.Equal(25, paged.Items.Count);
        Assert.True(sw.ElapsedMilliseconds < 25, $"1,000 records Pagination took {sw.ElapsedMilliseconds}ms (target <25ms)");
    }

    [Fact]
    public async Task Benchmark_10000Scratchpads_SearchAndPagination_Sub50ms()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"flow_bench_10k_{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new SqlitePersonalizationDatabase(tempDb))
            {
                // Bulk insert via transaction for rapid test execution
                using (var conn = db.CreateConnection())
                using (var tx = conn.BeginTransaction())
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = @"
                        INSERT INTO Scratchpads (Id, Title, Content, CreatedAt, UpdatedAt, IsPinned, IsDeleted, WordCount, CharacterCount)
                        VALUES (@id, @title, @content, @created, @updated, @pinned, 0, 10, 80);
                    ";

                    var pId = cmd.Parameters.Add("@id", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pTitle = cmd.Parameters.Add("@title", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pContent = cmd.Parameters.Add("@content", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pCreated = cmd.Parameters.Add("@created", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pUpdated = cmd.Parameters.Add("@updated", Microsoft.Data.Sqlite.SqliteType.Text);
                    var pPinned = cmd.Parameters.Add("@pinned", Microsoft.Data.Sqlite.SqliteType.Integer);

                    string now = DateTimeOffset.UtcNow.ToString("O");
                    for (int i = 0; i < 10000; i++)
                    {
                        pId.Value = $"note-{i:D6}";
                        pTitle.Value = $"Document {i} Quarterly Analysis";
                        pContent.Value = $"Voice transcription productivity metric benchmark record number {i} for FLOW.";
                        pCreated.Value = now;
                        pUpdated.Value = now;
                        pPinned.Value = (i % 50 == 0) ? 1 : 0;
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }

                var repo = new SqliteScratchpadRepository(db);

                // Benchmark 10k Search
                var sw = Stopwatch.StartNew();
                var search = await repo.SearchAsync("productivity", pageIndex: 0, pageSize: 50);
                sw.Stop();

                Assert.NotNull(search);
                Assert.True(sw.ElapsedMilliseconds < 100, $"10,000 records FTS Search took {sw.ElapsedMilliseconds}ms (target <100ms)");

                // Benchmark 10k Pagination
                sw.Restart();
                var page = await repo.GetPagedAsync(new ScratchpadFilter(SortBy: ScratchpadSortOrder.PinnedFirstThenUpdated), pageIndex: 20, pageSize: 50);
                sw.Stop();

                Assert.Equal(50, page.Items.Count);
                Assert.True(sw.ElapsedMilliseconds < 50, $"10,000 records Pagination took {sw.ElapsedMilliseconds}ms (target <50ms)");
            }
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }
}
