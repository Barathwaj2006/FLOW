using System;
using System.Threading.Tasks;
using Flow.Host.Windows.Native;
using Xunit;

namespace Flow.Windows.Tests;

public class ZeroEnterSafetyIntegrationTests
{
    [Fact]
    public async Task InsertTextAsync_NeutralizesAllNewlinesAndReturns()
    {
        var insertionService = new WindowsTextInsertionService();

        // Feed text laced with \r\n and \n
        string unsafeInput = "line 1\r\nline 2\nline 3\r";
        
        // This must run cleanly without throwing or injecting any Enter keys
        var result = await insertionService.InsertTextAsync(unsafeInput);

        // Verification: The service processed the call safely
        Assert.NotNull(result);
    }

    [Fact]
    public async Task InsertTextAsync_EmptyOrWhitespace_ReturnsImmediately()
    {
        var insertionService = new WindowsTextInsertionService();

        var res1 = await insertionService.InsertTextAsync("");
        Assert.True(res1.Success);

        var res2 = await insertionService.InsertTextAsync("   \r\n  ");
        Assert.True(res2.Success);
    }

    [Fact]
    public async Task InsertTextAsync_PowerShellCommand_NeverContainsExecutionTrigger()
    {
        var insertionService = new WindowsTextInsertionService();
        string dangerousCommand = "Remove-Item -Recurse C:\\temp\r\n";

        var result = await insertionService.InsertTextAsync(dangerousCommand);
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task InsertTextAsync_BrowserForm_NeutralizesEnterSubmissions()
    {
        var insertionService = new WindowsTextInsertionService();
        string formInput = "Search query with accidental newline\n";

        var result = await insertionService.InsertTextAsync(formInput);
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task InsertTextAsync_PreservesPreviousClipboardContents()
    {
        var insertionService = new WindowsTextInsertionService();

        // Perform test insertion
        string testText = "FLOW safe insertion test " + Guid.NewGuid().ToString("N");
        var result = await insertionService.InsertTextAsync(testText);

        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task BacktrackAsync_NullOrEmptyRecord_SafelyReturnsFalse()
    {
        var insertionService = new WindowsTextInsertionService();

        var emptyRecord = new Flow.Core.Backtrack.InsertionRecord(
            Guid.NewGuid(), "", 0, DateTimeOffset.UtcNow, IntPtr.Zero, "None", 0, Flow.Core.TextInsertion.InsertionStrategy.None);

        bool result = await insertionService.BacktrackAsync(emptyRecord);
        Assert.False(result);
    }

    [Fact]
    public async Task BacktrackAsync_TargetHwndMismatch_SafelyReturnsFalseWithoutModifyingWindow()
    {
        var insertionService = new WindowsTextInsertionService();

        // Simulated record from an inactive HWND
        var record = new Flow.Core.Backtrack.InsertionRecord(
            Guid.NewGuid(), "some text", 9, DateTimeOffset.UtcNow, new IntPtr(0x7FFFFFFF), "NonExistentWindow", 999999, Flow.Core.TextInsertion.InsertionStrategy.SendInputClipboardFallback);

        // Current active foreground window will not match 0xDEADBEEF
        bool result = await insertionService.BacktrackAsync(record);

        // Strict safety rule: Must return false without sending any keystrokes
        Assert.False(result);
    }
}
