using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class ApplicationAllowlistTests
{
    [Theory]
    [InlineData("notepad")]
    [InlineData("Notepad")]
    [InlineData("notepad.exe")]
    [InlineData("text editor")]
    [InlineData("vscode")]
    [InlineData("vs code")]
    [InlineData("visual studio code")]
    [InlineData("Code.exe")]
    [InlineData("terminal")]
    [InlineData("windows terminal")]
    [InlineData("wt.exe")]
    [InlineData("calculator")]
    [InlineData("calc")]
    [InlineData("calc.exe")]
    [InlineData("file explorer")]
    [InlineData("explorer")]
    [InlineData("explorer.exe")]
    public void Allowlist_ApprovedApplications_Permitted(string appName)
    {
        Assert.True(ApplicationAllowlist.IsAllowed(appName));
        Assert.True(ApplicationAllowlist.TryGetAllowlistedApp(appName, out var app));
        Assert.NotNull(app);
    }

    [Theory]
    [InlineData("powershell")]
    [InlineData("cmd")]
    [InlineData("cmd.exe")]
    [InlineData("pwsh")]
    [InlineData("bash")]
    [InlineData("random_malware.exe")]
    [InlineData("C:\\virus.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void Allowlist_UnapprovedOrDangerous_Rejected(string? appName)
    {
        Assert.False(ApplicationAllowlist.IsAllowed(appName!));
        Assert.False(ApplicationAllowlist.TryGetAllowlistedApp(appName!, out var app));
        Assert.Null(app);
    }

    [Theory]
    [InlineData("https://github.com/")]
    [InlineData("https://flow.ai/docs")]
    [InlineData("http://localhost:8080")]
    [InlineData("www.bing.com")]
    public void UrlSafetyValidator_ApprovedSchemes_Permitted(string url)
    {
        Assert.True(UrlSafetyValidator.TryValidateUrl(url, out var uri, out var failure));
        Assert.NotNull(uri);
        Assert.Null(failure);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("shell:startup")]
    [InlineData("ms-settings:privacy")]
    [InlineData("data:text/html,<b>xss</b>")]
    [InlineData("")]
    [InlineData(null)]
    public void UrlSafetyValidator_ProhibitedSchemes_Rejected(string? url)
    {
        Assert.False(UrlSafetyValidator.TryValidateUrl(url!, out var uri, out var failure));
        Assert.Null(uri);
        Assert.NotNull(failure);
    }

    [Fact]
    public void FolderSafetyValidator_RelativePaths_Rejected()
    {
        Assert.False(FolderSafetyValidator.TryValidateFolder("../relative/path", out _, out var failure));
        Assert.Contains("traversal", failure, StringComparison.OrdinalIgnoreCase);
    }
}
