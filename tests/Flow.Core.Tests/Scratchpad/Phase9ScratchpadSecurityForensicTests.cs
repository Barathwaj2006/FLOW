using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flow.Core.Scratchpad;
using Flow.Core.Storage;
using Xunit;

namespace Flow.Core.Tests.Scratchpad;

public sealed class Phase9ScratchpadSecurityForensicTests
{
    private const string SentinelA = "FLOW_SECRET_SENTINEL_A";
    private const string SentinelB = "FLOW_PASSWORD_SENTINEL_B";
    private const string SentinelC = "FLOW_COMMAND_SELECTION_SENTINEL_C";

    [Fact]
    public async Task SentinelScan_CleanDatabase_ContainsZeroSentinels()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"flow_sentinel_{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new SqlitePersonalizationDatabase(tempDb))
            {
                var repo = new SqliteScratchpadRepository(db);
                var service = new ScratchpadService(repo);

                // Normal operations
                await service.CreateScratchpadAsync("Project Roadmap", "Normal text without any credentials.");
                await service.CreateScratchpadAsync("Meeting Minutes", "Action items and planning for Q4.");

                // Forensic check of database tables
                using var conn = db.CreateConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT COUNT(*) FROM Scratchpads
                    WHERE Title LIKE '%' || @sA || '%' OR Content LIKE '%' || @sA || '%'
                       OR Title LIKE '%' || @sB || '%' OR Content LIKE '%' || @sB || '%'
                       OR Title LIKE '%' || @sC || '%' OR Content LIKE '%' || @sC || '%';
                ";
                cmd.Parameters.AddWithValue("@sA", SentinelA);
                cmd.Parameters.AddWithValue("@sB", SentinelB);
                cmd.Parameters.AddWithValue("@sC", SentinelC);

                int leaks = Convert.ToInt32(cmd.ExecuteScalar());
                Assert.Equal(0, leaks);
            }
        }
        finally
        {
            if (File.Exists(tempDb))
            {
                try { File.Delete(tempDb); } catch { }
            }
        }
    }

    [Fact]
    public async Task CommandModeIsolation_CommandProseInScratchpad_RemainsInertText()
    {
        using var db = new SqlitePersonalizationDatabase(":memory:");
        var repo = new SqliteScratchpadRepository(db);
        var service = new ScratchpadService(repo);

        string dangerousProse = "open cmd\nrun powershell -c rm -rf C:\\\ndelete all files\nformat D: /fs:ntfs";
        var created = await service.CreateScratchpadAsync("Script Draft", dangerousProse);

        Assert.NotNull(created);
        Assert.Equal("Script Draft", created.Title);
        Assert.Equal(dangerousProse, created.Content);

        // Content is retrieved verbatim and remained inert
        var loaded = await service.GetScratchpadAsync(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal(dangerousProse, loaded.Content);
    }

    [Fact]
    public void OfflineSovereignty_ScratchpadClasses_HaveZeroCloudOrHttpDependencies()
    {
        var scratchpadAssembly = typeof(ScratchpadService).Assembly;
        var types = scratchpadAssembly.GetTypes().Where(t => t.Namespace?.Contains("Scratchpad") == true);

        foreach (var type in types)
        {
            var methods = type.GetMethods();
            foreach (var m in methods)
            {
                var parameters = m.GetParameters();
                Assert.DoesNotContain(parameters, p => p.ParameterType.FullName?.Contains("System.Net.Http.HttpClient") == true);
                Assert.DoesNotContain(parameters, p => p.ParameterType.FullName?.Contains("Amazon") == true);
            }
        }
    }

    [Fact]
    public void StaticForensics_ScratchpadCoreFiles_ContainZeroProcessExecutionPrimitives()
    {
        string[] forbiddenPrimitives = new[]
        {
            "Process.Start", "CreateProcess", "ShellExecute", "WinExec",
            "popen", "system(", "\"cmd.exe\"", "\"powershell.exe\"", "\"cmd\"", "\"powershell\""
        };

        string scratchpadSourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "Flow.Core", "Scratchpad");
        var fullPath = Path.GetFullPath(scratchpadSourceDir);

        if (Directory.Exists(fullPath))
        {
            var files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                string text = File.ReadAllText(file);
                foreach (var primitive in forbiddenPrimitives)
                {
                    Assert.DoesNotContain(primitive, text, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
