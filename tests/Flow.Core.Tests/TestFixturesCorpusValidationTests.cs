using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Flow.Core.Language;
using Xunit;

namespace Flow.Core.Tests;

/// <summary>
/// Automated integration test suite validating tools/test_fixtures/
/// (entities.json, punctuation.json, speech_corpus.json) against FLOW's
/// DeterministicTextSanitizer, Content Lock, and Zero-Enter invariants.
/// </summary>
public sealed class TestFixturesCorpusValidationTests
{
    private readonly DeterministicTextSanitizer _sanitizer = new();

    private static string GetFixturesDirectory()
    {
        string current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 7; i++)
        {
            string candidate = Path.Combine(current, "tools", "test_fixtures");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            string? parent = Directory.GetParent(current)?.FullName;
            if (parent == null) break;
            current = parent;
        }
        throw new DirectoryNotFoundException("Could not locate tools/test_fixtures directory from " + AppDomain.CurrentDomain.BaseDirectory);
    }

    [Fact]
    public void EntitiesJson_MustExistAndBeValidJson()
    {
        string path = Path.Combine(GetFixturesDirectory(), "entities.json");
        Assert.True(File.Exists(path), $"entities.json must exist at {path}");

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 2);
    }

    [Fact]
    public void EntitiesJson_NegativeConstraintsAndTechnicalEntities_PreservedBySanitizer()
    {
        string path = Path.Combine(GetFixturesDirectory(), "entities.json");
        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        // tc-001: Negative constraint preservation
        var tc001 = doc.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetString() == "tc-001");
        string input1 = tc001.GetProperty("input").GetString()!;
        string formatted1 = _sanitizer.Format(input1);

        // Technical entities must be preserved
        Assert.Contains("FastAPI", formatted1);
        Assert.Contains("PostgreSQL", formatted1);
        Assert.Contains("Redis", formatted1);
        Assert.Contains("Firebase", formatted1);

        // Inviolable negative constraint must be preserved
        Assert.Contains("Do not use Firebase", formatted1);

        // Inviolable Zero-Enter rule
        Assert.DoesNotContain("\r", formatted1);
        Assert.DoesNotContain("\n", formatted1);

        // tc-002: No-Invention rule compliance
        var tc002 = doc.RootElement.EnumerateArray().First(e => e.GetProperty("id").GetString() == "tc-002");
        string input2 = tc002.GetProperty("input").GetString()!;
        string formatted2 = _sanitizer.Format(input2);

        var forbidden = tc002.GetProperty("inventedStacksForbidden").EnumerateArray()
            .Select(x => x.GetString()!)
            .ToList();

        // The sanitizer must NEVER invent unmentioned stacks
        foreach (string stack in forbidden)
        {
            Assert.DoesNotContain(stack, formatted2);
        }
    }

    [Fact]
    public void PunctuationJson_MustExistAndPassZeroEnterAndPunctuationValidation()
    {
        string path = Path.Combine(GetFixturesDirectory(), "punctuation.json");
        Assert.True(File.Exists(path), $"punctuation.json must exist at {path}");

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            string input = item.GetProperty("input").GetString()!;
            string formatted = _sanitizer.Format(input);

            // Inviolable Zero-Enter Guarantee: Newlines and Carriage Returns must NEVER be emitted
            Assert.DoesNotContain("\r", formatted);
            Assert.DoesNotContain("\n", formatted);

            // Must produce non-empty formatted output
            Assert.False(string.IsNullOrWhiteSpace(formatted));

            // First character should be capitalized
            Assert.True(char.IsUpper(formatted[0]));
        }

        // Specific test cases from punctuation.json
        string punctuationTest1 = _sanitizer.Format("hello world period this is a test question mark");
        Assert.Equal("Hello world. This is a test?", punctuationTest1);

        string punctuationTest2 = _sanitizer.Format("please note colon item one comma item two comma and item three period");
        Assert.Equal("Please note: item one, item two, and item three.", punctuationTest2);

        string punctuationTest3 = _sanitizer.Format("first paragraph new line second paragraph");
        // In FLOW, spoken newlines map to spaces under the Zero-Enter Invariant
        Assert.Equal("First paragraph second paragraph.", punctuationTest3);

        string punctuationTest4 = _sanitizer.Format("um we need to like deploy the the server uh right now");
        // Filler words um/uh removed
        Assert.DoesNotContain("um", punctuationTest4, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uh", punctuationTest4, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SpeechCorpusJson_MustExistAndValidateCorpusStructure()
    {
        string path = Path.Combine(GetFixturesDirectory(), "speech_corpus.json");
        Assert.True(File.Exists(path), $"speech_corpus.json must exist at {path}");

        string json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 5);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            string id = item.GetProperty("id").GetString()!;
            double duration = item.GetProperty("durationSeconds").GetDouble();
            string groundTruth = item.GetProperty("groundTruth").GetString()!;
            string category = item.GetProperty("category").GetString()!;

            Assert.StartsWith("corpus-", id);
            Assert.True(duration > 0, "Corpus duration must be positive");
            Assert.False(string.IsNullOrWhiteSpace(groundTruth));
            Assert.False(string.IsNullOrWhiteSpace(category));

            // Run through sanitizer: ground truth sentences must stay intact
            string sanitized = _sanitizer.Format(groundTruth);
            Assert.DoesNotContain("\r", sanitized);
            Assert.DoesNotContain("\n", sanitized);
            Assert.Equal(groundTruth, sanitized);
        }
    }
}
