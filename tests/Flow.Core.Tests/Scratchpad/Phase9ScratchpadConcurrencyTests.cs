using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadConcurrencyTests
{
    [Fact]
    public async Task Concurrency_16ConcurrentWorkers_PerformsReadWriteSearchWithoutDeadlockOrCorruption()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"flow_concurrency_{Guid.NewGuid():N}.db");
        try
        {
            using var db = new SqlitePersonalizationDatabase(tempDb);
            var repo = new SqliteScratchpadRepository(db);
            var service = new ScratchpadService(repo);

            // Pre-seed 50 items
            var seededIds = new List<string>();
            for (int i = 0; i < 50; i++)
            {
                var item = await service.CreateScratchpadAsync($"Seed {i}", $"Initial content for worker test {i}");
                seededIds.Add(item.Id);
            }

            const int workerCount = 16;
            const int operationsPerWorker = 50;
            var tasks = new Task[workerCount];
            var errors = new List<Exception>();
            var errorLock = new object();

            for (int w = 0; w < workerCount; w++)
            {
                int workerId = w;
                tasks[w] = Task.Run(async () =>
                {
                    var rand = new Random(100 + workerId);
                    for (int op = 0; op < operationsPerWorker; op++)
                    {
                        try
                        {
                            int action = rand.Next(6);
                            switch (action)
                            {
                                case 0: // Concurrent read
                                    string targetId = seededIds[rand.Next(seededIds.Count)];
                                    var item = await service.GetScratchpadAsync(targetId);
                                    Assert.NotNull(item);
                                    break;

                                case 1: // Concurrent write / update
                                    string editId = seededIds[rand.Next(seededIds.Count)];
                                    await service.UpdateScratchpadAsync(editId, $"Worker {workerId} Edit", $"Updated content at {DateTime.UtcNow.Ticks}");
                                    break;

                                case 2: // Concurrent search
                                    string query = rand.Next(2) == 0 ? "worker" : "content";
                                    var searchResult = await service.SearchScratchpadsAsync(query, pageIndex: 0, pageSize: 10);
                                    Assert.NotNull(searchResult);
                                    break;

                                case 3: // Concurrent create
                                    var newEntry = await service.CreateScratchpadAsync($"New from worker {workerId}", "Dynamic note");
                                    Assert.NotNull(newEntry);
                                    break;

                                case 4: // Concurrent pin/unpin
                                    string pinId = seededIds[rand.Next(seededIds.Count)];
                                    if (rand.Next(2) == 0) await service.PinScratchpadAsync(pinId);
                                    else await service.UnpinScratchpadAsync(pinId);
                                    break;

                                case 5: // Concurrent soft-delete & restore
                                    string delId = seededIds[rand.Next(seededIds.Count)];
                                    await service.DeleteScratchpadAsync(delId);
                                    await Task.Yield();
                                    await service.RestoreScratchpadAsync(delId);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (errorLock)
                            {
                                errors.Add(ex);
                            }
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);

            Assert.Empty(errors);

            // Verify database integrity
            using var conn = db.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var check = cmd.ExecuteScalar()?.ToString();
            Assert.Equal("ok", check);
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
