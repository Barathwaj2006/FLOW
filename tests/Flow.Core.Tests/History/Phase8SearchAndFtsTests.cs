using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8SearchAndFtsTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;

    public Phase8SearchAndFtsTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_search_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    private async Task SeedEntriesAsync()
    {
        var entries = new[]
        {
            new DictationEntry(
                Id: "e1",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-10),
                DurationMs: 15000,
                CharacterCount: 45,
                WordCount: 8,
                Language: "en",
                Application: "devenv.exe",
                ApplicationCategory: "Development",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: "Implementing DirectML tensor acceleration for whisper model.",
                IsFavorite: true
            ),
            new DictationEntry(
                Id: "e2",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-5),
                DurationMs: 20000,
                CharacterCount: 50,
                WordCount: 9,
                Language: "en",
                Application: "slack.exe",
                ApplicationCategory: "Communication",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: "Standup update: Core voice dictation pipeline is fully green.",
                IsFavorite: false
            ),
            new DictationEntry(
                Id: "e3",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-2),
                DurationMs: 12000,
                CharacterCount: 40,
                WordCount: 6,
                Language: "ta",
                Application: "notepad.exe",
                ApplicationCategory: "TextEditor",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: "வணக்கம் உலகம் இது குரல் தட்டச்சு சோதனை",
                IsFavorite: true
            ),
            new DictationEntry(
                Id: "e4",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-1),
                DurationMs: 8000,
                CharacterCount: 35,
                WordCount: 5,
                Language: "hi",
                Application: "notepad.exe",
                ApplicationCategory: "TextEditor",
                Mode: "Dictation",
                State: HistoryState.Completed,
                Text: "नमस्ते दुनिया यह आवाज टाइपिंग परीक्षण है",
                IsFavorite: false
            ),
            new DictationEntry(
                Id: "e5",
                SessionId: Guid.NewGuid(),
                CreatedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
                DurationMs: 500,
                CharacterCount: 0,
                WordCount: 0,
                Language: "en",
                Application: "code.exe",
                ApplicationCategory: "Development",
                Mode: "Command",
                State: HistoryState.Completed,
                Text: "",
                MetadataJson: "{\"Intent\":\"TransformSelection\"}"
            )
        };

        foreach (var e in entries)
        {
            await _repository.InsertAsync(e);
        }
    }

    [Fact]
    public async Task Search_ExactPhrase_ReturnsMatchingEntry()
    {
        await SeedEntriesAsync();

        var result = await _repository.SearchAsync("DirectML");
        Assert.Single(result.Items);
        Assert.Equal("e1", result.Items[0].Id);
    }

    [Fact]
    public async Task Search_PrefixQuery_FindsMatchingEntries()
    {
        await SeedEntriesAsync();

        var result = await _repository.SearchAsync("pipe*");
        Assert.Single(result.Items);
        Assert.Equal("e2", result.Items[0].Id);
    }

    [Fact]
    public async Task Search_FilterByApplication_ReturnsOnlyMatchingApp()
    {
        await SeedEntriesAsync();

        var filter = new HistoryFilter(Application: "notepad.exe");
        var result = await _repository.SearchAsync("", filter);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal("notepad.exe", i.Application));
    }

    [Fact]
    public async Task Search_FilterByLanguage_ReturnsOnlyMatchingLanguage()
    {
        await SeedEntriesAsync();

        var filter = new HistoryFilter(Language: "ta");
        var result = await _repository.SearchAsync("", filter);

        Assert.Single(result.Items);
        Assert.Equal("e3", result.Items[0].Id);
        Assert.Equal("ta", result.Items[0].Language);
    }

    [Fact]
    public async Task Search_FilterByFavorite_ReturnsOnlyFavorites()
    {
        await SeedEntriesAsync();

        var filter = new HistoryFilter(IsFavorite: true);
        var result = await _repository.SearchAsync("", filter);

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, i => Assert.True(i.IsFavorite));
    }

    [Fact]
    public async Task Search_FilterByMode_ReturnsOnlyCommandMode()
    {
        await SeedEntriesAsync();

        var filter = new HistoryFilter(Mode: "Command");
        var result = await _repository.SearchAsync("", filter);

        Assert.Single(result.Items);
        Assert.Equal("e5", result.Items[0].Id);
        Assert.Equal("Command", result.Items[0].Mode);
    }

    [Fact]
    public async Task Search_Unicode_Tamil_FindsMatch()
    {
        await SeedEntriesAsync();

        var result = await _repository.SearchAsync("வணக்கம்");
        Assert.Single(result.Items);
        Assert.Equal("e3", result.Items[0].Id);
    }

    [Fact]
    public async Task Search_Unicode_Hindi_FindsMatch()
    {
        await SeedEntriesAsync();

        var result = await _repository.SearchAsync("नमस्ते");
        Assert.Single(result.Items);
        Assert.Equal("e4", result.Items[0].Id);
    }

    [Fact]
    public async Task Search_PaginationBounds_ClampsMaxLimit()
    {
        await SeedEntriesAsync();

        // Page size over 200 should be clamped to 200
        var result = await _repository.GetPagedAsync(new HistoryFilter(), pageIndex: 0, pageSize: 500);
        Assert.True(result.PageSize <= 200);

        // Out of bounds page index returns empty list
        var emptyPage = await _repository.GetPagedAsync(new HistoryFilter(), pageIndex: 100, pageSize: 10);
        Assert.Empty(emptyPage.Items);
        Assert.Equal(5, emptyPage.TotalCount);
    }

    [Fact]
    public async Task Search_SpecialCharactersFallback_DoesNotThrow()
    {
        await SeedEntriesAsync();

        // Unbalanced quotes, operators, and special symbols
        var result1 = await _repository.SearchAsync("\"unbalanced quote");
        Assert.NotNull(result1);

        var result2 = await _repository.SearchAsync("NEAR() OR NOT AND *");
        Assert.NotNull(result2);

        var result3 = await _repository.SearchAsync(@"C:\Windows\System32\cmd.exe");
        Assert.NotNull(result3);
    }
}
