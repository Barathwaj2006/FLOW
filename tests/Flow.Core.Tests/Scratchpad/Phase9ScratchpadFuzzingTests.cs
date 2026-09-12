using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadFuzzingTests
{
    private static readonly string[] FuzzPrimitives = new[]
    {
        "", " ", "\t", "\r\n", "\0", "\uFFFF",
        "'", "\"", "''", "\"\"", "'''", "\\", "\\\\", "/", "//",
        ";", "--", "/*", "*/", "@@", "%", "_", "*", "?",
        "<script>alert(1)</script>", "${7*7}", "{{foo}}",
        "DROP TABLE Scratchpads;", "OR 1=1--", "UNION SELECT NULL",
        "rm -rf /", "powershell -c echo bad", "cmd /c dir",
        "வணக்கம்", "नमस्ते", "こんにちは", "مرحبا", "🚀🔥🎉",
        new string('A', 500), new string('0', 1000), new string('\n', 200)
    };

    [Fact]
    public async Task Fuzzing_10000_SeededFuzzCases_ZeroCrashesZeroCorruption()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);
        var service = new ScratchpadService(repo);

        var random = new Random(42); // Deterministic seed for reproducible fuzzing

        // Populate base records
        var createdIds = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            var e = await service.CreateScratchpadAsync($"Base Note {i}", $"Initial content {i}");
            createdIds.Add(e.Id);
        }

        int iterations = 10000;
        int errorCount = 0;

        for (int i = 0; i < iterations; i++)
        {
            string fuzzTitle = GenerateFuzzString(random, 1, 4);
            string fuzzContent = GenerateFuzzString(random, 1, 8);
            string fuzzQuery = GenerateFuzzString(random, 1, 3);

            int op = random.Next(7);
            try
            {
                switch (op)
                {
                    case 0: // Search
                        var page = await service.SearchScratchpadsAsync(fuzzQuery, pageIndex: 0, pageSize: 20);
                        Assert.NotNull(page);
                        break;

                    case 1: // Create
                        var created = await service.CreateScratchpadAsync(fuzzTitle, fuzzContent, isPinned: (i % 5 == 0));
                        Assert.NotNull(created);
                        if (createdIds.Count < 50) createdIds.Add(created.Id);
                        break;

                    case 2: // Update
                        if (createdIds.Count > 0)
                        {
                            string targetId = createdIds[random.Next(createdIds.Count)];
                            await service.UpdateScratchpadAsync(targetId, fuzzTitle, fuzzContent);
                        }
                        break;

                    case 3: // Pin/Unpin
                        if (createdIds.Count > 0)
                        {
                            string targetId = createdIds[random.Next(createdIds.Count)];
                            if (random.Next(2) == 0) await service.PinScratchpadAsync(targetId);
                            else await service.UnpinScratchpadAsync(targetId);
                        }
                        break;

                    case 4: // SoftDelete/Restore
                        if (createdIds.Count > 0)
                        {
                            string targetId = createdIds[random.Next(createdIds.Count)];
                            await service.DeleteScratchpadAsync(targetId);
                            if (random.Next(2) == 0) await service.RestoreScratchpadAsync(targetId);
                        }
                        break;

                    case 5: // Word & Character count invariants
                        int wc = ScratchpadEntry.CalculateWordCount(fuzzContent);
                        Assert.True(wc >= 0);
                        string derived = ScratchpadEntry.DeriveTitleFromContent(fuzzContent);
                        Assert.NotNull(derived);
                        break;

                    case 6: // List with custom filter
                        var filter = new ScratchpadFilter(
                            SearchQuery: random.Next(2) == 0 ? fuzzQuery : null,
                            IsPinned: random.Next(3) switch { 0 => true, 1 => false, _ => null },
                            SortBy: (ScratchpadSortOrder)random.Next(4)
                        );
                        var filteredPage = await service.ListScratchpadsAsync(filter, pageIndex: 0, pageSize: 15);
                        Assert.NotNull(filteredPage);
                        break;
                }
            }
            catch (Exception)
            {
                errorCount++;
            }
        }

        Assert.Equal(0, errorCount);

        // Verify database integrity check passes after 10,000 fuzz operations
        using var conn = db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA integrity_check;";
        var integrityResult = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("ok", integrityResult);
    }

    private static string GenerateFuzzString(Random random, int minTokens, int maxTokens)
    {
        int count = random.Next(minTokens, maxTokens + 1);
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            string token = FuzzPrimitives[random.Next(FuzzPrimitives.Length)];
            sb.Append(token);
            if (i < count - 1 && random.Next(2) == 0)
            {
                sb.Append(' ');
            }
        }
        return sb.ToString();
    }
}
