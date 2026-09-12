using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Core.Context;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

public class Phase8HistoryCaptureAndPrivacyTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly HistoryPrivacyService _privacyService;
    private readonly HistoryRetentionService _retentionService;
    private readonly ProductivityStatisticsService _statsService;
    private readonly HistoryExportService _exportService;
    private readonly HistoryService _historyService;

    public Phase8HistoryCaptureAndPrivacyTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_phase8_priv_{Guid.NewGuid():N}.db");
        _database = new SqlitePersonalizationDatabase(_tempDbPath);
        _repository = new SqliteHistoryRepository(_database);
        _privacyService = new HistoryPrivacyService();
        _retentionService = new HistoryRetentionService(_repository);
        _statsService = new ProductivityStatisticsService(_repository);
        _exportService = new HistoryExportService(_repository);

        _historyService = new HistoryService(
            _repository,
            _repository,
            _statsService,
            _retentionService,
            _exportService,
            _privacyService
        );
    }

    public void Dispose()
    {
        _database.Dispose();
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task NormalDictation_PersistsTextAndMetadataAccurately()
    {
        var context = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 1234, "notepad.exe", "Untitled - Notepad"),
            Category: ApplicationCategory.Document,
            FocusedControl: FocusedControlInfo.Empty,
            IsSensitive: false,
            NearbyText: null,
            SelectionText: null
        );

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "Hello world this is a test.",
            duration: TimeSpan.FromSeconds(5),
            language: "en",
            context: context,
            mode: "Dictation"
        );

        Assert.NotNull(entry);
        Assert.Equal(HistoryState.Completed, entry.State);
        Assert.Equal("Hello world this is a test.", entry.Text);
        Assert.Equal(6, entry.WordCount);
        Assert.Equal(27, entry.CharacterCount);
        Assert.Equal("notepad.exe", entry.Application);
        Assert.NotNull(entry.TextHash);

        var fetched = await _repository.GetByIdAsync(entry.Id);
        Assert.NotNull(fetched);
        Assert.Equal(entry.Text, fetched.Text);
    }

    [Fact]
    public async Task SensitiveContext_ExcludesTextFromPersistence()
    {
        var context = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 5678, "banking.exe", "Account Balance"),
            Category: ApplicationCategory.Sensitive,
            FocusedControl: FocusedControlInfo.Empty,
            IsSensitive: true, // Marked sensitive
            NearbyText: null,
            SelectionText: null
        );

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "Secret account number 12345678",
            duration: TimeSpan.FromSeconds(4),
            language: "en",
            context: context,
            mode: "Dictation"
        );

        Assert.NotNull(entry);
        Assert.Equal(HistoryState.Excluded, entry.State);
        Assert.Null(entry.Text); // INVIOLABLE: Text is NEVER persisted for sensitive context
        Assert.Null(entry.TextHash);

        var fetched = await _repository.GetByIdAsync(entry.Id);
        Assert.NotNull(fetched);
        Assert.Null(fetched.Text);
        Assert.Equal(HistoryState.Excluded, fetched.State);
    }

    [Fact]
    public async Task PasswordField_ExcludesTextFromPersistence()
    {
        var context = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 9999, "chrome.exe", "Login"),
            Category: ApplicationCategory.Browser,
            FocusedControl: new FocusedControlInfo("Edit", "pwd", "PasswordBox", "Password", IsPassword: true, false, false), // Password field!
            IsSensitive: false,
            NearbyText: null,
            SelectionText: null
        );

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "MySecretPassword123!",
            duration: TimeSpan.FromSeconds(3),
            language: "en",
            context: context,
            mode: "Dictation"
        );

        Assert.NotNull(entry);
        Assert.Equal(HistoryState.Excluded, entry.State);
        Assert.Null(entry.Text);
        Assert.Null(entry.TextHash);

        var fetched = await _repository.GetByIdAsync(entry.Id);
        Assert.NotNull(fetched);
        Assert.Null(fetched.Text);
    }

    [Theory]
    [InlineData("keepass")]
    [InlineData("1password")]
    [InlineData("bitwarden")]
    [InlineData("credentialui")]
    public async Task CredentialManagerApp_ExcludesTextFromPersistence(string appName)
    {
        var context = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 1111, appName, "Credentials"),
            Category: ApplicationCategory.Unknown,
            FocusedControl: FocusedControlInfo.Empty,
            IsSensitive: false,
            NearbyText: null,
            SelectionText: null
        );

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "MasterPasswordSecret",
            duration: TimeSpan.FromSeconds(2),
            language: "en",
            context: context,
            mode: "Dictation"
        );

        Assert.NotNull(entry);
        Assert.Equal(HistoryState.Excluded, entry.State);
        Assert.Null(entry.Text);
    }

    [Fact]
    public async Task CommandMode_RecordsMetadataOnly_ZeroRawSelectionStored()
    {
        string metadata = "{\"Intent\":\"TransformSelection\",\"Transform\":\"Uppercase\",\"Result\":\"Success\"}";

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "", // Never store raw selection
            duration: TimeSpan.FromMilliseconds(450),
            language: "en",
            context: null,
            mode: "Command",
            state: HistoryState.Completed,
            metadataJson: metadata
        );

        Assert.NotNull(entry);
        Assert.Equal("Command", entry.Mode);
        Assert.Equal("", entry.Text);
        Assert.Equal(metadata, entry.MetadataJson);

        var fetched = await _repository.GetByIdAsync(entry.Id);
        Assert.NotNull(fetched);
        Assert.Equal("", fetched.Text);
        Assert.Equal(metadata, fetched.MetadataJson);
    }

    [Fact]
    public async Task Settings_SaveTranscriptTextDisabled_DoesNotPersistText()
    {
        var settings = await _repository.GetSettingsAsync();
        await _repository.SaveSettingsAsync(settings with { SaveTranscriptText = false });

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "This transcript should not be saved",
            duration: TimeSpan.FromSeconds(5),
            language: "en",
            context: null,
            mode: "Dictation"
        );

        Assert.NotNull(entry);
        Assert.Null(entry.Text);
        Assert.Null(entry.TextHash);
        Assert.Equal(HistoryState.Completed, entry.State);
    }

    [Fact]
    public async Task CancelledSession_PersistsAsCancelledState()
    {
        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "Partial audio cancelled by user",
            duration: TimeSpan.FromSeconds(2),
            language: "en",
            context: null,
            mode: "Dictation",
            state: HistoryState.Cancelled
        );

        Assert.NotNull(entry);
        Assert.Equal(HistoryState.Cancelled, entry.State);

        var fetched = await _repository.GetByIdAsync(entry.Id);
        Assert.NotNull(fetched);
        Assert.Equal(HistoryState.Cancelled, fetched.State);
    }
}
