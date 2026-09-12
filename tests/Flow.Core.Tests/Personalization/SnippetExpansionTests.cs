using System;
using System.Threading.Tasks;
using Flow.Core.Personalization.Snippets;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Personalization;

public sealed class SnippetExpansionTests : IDisposable
{
    private readonly SqlitePersonalizationDatabase _db;
    private readonly SqliteSnippetRepository _repo;

    public SnippetExpansionTests()
    {
        _db = new SqlitePersonalizationDatabase(":memory:");
        _repo = new SqliteSnippetRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task AddAndRetrieve_Snippet_Succeeds()
    {
        var snippet = new SnippetEntry
        {
            TriggerPhrase = "my email",
            ExpansionText = "barat@example.com",
            Category = "Contact"
        };
        await _repo.AddAsync(snippet);

        var retrieved = await _repo.GetByTriggerAsync("my email");
        Assert.NotNull(retrieved);
        Assert.Equal("barat@example.com", retrieved.ExpansionText);
        Assert.True(retrieved.IsEnabled);
    }

    [Fact]
    public async Task Update_Snippet_ModifiesContent()
    {
        var snippet = new SnippetEntry
        {
            TriggerPhrase = "standup update",
            ExpansionText = "Working on voice core."
        };
        await _repo.AddAsync(snippet);

        snippet.ExpansionText = "Working on Phase 2D personalization engine.";
        await _repo.UpdateAsync(snippet);

        var updated = await _repo.GetByTriggerAsync("standup update");
        Assert.NotNull(updated);
        Assert.Equal("Working on Phase 2D personalization engine.", updated.ExpansionText);
    }

    [Fact]
    public async Task Delete_Snippet_RemovesFromDatabase()
    {
        var snippet = new SnippetEntry
        {
            TriggerPhrase = "temp snippet",
            ExpansionText = "temp text"
        };
        await _repo.AddAsync(snippet);

        var retrieved = await _repo.GetByTriggerAsync("temp snippet");
        Assert.NotNull(retrieved);

        await _repo.DeleteAsync(retrieved.Id);
        var afterDelete = await _repo.GetByTriggerAsync("temp snippet");
        Assert.Null(afterDelete);
    }

    [Fact]
    public void SnippetExpansionEngine_ExpandsSpokenTrigger()
    {
        var engine = new SnippetExpansionEngine();
        engine.SetSnippets(new[]
        {
            new SnippetEntry { TriggerPhrase = "my email", ExpansionText = "user@example.com" }
        });

        string input = "please send the document to my email right away";
        string output = engine.Expand(input);

        Assert.Equal("please send the document to user@example.com right away", output);
    }

    [Fact]
    public void SnippetExpansionEngine_PrioritizesLongestTrigger()
    {
        var engine = new SnippetExpansionEngine();
        engine.SetSnippets(new[]
        {
            new SnippetEntry { TriggerPhrase = "my email", ExpansionText = "personal@me.com" },
            new SnippetEntry { TriggerPhrase = "my work email", ExpansionText = "work@company.com" }
        });

        string input = "send it to my work email thanks";
        string output = engine.Expand(input);

        Assert.Equal("send it to work@company.com thanks", output);
    }

    [Fact]
    public void SnippetExpansionEngine_IgnoresDisabledSnippets()
    {
        var engine = new SnippetExpansionEngine();
        engine.SetSnippets(new[]
        {
            new SnippetEntry { TriggerPhrase = "zoom link", ExpansionText = "https://zoom.us/j/12345", IsEnabled = false }
        });

        string input = "here is the zoom link for our meeting";
        string output = engine.Expand(input);

        Assert.Equal("here is the zoom link for our meeting", output);
    }

    [Fact]
    public void SnippetExpansionEngine_EnforcesZeroEnterInvariant_OnExpansionWithNewlines()
    {
        var engine = new SnippetExpansionEngine();
        engine.SetSnippets(new[]
        {
            new SnippetEntry
            {
                TriggerPhrase = "standard greeting",
                ExpansionText = "Hello team,\r\nHere is the daily update:\r\nAll systems nominal."
            }
        });

        string input = "insert standard greeting now";
        string output = engine.Expand(input);

        // Crucial Inviolable Rule: Zero Enter (\r, \n) simulation or characters
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);
        Assert.Equal("insert Hello team, Here is the daily update: All systems nominal. now", output);
    }

    [Fact]
    public void SnippetExpansionEngine_DetectsTriggerConflicts()
    {
        var engine = new SnippetExpansionEngine();
        engine.SetSnippets(new[]
        {
            new SnippetEntry { TriggerPhrase = "my link", ExpansionText = "https://link.com" },
            new SnippetEntry { TriggerPhrase = "my link today", ExpansionText = "https://link2.com" }
        });

        var conflicts = engine.GetConflicts();
        Assert.NotEmpty(conflicts);
        Assert.Contains(conflicts, c => c.Shorter == "my link" && c.Longer == "my link today");
    }

    [Fact]
    public async Task ExportAndImportJson_Snippets_Roundtrips()
    {
        await _repo.AddAsync(new SnippetEntry { TriggerPhrase = "snip1", ExpansionText = "text1" });
        await _repo.AddAsync(new SnippetEntry { TriggerPhrase = "snip2", ExpansionText = "text2", IsEnabled = false });

        string json = await _repo.ExportToJsonAsync();
        Assert.Contains("snip1", json);
        Assert.Contains("snip2", json);

        using var db2 = new SqlitePersonalizationDatabase(":memory:");
        var repo2 = new SqliteSnippetRepository(db2);

        await repo2.ImportFromJsonAsync(json);
        var all = await repo2.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, s => s.TriggerPhrase == "snip1" && s.ExpansionText == "text1" && s.IsEnabled);
        Assert.Contains(all, s => s.TriggerPhrase == "snip2" && s.ExpansionText == "text2" && !s.IsEnabled);
    }
}
