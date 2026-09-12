using System;
using Flow.Core.Commands;
using Xunit;

namespace Flow.Core.Tests.Commands;

public class DeterministicCommandSafetyPolicyTests
{
    private readonly DeterministicCommandSafetyPolicy _policy = new();

    [Theory]
    [InlineData("shutdown /s /t 0")]
    [InlineData("shutdown")]
    [InlineData("reboot")]
    [InlineData("restart-computer")]
    [InlineData("poweroff")]
    [InlineData("logoff")]
    public void EvaluateTranscript_SystemPowerCommands_Blocked(string command)
    {
        var result = _policy.EvaluateTranscript(command);
        Assert.Equal(CommandSafetyVerdict.Blocked, result.Verdict);
        Assert.Contains("strictly blocked", result.Reason);
    }

    [Theory]
    [InlineData("cmd.exe /c dir")]
    [InlineData("cmd /k start")]
    [InlineData("powershell -ExecutionPolicy Bypass")]
    [InlineData("powershell.exe Remove-Item")]
    [InlineData("pwsh -c ls")]
    [InlineData("bash -c 'rm -rf /'")]
    [InlineData("wscript script.vbs")]
    [InlineData("cscript script.vbs")]
    public void EvaluateTranscript_ShellExecution_Blocked(string command)
    {
        var result = _policy.EvaluateTranscript(command);
        Assert.Equal(CommandSafetyVerdict.Blocked, result.Verdict);
        Assert.Contains("strictly blocked", result.Reason);
    }

    [Theory]
    [InlineData("rm -rf /")]
    [InlineData("rm -r C:\\Users")]
    [InlineData("del C:\\Windows\\System32")]
    [InlineData("erase /f secret.txt")]
    [InlineData("Remove-Item C:\\test")]
    [InlineData("rmdir /s /q C:\\data")]
    [InlineData("rd /s /q C:\\data")]
    [InlineData("format C:")]
    [InlineData("format drive")]
    [InlineData("diskpart")]
    public void EvaluateTranscript_FilesystemDestruction_Blocked(string command)
    {
        var result = _policy.EvaluateTranscript(command);
        Assert.Equal(CommandSafetyVerdict.Blocked, result.Verdict);
        Assert.Contains("strictly blocked", result.Reason);
    }

    [Theory]
    [InlineData("taskkill /f /im explorer.exe")]
    [InlineData("stop-process -name notepad")]
    [InlineData("kill -9 1234")]
    [InlineData("drop table users")]
    [InlineData("drop database production")]
    [InlineData("truncate table logs")]
    [InlineData("sudo rm -rf")]
    [InlineData("runas /user:administrator cmd")]
    [InlineData("set-executionpolicy unrestricted")]
    public void EvaluateTranscript_ProcessAndPrivilegeCommands_Blocked(string command)
    {
        var result = _policy.EvaluateTranscript(command);
        Assert.Equal(CommandSafetyVerdict.Blocked, result.Verdict);
        Assert.Contains("strictly blocked", result.Reason);
    }

    [Theory]
    [InlineData("format as bullet points")]
    [InlineData("make bullet points")]
    [InlineData("make uppercase")]
    [InlineData("camel case")]
    [InlineData("trim whitespace")]
    [InlineData("wrap in quotes")]
    [InlineData("select all")]
    [InlineData("undo")]
    public void EvaluateTranscript_SafeVoiceCommands_Passes(string command)
    {
        var result = _policy.EvaluateTranscript(command);
        Assert.Equal(CommandSafetyVerdict.Safe, result.Verdict);
    }

    [Fact]
    public void EvaluateIntent_BlockedTranscriptInIntent_Blocked()
    {
        var maliciousIntent = new UnknownCommandIntent("shutdown /s /t 0", "Malicious command");
        var result = _policy.EvaluateIntent(maliciousIntent);
        Assert.Equal(CommandSafetyVerdict.Blocked, result.Verdict);
    }

    [Fact]
    public void EvaluateIntent_ValidTransformIntent_Safe()
    {
        var intent = new TransformCommandIntent("make bullet points", TransformType.BulletList);
        var result = _policy.EvaluateIntent(intent);
        Assert.Equal(CommandSafetyVerdict.Safe, result.Verdict);
    }

    [Theory]
    [InlineData("undo")]
    [InlineData("redo")]
    [InlineData("copy")]
    [InlineData("cut")]
    [InlineData("paste")]
    [InlineData("select_all")]
    [InlineData("deselect")]
    public void EvaluateIntent_PermittedEditorActions_Safe(string action)
    {
        var intent = new EditorCommandIntent(action, action);
        var result = _policy.EvaluateIntent(intent);
        Assert.Equal(CommandSafetyVerdict.Safe, result.Verdict);
    }

    [Fact]
    public void EvaluateIntent_UnknownEditorAction_FailsClosed()
    {
        var intent = new EditorCommandIntent("execute_payload", "execute_payload");
        var result = _policy.EvaluateIntent(intent);
        Assert.Equal(CommandSafetyVerdict.Unknown, result.Verdict);
    }
}
