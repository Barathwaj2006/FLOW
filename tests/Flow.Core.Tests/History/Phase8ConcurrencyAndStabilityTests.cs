using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8ConcurrencyAndStabilityTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;

    public Phase8ConcurrencyAndStabilityTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_conc_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task Concurrent_16WriterTasks_SucceedWithoutDeadlock()
    {
        int threadCount = 16;
        int itemsPerThread = 25; // 400 total insertions
        var tasks = new List<Task>();

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    var entry = new DictationEntry(
                        Id: $"conc_{threadId}_{i}",
                        SessionId: Guid.NewGuid(),
                        CreatedAt: DateTimeOffset.UtcNow,
                        DurationMs: 5000,
                        CharacterCount: 25,
                        WordCount: 5,
                        Language: "en",
                        Application: $"worker_{threadId}.exe",
                        ApplicationCategory: "Worker",
                        Mode: "Dictation",
                        State: HistoryState.Completed,
                        Text: $"Concurrent text from thread {threadId} index {i}"
                    );

                    await _repository.InsertAsync(entry);
                }
            }));
        }

        await Task.WhenAll(tasks);

        int totalCount = await _repository.GetCountAsync();
        Assert.Equal(threadCount * itemsPerThread, totalCount);

        string? Integrity = await _database.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
        Assert.Equal("ok", Integrity);
    }

    [Fact]
    public async Task Concurrent_16ReadersAnd16Writers_RunSimultaneously()
    {
        var tasks = new List<Task>();

        // 16 writers
        for (int t = 0; t < 16; t++)
        {
            int id = t;
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    await _repository.InsertAsync(new DictationEntry(
                        Id: $"rw_{id}_{i}",
                        SessionId: Guid.NewGuid(),
                        CreatedAt: DateTimeOffset.UtcNow,
                        DurationMs: 4000,
                        CharacterCount: 20,
                        WordCount: 4,
                        Language: "en",
                        Application: "writer.exe",
                        ApplicationCategory: "Write",
                        Mode: "Dictation",
                        State: HistoryState.Completed,
                        Text: $"Writer entry {id} - {i}"
                    ));
                }
            }));
        }

        // 16 readers querying FTS and count simultaneously
        for (int r = 0; r < 16; r++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < 20; i++)
                {
                    _ = await _repository.SearchAsync("Writer");
                    _ = await _repository.GetCountAsync();
                    await Task.Delay(2);
                }
            }));
        }

        await Task.WhenAll(tasks);

        int finalCount = await _repository.GetCountAsync();
        Assert.Equal(16 * 20, finalCount);
    }
}
