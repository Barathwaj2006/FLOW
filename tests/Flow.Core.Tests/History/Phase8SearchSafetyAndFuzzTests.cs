using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

/// <summary>
/// Phase 8 Search Safety: 10,000 Deterministic Seeded Fuzzing Iterations.
/// Guarantees that arbitrary, malicious, malformed, or hostile query inputs:
/// 1. Never throw unhandled exceptions.
/// 2. Never corrupt FTS5 virtual tables or internal SQLite state.
/// 3. Never bypass SQL parameterization.
/// </summary>
public class Phase8SearchSafetyAndFuzzTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;

    public Phase8SearchSafetyAndFuzzTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_fuzz_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task Search_10000DeterministicFuzzInputs_NeverThrowsOrCorrupts()
    {
        // Seed database with sample records
        for (int i = 0; i < 20; i++)
        {
            await _repository.InsertAsync(new DictationEntry(
                Id: $"seed_{i}",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-i),
                DurationMs: 5000,
                CharacterCount: 30,
                WordCount: 5,
                Language: "en",
                Application: "test.exe",
                ApplicationCategory: "Test",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: $"Entry number {i} with some sample text for testing search resilience."
            ));
        }

        var rng = new Random(42_888); // Deterministic seed

        string[] attackVectors = new[]
        {
            "' OR '1'='1",
            "'; DROP TABLE DictationHistory; --",
            "'; DROP TABLE DictationHistoryFts; --",
            "UNION SELECT * FROM DictationHistory",
            "\"\"\"\"\"\"\"\"\"\"",
            "NEAR(foo, bar, 10)",
            "NOT OR AND * ^ ~",
            "\\x00\\x1F\\x7F",
            "<script>alert(1)</script>",
            "{{constructor.constructor('return this')()}}",
            "${7*7}",
            "SELECT COUNT(*) FROM sqlite_master",
            "PRAGMA integrity_check;",
            "INSERT INTO DictationHistory VALUES ('hacked')",
            "UPDATE DictationHistory SET Text = 'hacked'",
            "DELETE FROM DictationHistory",
            "/* block comment */",
            "-- line comment",
            "\0\0\0\0",
            "🚀🔥🎉✨💯",
            "வணக்கம்",
            "नमस्ते",
            "中文测试",
            "العربية"
        };

        for (int iteration = 0; iteration < 10000; iteration++)
        {
            string query;
            int type = rng.Next(5);

            if (type == 0)
            {
                // Attack vector with random punctuation
                query = attackVectors[rng.Next(attackVectors.Length)] + (char)rng.Next(32, 127);
            }
            else if (type == 1)
            {
                // Combinations of FTS special operators
                query = $"foo {attackVectors[rng.Next(attackVectors.Length)]} bar";
            }
            else if (type == 2)
            {
                // Random junk characters
                int len = rng.Next(1, 100);
                var chars = new char[len];
                for (int c = 0; c < len; c++)
                {
                    chars[c] = (char)rng.Next(0, 1000);
                }
                query = new string(chars);
            }
            else if (type == 3)
            {
                // Very long string (1,000 to 5,000 characters)
                query = new string('A', rng.Next(1000, 5000));
            }
            else
            {
                // Unclosed quotes and brackets
                query = new string('"', rng.Next(1, 15)) + "test" + new string('*', rng.Next(1, 5));
            }

            // Must execute cleanly without exception
            var result = await _repository.SearchAsync(query, pageIndex: 0, pageSize: 20);
            Assert.NotNull(result);
            Assert.NotNull(result.Items);
        }

        // Final verification: Table and FTS index are completely intact
        int count = await _repository.GetCountAsync();
        Assert.Equal(20, count);

        string? integrity = await _database.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
        Assert.Equal("ok", integrity);
    }
}
