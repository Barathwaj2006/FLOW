using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8ExportSecurityTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _exportDir;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly HistoryExportService _exportService;

    public Phase8ExportSecurityTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_exp_{Guid.NewGuid():N}.db");
        _exportDir = Path.Combine(Path.GetTempPath(), $"flow_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_exportDir);

        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
        _exportService = new HistoryExportService(_repository);
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
        try { if (Directory.Exists(_exportDir)) Directory.Delete(_exportDir, true); } catch { }
    }

    private async Task SeedExportDataAsync()
    {
        await _repository.InsertAsync(new DictationEntry(
            Id: "exp_1",
            SessionId: Guid.NewGuid(),
            CreatedAt: DateTimeOffset.UtcNow.AddHours(-1),
            DurationMs: 60000,
            CharacterCount: 50,
            WordCount: 10,
            Language: "en",
            Application: "Word.exe",
            ApplicationCategory: "Document",
            Mode: "Dictation",
            State: HistoryState.Completed,
            Text: "Export test line one with \"quotes\" and, commas."
        ));
    }

    [Fact]
    public async Task Export_Json_ProducesValidJsonDocument()
    {
        await SeedExportDataAsync();
        string targetFile = Path.Combine(_exportDir, "export.json");

        await _exportService.ExportAsync(targetFile, HistoryExportFormat.Json);

        Assert.True(File.Exists(targetFile));
        string json = await File.ReadAllTextAsync(targetFile);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());

        var first = root[0];
        Assert.Equal("exp_1", first.GetProperty("id").GetString());
        Assert.Equal(10, first.GetProperty("wordCount").GetInt32());
    }

    [Fact]
    public async Task Export_Csv_ProducesValidRFC4180Format()
    {
        await SeedExportDataAsync();
        string targetFile = Path.Combine(_exportDir, "export.csv");

        await _exportService.ExportAsync(targetFile, HistoryExportFormat.Csv);

        Assert.True(File.Exists(targetFile));
        string csv = await File.ReadAllTextAsync(targetFile);

        string[] lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        Assert.True(lines.Length >= 2);
        Assert.StartsWith("Id,SessionId,CreatedAt,", lines[0]);
        Assert.Contains("Word.exe", lines[1]);
    }

    [Fact]
    public async Task Export_PlainText_ProducesReadableDocument()
    {
        await SeedExportDataAsync();
        string targetFile = Path.Combine(_exportDir, "export.txt");

        await _exportService.ExportAsync(targetFile, HistoryExportFormat.PlainText);

        Assert.True(File.Exists(targetFile));
        string txt = await File.ReadAllTextAsync(targetFile);
        Assert.Contains("App: Word.exe", txt);
        Assert.Contains("Export test line one", txt);
    }

    [Theory]
    [InlineData(@"..\evil.json")]
    [InlineData(@"sub\..\..\evil.json")]
    [InlineData(@"evil/../evil.json")]
    public async Task Export_PathTraversal_RejectedWithArgumentException(string maliciousPath)
    {
        string fullBadPath = Path.Combine(_exportDir, maliciousPath);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exportService.ExportAsync(fullBadPath, HistoryExportFormat.Json));
    }

    [Theory]
    [InlineData("CON.json")]
    [InlineData("PRN.csv")]
    [InlineData("AUX.txt")]
    [InlineData("NUL.json")]
    [InlineData("COM1.json")]
    [InlineData("LPT1.csv")]
    public async Task Export_WindowsReservedDeviceNames_Rejected(string reservedName)
    {
        string target = Path.Combine(_exportDir, reservedName);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exportService.ExportAsync(target, HistoryExportFormat.Json));
    }

    [Theory]
    [InlineData(@"\\remoteServer\share\export.json")]
    [InlineData(@"\\192.168.1.100\c$\export.json")]
    public async Task Export_UNCNetworkPaths_Rejected(string uncPath)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exportService.ExportAsync(uncPath, HistoryExportFormat.Json));
    }

    [Theory]
    [InlineData(@"\\.\C:\temp\export.json")]
    [InlineData(@"\\?\C:\temp\export.json")]
    public async Task Export_DeviceNamespaces_Rejected(string devicePath)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _exportService.ExportAsync(devicePath, HistoryExportFormat.Json));
    }

    [Fact]
    public async Task Export_OverwriteDefense_ThrowsIfDestinationExistsAndOverwriteFalse()
    {
        await SeedExportDataAsync();
        string targetFile = Path.Combine(_exportDir, "existing.json");
        await File.WriteAllTextAsync(targetFile, "existing contents");

        // Attempting export with overwrite: false must throw IOException
        await Assert.ThrowsAsync<IOException>(() =>
            _exportService.ExportAsync(targetFile, HistoryExportFormat.Json, overwrite: false));

        // Overwrite: true must succeed
        await _exportService.ExportAsync(targetFile, HistoryExportFormat.Json, overwrite: true);
        Assert.NotEqual("existing contents", await File.ReadAllTextAsync(targetFile));
    }
}
