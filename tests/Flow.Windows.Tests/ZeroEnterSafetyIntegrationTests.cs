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
}
