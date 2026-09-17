using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;
using Flow.Host.Windows.Diagnostics;
using Flow.Host.Windows.UI;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Flow.Windows.Tests;

public sealed class DiagnosticsAndCrashReporterTests
{
    private static void RunOnSta(Action action)
    {
        Exception? ex = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(TimeSpan.FromSeconds(10));
        Assert.True(finished, "STA execution timed out after 10s.");
        if (ex != null) throw ex;
    }

    [Fact]
    public void PiiDataScrubber_RedactsApiKeys_AndBearerTokens()
    {
        string input = "Header: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 and Key: sk-123456789012345678901234 and AWS: AKIAIOSFODNN7EXAMPLE";
        string scrubbed = PiiDataScrubber.Scrub(input);

        Assert.DoesNotContain("eyJhbGci", scrubbed);
        Assert.DoesNotContain("sk-123456789012345678901234", scrubbed);
        Assert.DoesNotContain("AKIAIOSFODNN7EXAMPLE", scrubbed);

        Assert.Contains("Bearer [REDACTED_API_KEY]", scrubbed);
        Assert.Contains("[REDACTED_API_KEY]", scrubbed);
    }

    [Fact]
    public void PiiDataScrubber_RedactsEmailsAndCreditCards()
    {
        string input = "User email is test.user@enterprise-corp.com and card number is 4111 2222 3333 4444.";
        string scrubbed = PiiDataScrubber.Scrub(input);

        Assert.DoesNotContain("test.user@enterprise-corp.com", scrubbed);
        Assert.DoesNotContain("4111 2222 3333 4444", scrubbed);

        Assert.Contains("[REDACTED_EMAIL]", scrubbed);
        Assert.Contains("[REDACTED_CARD]", scrubbed);
    }

    [Fact]
    public void PiiDataScrubber_RedactsUserProfilePaths()
    {
        string input = @"Loaded model from C:\Users\barathwaj\AppData\Local\FLOW\models\ggml-tiny.bin";
        string scrubbed = PiiDataScrubber.Scrub(input);

        Assert.DoesNotContain("barathwaj", scrubbed);
        Assert.Contains(@"C:\Users\[REDACTED_USER]\AppData\Local\FLOW\models\ggml-tiny.bin", scrubbed);
    }

    [Fact]
    public void PiiDataScrubber_RedactsDictationTranscripts()
    {
        string input = @"Coordinator completed recognized text: ""Send the wire transfer immediately""";
        string scrubbed = PiiDataScrubber.Scrub(input);

        Assert.DoesNotContain("wire transfer", scrubbed);
        Assert.Contains("recognized text: [REDACTED_TRANSCRIPT]", scrubbed);
    }

    [Fact]
    public async Task RedactingFileLogger_WritesSanitizedLogsToDisk()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"flow_test_logs_{Guid.NewGuid():N}");
        try
        {
            await using (var provider = new RedactingFileLoggerProvider(tempDir, LogLevel.Information))
            {
                var logger = provider.CreateLogger("TestCategory");
                logger.LogInformation("Connecting to service with secret sk-99887766554433221100aa for user test@domain.com");

                // Give async queue worker time to process
                await Task.Delay(350);
            }

            string[] logFiles = Directory.GetFiles(tempDir, "flow-*.log");
            Assert.NotEmpty(logFiles);

            string content = await File.ReadAllTextAsync(logFiles[0]);
            Assert.Contains("[INF] [TestCategory]", content);
            Assert.DoesNotContain("sk-99887766554433221100aa", content);
            Assert.DoesNotContain("test@domain.com", content);
            Assert.Contains("[REDACTED_API_KEY]", content);
            Assert.Contains("[REDACTED_EMAIL]", content);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CrashReporter_WritesMetadataAndManifest()
    {
        string tempCrashes = Path.Combine(Path.GetTempPath(), $"flow_test_crashes_{Guid.NewGuid():N}");
        CrashReporter.SetCustomCrashesDirectory(tempCrashes);

        try
        {
            var testEx = new InvalidOperationException("Simulation of audio buffer overflow: secret sk-11223344556677889900aabb");
            var (dumpPath, metaPath) = CrashReporter.WriteCrashDump(testEx, "UnitTestingCrash");

            Assert.NotNull(metaPath);
            Assert.True(File.Exists(metaPath));

            string json = File.ReadAllText(metaPath);
            Assert.Contains("\"Reason\": \"UnitTestingCrash\"", json);
            Assert.Contains("\"ExceptionType\": \"System.InvalidOperationException\"", json);
            Assert.DoesNotContain("sk-11223344556677889900aabb", json);
            Assert.Contains("[REDACTED_API_KEY]", json);

            var manifest = CrashReporter.CheckPreviousCrash();
            Assert.NotNull(manifest);
            Assert.True(manifest!.TotalCrashCount >= 1);
            Assert.Equal("UnitTestingCrash", manifest.LastCrashReason);

            CrashReporter.ClearCrashDumps();
            var manifestAfterClear = CrashReporter.CheckPreviousCrash();
            Assert.Null(manifestAfterClear);
        }
        finally
        {
            CrashReporter.SetCustomCrashesDirectory(null);
            try { Directory.Delete(tempCrashes, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task FlowDiagnosticsService_ExportDiagnosticsBundle_CreatesValidZip()
    {
        string tempLogs = Path.Combine(Path.GetTempPath(), $"flow_diag_logs_{Guid.NewGuid():N}");
        string tempCrashes = Path.Combine(Path.GetTempPath(), $"flow_diag_crashes_{Guid.NewGuid():N}");
        string tempExport = Path.Combine(Path.GetTempPath(), $"flow_diag_out_{Guid.NewGuid():N}");

        Directory.CreateDirectory(tempLogs);
        Directory.CreateDirectory(tempCrashes);

        await File.WriteAllTextAsync(Path.Combine(tempLogs, "flow-20260917.log"), "[2026-09-17] Normal diagnostic event");
        await File.WriteAllTextAsync(Path.Combine(tempCrashes, "flow_crash_test.json"), "{\"test\": true}");

        try
        {
            var diagService = new FlowDiagnosticsService(
                settingsRepo: null,
                logsDirectory: tempLogs,
                crashesDirectory: tempCrashes);

            string zipPath = await diagService.ExportDiagnosticsBundleAsync(tempExport);

            Assert.True(File.Exists(zipPath));
            Assert.EndsWith(".zip", zipPath);

            using var archive = ZipFile.OpenRead(zipPath);
            Assert.Contains(archive.Entries, e => e.FullName == "system_summary.json");
            Assert.Contains(archive.Entries, e => e.FullName.Contains("flow-20260917.log"));
            Assert.Contains(archive.Entries, e => e.FullName.Contains("flow_crash_test.json"));
        }
        finally
        {
            try { Directory.Delete(tempLogs, recursive: true); } catch { }
            try { Directory.Delete(tempCrashes, recursive: true); } catch { }
            try { Directory.Delete(tempExport, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FlowShellWindow_HandleAction_GetDiagnosticsInfo_ReturnsValidResponse()
    {
        RunOnSta(() =>
        {
            var diagService = new FlowDiagnosticsService();
            var window = new FlowShellWindow(diagnosticsService: diagService);

            string? respondedAction = null;
            bool? wasSuccess = null;
            object? responsePayload = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                respondedAction = action;
                responsePayload = payload;
                wasSuccess = success;
            };

            using var doc = JsonDocument.Parse("{}");
            window.HandleActionAsync("req-diag-1", "get-diagnostics-info", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("get-diagnostics-info", respondedAction);
            Assert.True(wasSuccess);
            Assert.NotNull(responsePayload);

            string json = JsonSerializer.Serialize(responsePayload);
            Assert.Contains("\"totalCrashCount\":", json);
            Assert.Contains("\"logsDirectory\":", json);
            Assert.Contains("\"enableAnonymousTelemetry\":", json);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_ClearCrashReports_ReturnsSuccess()
    {
        RunOnSta(() =>
        {
            var diagService = new FlowDiagnosticsService();
            var window = new FlowShellWindow(diagnosticsService: diagService);

            string? respondedAction = null;
            bool? wasSuccess = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                respondedAction = action;
                wasSuccess = success;
            };

            using var doc = JsonDocument.Parse("{}");
            window.HandleActionAsync("req-diag-2", "clear-crash-reports", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("clear-crash-reports", respondedAction);
            Assert.True(wasSuccess);
        });
    }

    [Fact]
    public void FlowShellWindow_HandleAction_SetTelemetryOptIn_ReturnsSuccess()
    {
        RunOnSta(() =>
        {
            var diagService = new FlowDiagnosticsService();
            var window = new FlowShellWindow(diagnosticsService: diagService);

            string? respondedAction = null;
            bool? wasSuccess = null;
            object? responsePayload = null;

            window.MessageSent += (reqId, action, payload, success) =>
            {
                respondedAction = action;
                responsePayload = payload;
                wasSuccess = success;
            };

            using var doc = JsonDocument.Parse("{\"enabled\": true}");
            window.HandleActionAsync("req-diag-3", "set-telemetry-opt-in", doc.RootElement).GetAwaiter().GetResult();

            Assert.Equal("set-telemetry-opt-in", respondedAction);
            Assert.True(wasSuccess);

            string json = JsonSerializer.Serialize(responsePayload);
            Assert.Contains("\"enabled\":true", json);
        });
    }
}
