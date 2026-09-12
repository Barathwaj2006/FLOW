using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.Personalization.Dictionary;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Personalization;

public sealed class PersonalDictionaryTests : IDisposable
{
    private readonly SqlitePersonalizationDatabase _db;
    private readonly SqlitePersonalDictionaryRepository _repo;

    public PersonalDictionaryTests()
    {
        _db = new SqlitePersonalizationDatabase(":memory:");
        _repo = new SqlitePersonalDictionaryRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task AddAndRetrieve_DictionaryEntry_Succeeds()
    {
        var entry = new DictionaryEntry
        {
            Term = "anti gravity",
            Replacement = "Antigravity",
            IsStarred = true,
            Category = "Project"
        };

        await _repo.AddAsync(entry);

        var retrieved = await _repo.GetByTermAsync("anti gravity");
        Assert.NotNull(retrieved);
        Assert.Equal("Antigravity", retrieved.Replacement);
        Assert.True(retrieved.IsStarred);
        Assert.Equal("Project", retrieved.Category);
    }

    [Fact]
    public async Task Update_DictionaryEntry_UpdatesFields()
    {
        var entry = new DictionaryEntry
        {
            Term = "neuro sim",
            Replacement = "Neurosim"
        };
        await _repo.AddAsync(entry);

        entry.Replacement = "NeuroSim";
        entry.IsStarred = true;
        await _repo.UpdateAsync(entry);

        var updated = await _repo.GetByTermAsync("neuro sim");
        Assert.NotNull(updated);
        Assert.Equal("NeuroSim", updated.Replacement);
        Assert.True(updated.IsStarred);
    }

    [Fact]
    public async Task Delete_DictionaryEntry_RemovesEntry()
    {
        var entry = new DictionaryEntry { Term = "temporary term" };
        await _repo.AddAsync(entry);

        var retrieved = await _repo.GetByTermAsync("temporary term");
        Assert.NotNull(retrieved);

        await _repo.DeleteAsync(retrieved.Id);
        var afterDelete = await _repo.GetByTermAsync("temporary term");
        Assert.Null(afterDelete);
    }

    [Fact]
    public void PersonalDictionaryEngine_AppliesExactCorrection()
    {
        var engine = new PersonalDictionaryEngine();
        engine.SetEntries(new[]
        {
            new DictionaryEntry { Term = "anti gravity", Replacement = "Antigravity" },
            new DictionaryEntry { Term = "react native", Replacement = "React Native" }
        });

        string input = "we are testing anti gravity with react native today";
        string output = engine.Apply(input);

        Assert.Equal("we are testing Antigravity with React Native today", output);
    }

    [Fact]
    public void PersonalDictionaryEngine_PreservesCustomVocabularyCasing()
    {
        var engine = new PersonalDictionaryEngine();
        engine.SetEntries(new[]
        {
            new DictionaryEntry { Term = "PostgreSQL", Replacement = null },
            new DictionaryEntry { Term = "DirectML", Replacement = "" }
        });

        string input = "connecting postgresql to directml pipeline";
        string output = engine.Apply(input);

        Assert.Equal("connecting PostgreSQL to DirectML pipeline", output);
    }

    [Fact]
    public void PersonalDictionaryEngine_RespectsWordBoundaries()
    {
        var engine = new PersonalDictionaryEngine();
        engine.SetEntries(new[]
        {
            new DictionaryEntry { Term = "flow", Replacement = "FLOW" }
        });

        string input = "the flower had a steady flow of water";
        string output = engine.Apply(input);

        // "flower" should NOT be replaced, "flow" should be replaced
        Assert.Equal("the flower had a steady FLOW of water", output);
    }

    [Fact]
    public void PersonalDictionaryEngine_PrioritizesStarredAndLongestPhrases()
    {
        var engine = new PersonalDictionaryEngine();
        engine.SetEntries(new[]
        {
            new DictionaryEntry { Term = "react", Replacement = "React", IsStarred = false },
            new DictionaryEntry { Term = "react native", Replacement = "React Native", IsStarred = true }
        });

        string input = "we build with react native and react";
        string output = engine.Apply(input);

        Assert.Equal("we build with React Native and React", output);
    }

    [Fact]
    public async Task ExportAndImportJson_RoundtripsEntries()
    {
        await _repo.AddAsync(new DictionaryEntry { Term = "term1", Replacement = "Repl1", IsStarred = true });
        await _repo.AddAsync(new DictionaryEntry { Term = "term2", Replacement = "Repl2", IsStarred = false });

        string json = await _repo.ExportToJsonAsync();
        Assert.Contains("term1", json);
        Assert.Contains("term2", json);

        using var db2 = new SqlitePersonalizationDatabase(":memory:");
        var repo2 = new SqlitePersonalDictionaryRepository(db2);

        await repo2.ImportFromJsonAsync(json);
        var entries = await repo2.GetAllAsync();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Term == "term1" && e.Replacement == "Repl1" && e.IsStarred);
        Assert.Contains(entries, e => e.Term == "term2" && e.Replacement == "Repl2" && !e.IsStarred);
    }

    [Fact]
    public async Task ExportAndImportCsv_RoundtripsEntries()
    {
        await _repo.AddAsync(new DictionaryEntry { Term = "termA", Replacement = "ReplA", IsStarred = true, Category = "CatA" });
        await _repo.AddAsync(new DictionaryEntry { Term = "termB", Replacement = "ReplB", IsStarred = false, Category = "CatB" });

        string csv = await _repo.ExportToCsvAsync();
        Assert.Contains("termA", csv);
        Assert.Contains("termB", csv);

        using var db2 = new SqlitePersonalizationDatabase(":memory:");
        var repo2 = new SqlitePersonalDictionaryRepository(db2);

        await repo2.ImportFromCsvAsync(csv);
        var entries = await repo2.GetAllAsync();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Term == "termA" && e.Replacement == "ReplA" && e.IsStarred && e.Category == "CatA");
    }
}
