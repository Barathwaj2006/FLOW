using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadLifecycleAndLongRunTests
{
    [Fact]
    public async Task LongRun_2000LifecycleOperations_MaintainsDataIntegrityZeroOrphans()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"flow_longrun_{Guid.NewGuid():N}.db");
        try
        {
            var trackedIds = new HashSet<string>();
            var activeIds = new List<string>();
            var deletedIds = new List<string>();
            var rand = new Random(777);

            // Run 2,000 operations across repeated opens/closes
            for (int cycle = 0; cycle < 10; cycle++)
            {
                using var db = new SqlitePersonalizationDatabase(tempDb);
                var repo = new SqliteScratchpadRepository(db);
                var service = new ScratchpadService(repo);

                for (int op = 0; op < 200; op++)
                {
                    int action = rand.Next(7);
                    switch (action)
                    {
                        case 0: // Create
                            var created = await service.CreateScratchpadAsync(
                                $"Cycle {cycle} Note {op}",
                                $"Body content for note in cycle {cycle} step {op}"
                            );
                            Assert.DoesNotContain(created.Id, trackedIds); // Guarantee no duplicate IDs
                            trackedIds.Add(created.Id);
                            activeIds.Add(created.Id);
                            break;

                        case 1: // Edit / Save
                            if (activeIds.Count > 0)
                            {
                                string editId = activeIds[rand.Next(activeIds.Count)];
                                var updated = await service.UpdateScratchpadAsync(
                                    editId,
                                    $"Edited Cycle {cycle} Step {op}",
                                    $"Revised content at {DateTime.UtcNow.Ticks}"
                                );
                                Assert.NotNull(updated);
                            }
                            break;

                        case 2: // Search
                            var searchResults = await service.SearchScratchpadsAsync("content", pageIndex: 0, pageSize: 10);
                            Assert.NotNull(searchResults);
                            break;

                        case 3: // Pin
                            if (activeIds.Count > 0)
                            {
                                string pinId = activeIds[rand.Next(activeIds.Count)];
                                bool pinned = await service.PinScratchpadAsync(pinId);
                                Assert.True(pinned);
                            }
                            break;

                        case 4: // Unpin
                            if (activeIds.Count > 0)
                            {
                                string unpinId = activeIds[rand.Next(activeIds.Count)];
                                bool unpinned = await service.UnpinScratchpadAsync(unpinId);
                                Assert.True(unpinned);
                            }
                            break;

                        case 5: // Delete (soft)
                            if (activeIds.Count > 0)
                            {
                                int idx = rand.Next(activeIds.Count);
                                string delId = activeIds[idx];
                                activeIds.RemoveAt(idx);
                                bool deleted = await service.DeleteScratchpadAsync(delId);
                                Assert.True(deleted);
                                deletedIds.Add(delId);
                            }
                            break;

                        case 6: // Restore
                            if (deletedIds.Count > 0)
                            {
                                int idx = rand.Next(deletedIds.Count);
                                string restoreId = deletedIds[idx];
                                deletedIds.RemoveAt(idx);
                                bool restored = await service.RestoreScratchpadAsync(restoreId);
                                Assert.True(restored);
                                activeIds.Add(restoreId);
                            }
                            break;
                    }
                }
            }

            // Final verification on reopened DB
            using (var finalDb = new SqlitePersonalizationDatabase(tempDb))
            {
                using var conn = finalDb.CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var integrity = cmd.ExecuteScalar()?.ToString();
                Assert.Equal("ok", integrity);

                // Verify row counts match tracking
                using var countCmd = conn.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM Scratchpads;";
                int totalRows = Convert.ToInt32(countCmd.ExecuteScalar());
                Assert.Equal(trackedIds.Count, totalRows);
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
