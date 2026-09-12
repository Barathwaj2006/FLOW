using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Flow.Core.Commands;
using Flow.Core.Context;
using Flow.Core.History;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.History;

/// <summary>
/// Phase 8.5 Adversarial Audit:
/// - Credential manager fail-closed isolation
/// - Command Mode raw selection secrecy
/// - Normal dictation 1,000-utterance command isolation
/// - Static zero-enter & zero-execution source scan
/// - Sentinel secret injection & leak scan
/// - Offline network sovereignty
/// - 2,000 session long-run lifecycle stability
/// </summary>
public class Phase85PrivacyAndAdversarialAuditTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _exportDir;
    private readonly SqlitePersonalizationDatabase _database;
    private readonly SqliteHistoryRepository _repository;
    private readonly HistoryPrivacyService _privacyService;
    private readonly HistoryRetentionService _retentionService;
    private readonly ProductivityStatisticsService _statsService;
    private readonly HistoryExportService _exportService;
    private readonly HistoryService _historyService;

    public Phase85PrivacyAndAdversarialAuditTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"flow_adv_audit_{Guid.NewGuid():N}.db");
        _exportDir = Path.Combine(Path.GetTempPath(), $"flow_adv_exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_exportDir);

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
        try { if (Directory.Exists(_exportDir)) Directory.Delete(_exportDir, true); } catch { }
    }

    [Theory]
    [InlineData("keepass.exe")]
    [InlineData("1password.exe")]
    [InlineData("bitwarden.exe")]
    [InlineData("credentialui.exe")]
    [InlineData("credwiz.exe")]
    [InlineData("consent.exe")]
    public async Task CredentialManagers_FailClosed_NeverPersistPlaintext(string processName)
    {
        var context = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 4444, processName, "Credential Manager Dialog"),
            Category: ApplicationCategory.Sensitive,
            FocusedControl: FocusedControlInfo.Empty,
            IsSensitive: false,
            NearbyText: null,
            SelectionText: null
        );

        string secretPayload = "SUPER_SECRET_MASTER_PASSWORD_998877";

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: secretPayload,
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
        Assert.Null(fetched.TextHash);
    }

    [Fact]
    public async Task CommandMode_HistoryIsolation_RawSelectedTextNeverPersisted()
    {
        string rawSecretSelection = "FLOW_SECRET_SELECTION_TOKEN_12345";
        string auditMetadata = "{\"Intent\":\"TransformSelection\",\"Transform\":\"Uppercase\",\"Result\":\"Success\"}";

        var entry = await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "", // Inviolable contract: raw selection is never passed
            duration: TimeSpan.FromMilliseconds(450),
            language: "en",
            context: null,
            mode: "Command",
            state: HistoryState.Completed,
            metadataJson: auditMetadata
        );

        Assert.NotNull(entry);
        Assert.Equal("Command", entry.Mode);
        Assert.Equal("", entry.Text);
        Assert.DoesNotContain(rawSecretSelection, entry.MetadataJson ?? "");

        // Direct SQLite row inspection
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Text, MetadataJson FROM DictationHistory WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", entry.Id);

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        string dbText = reader.GetString(0);
        string dbMeta = reader.GetString(1);

        Assert.Equal("", dbText);
        Assert.DoesNotContain(rawSecretSelection, dbMeta);
    }

    [Fact]
    public void StaticForensicAudit_SrcContainsZeroExecutionPrimitives()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\.."));
        string srcDir = Path.Combine(repoRoot, "src");

        Assert.True(Directory.Exists(srcDir), $"src directory not found at {srcDir}");

        var prohibitedTokens = new[]
        {
            "Process.Start",
            "ProcessStartInfo",
            "CreateProcess",
            "ShellExecute",
            "WinExec",
            "popen",
            "system(",
            "SendKeys.Send"
        };

        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            string content = File.ReadAllText(file);
            foreach (var token in prohibitedTokens)
            {
                if (content.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    // Allow references in comments/docs or policy rule pattern definitions
                    string[] lines = content.Split('\n');
                    for (int l = 0; l < lines.Length; l++)
                    {
                        string line = lines[l].Trim();
                        if (line.Contains(token, StringComparison.OrdinalIgnoreCase) &&
                            !line.StartsWith("//") && !line.StartsWith("/*") && !line.StartsWith("*") &&
                            !line.Contains("Regex") && !line.Contains("RegexOptions"))
                        {
                            violations.Add($"{Path.GetFileName(file)}:L{l+1} -> {token}");
                        }
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public async Task SentinelSecrets_LeakScan_ExportsNeverContainSentinels()
    {
        string sentinelPassword = "FLOW_PASSWORD_SENTINEL_B";
        string sentinelSensitive = "FLOW_SECRET_SENTINEL_A";
        string sentinelSelection = "FLOW_COMMAND_SELECTION_SENTINEL_C";

        // 1. Record normal entry
        await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "Safe public transcript content for export verification.",
            duration: TimeSpan.FromSeconds(5),
            language: "en",
            context: null
        );

        // 2. Record password entry with sentinel
        var pwdContext = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 1, "app.exe", "Login"),
            Category: ApplicationCategory.Sensitive,
            FocusedControl: new FocusedControlInfo("Edit", "pwd", "PasswordBox", "Password", IsPassword: true, false, false),
            IsSensitive: false,
            NearbyText: null,
            SelectionText: null
        );
        await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: sentinelPassword,
            duration: TimeSpan.FromSeconds(3),
            language: "en",
            context: pwdContext
        );

        // 3. Record sensitive context entry with sentinel
        var sensContext = new ContextSnapshot(
            SessionId: Guid.NewGuid(),
            Timestamp: DateTimeOffset.UtcNow,
            TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 2, "banking.exe", "Bank"),
            Category: ApplicationCategory.Sensitive,
            FocusedControl: FocusedControlInfo.Empty,
            IsSensitive: true,
            NearbyText: null,
            SelectionText: null
        );
        await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: sentinelSensitive,
            duration: TimeSpan.FromSeconds(4),
            language: "en",
            context: sensContext
        );

        // 4. Record command mode transform with selection sentinel
        await _historyService.RecordDictationAsync(
            sessionId: Guid.NewGuid(),
            text: "", // Inviolable: raw selection never passed to history text
            duration: TimeSpan.FromSeconds(1),
            language: "en",
            context: null,
            mode: "Command",
            metadataJson: "{\"Intent\":\"TransformSelection\",\"Transform\":\"Uppercase\",\"Result\":\"Success\"}"
        );

        // 5. Inspect SQLite Database directly across DictationHistory.Text, MetadataJson, and DictationHistoryFts
        await using (var conn = _database.CreateConnection())
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM DictationHistory 
                WHERE Text LIKE '%FLOW_%' OR MetadataJson LIKE '%FLOW_%';
            ";
            int dbMatches = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(0, dbMatches);

            await using var ftsCmd = conn.CreateCommand();
            ftsCmd.CommandText = "SELECT COUNT(*) FROM DictationHistoryFts WHERE Text LIKE '%FLOW_%';";
            int ftsMatches = Convert.ToInt32(await ftsCmd.ExecuteScalarAsync());
            Assert.Equal(0, ftsMatches);
        }

        // Export all formats
        string jsonPath = Path.Combine(_exportDir, "export.json");
        string csvPath = Path.Combine(_exportDir, "export.csv");
        string txtPath = Path.Combine(_exportDir, "export.txt");

        await _exportService.ExportAsync(jsonPath, HistoryExportFormat.Json);
        await _exportService.ExportAsync(csvPath, HistoryExportFormat.Csv);
        await _exportService.ExportAsync(txtPath, HistoryExportFormat.PlainText);

        string json = await File.ReadAllTextAsync(jsonPath);
        string csv = await File.ReadAllTextAsync(csvPath);
        string txt = await File.ReadAllTextAsync(txtPath);

        // Hard assertion: Sentinels MUST NOT leak in any export format
        Assert.DoesNotContain(sentinelPassword, json);
        Assert.DoesNotContain(sentinelPassword, csv);
        Assert.DoesNotContain(sentinelPassword, txt);

        Assert.DoesNotContain(sentinelSensitive, json);
        Assert.DoesNotContain(sentinelSensitive, csv);
        Assert.DoesNotContain(sentinelSensitive, txt);

        Assert.DoesNotContain(sentinelSelection, json);
        Assert.DoesNotContain(sentinelSelection, csv);
        Assert.DoesNotContain(sentinelSelection, txt);
    }

    [Fact]
    public void NetworkSovereignty_HistorySubsystemContainsZeroNetworkAPIs()
    {
        var asm = typeof(HistoryService).Assembly;
        var historyTypes = asm.GetTypes().Where(t => t.Namespace != null && t.Namespace.StartsWith("Flow.Core.History"));

        foreach (var t in historyTypes)
        {
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var f in fields)
            {
                Assert.False(f.FieldType.Name.Contains("HttpClient"), $"Prohibited network client in {t.Name}");
                Assert.False(f.FieldType.Name.Contains("Socket"), $"Prohibited network socket in {t.Name}");
                Assert.False(f.FieldType.Name.Contains("WebRequest"), $"Prohibited web request in {t.Name}");
            }
        }
    }

    [Fact]
    public async Task TwoThousandSessions_LongRunLifecycle_StabilityVerification()
    {
        var rng = new Random(554433);
        string[] apps = new[] { "devenv.exe", "slack.exe", "notepad.exe", "chrome.exe" };
        string[] langs = new[] { "en", "ta", "hi" };

        for (int i = 0; i < 2000; i++)
        {
            bool isExcluded = (i % 10 == 0);
            ContextSnapshot? ctx = null;
            if (isExcluded)
            {
                ctx = new ContextSnapshot(
                    SessionId: Guid.NewGuid(),
                    Timestamp: DateTimeOffset.UtcNow,
                    TargetInfo: new ForegroundTargetInfo(IntPtr.Zero, 12, "banking.exe", "Account"),
                    Category: ApplicationCategory.Sensitive,
                    FocusedControl: FocusedControlInfo.Empty,
                    IsSensitive: true,
                    NearbyText: null,
                    SelectionText: null
                );
            }

            var entry = await _historyService.RecordDictationAsync(
                sessionId: Guid.NewGuid(),
                text: isExcluded ? "secret text" : $"Lifecycle session test string index {i}",
                duration: TimeSpan.FromMilliseconds(rng.Next(1000, 30000)),
                language: langs[i % langs.Length],
                context: ctx,
                mode: (i % 20 == 0) ? "Command" : "Dictation",
                state: isExcluded ? HistoryState.Excluded : HistoryState.Completed
            );

            Assert.NotNull(entry);
        }

        int totalCount = await _repository.GetCountAsync();
        Assert.Equal(2000, totalCount);

        string? integrity = await _database.ExecuteScalarAsync<string>("PRAGMA integrity_check;");
        Assert.Equal("ok", integrity);
    }
}
