using System;
using System.Text;
using Flow.Core.Commands;
using Flow.Core.Context;
using Xunit;

namespace Flow.Core.Tests.Commands;

/// <summary>
/// Section 48: 5,000-iteration deterministic fuzzing suite.
/// Evaluates parser and policy stability against random permutations, Unicode, emojis,
/// shell syntax, malformed commands, and control codes.
/// </summary>
public class CommandFuzzTests
{
    private static readonly string[] SeedTokens =
    {
        "copy", "paste", "undo", "redo", "select", "all", "delete", "this", "that",
        "make", "bullet", "points", "numbered", "list", "uppercase", "lowercase",
        "title", "case", "camel", "snake", "pascal", "kebab", "quotes", "backticks",
        "code", "block", "trim", "whitespace", "concise", "formal", "fix", "punctuation",
        "normalize", "spacing", "open", "launch", "notepad", "terminal", "vscode",
        "calculator", "explorer", "website", "url", "https://", "http://", "www.bing.com",
        "shutdown", "reboot", "cmd", "powershell", "rm", "-rf", "del", "format", "C:",
        "drop", "table", "kill", "-9", "sudo", "runas", ";", "&&", "|", ">", "<",
        "ரத்து செய்", "पूर्ववत करो", "🔥", "🚀", "🎉", "\0", "\t", "   "
    };

    [Fact]
    public void FuzzTest_5000Iterations_ZeroCrashesOrUnhandledExceptions()
    {
        const int iterations = 5000;
        var rng = new Random(42); // Deterministic seed

        var parser = new DeterministicCommandParser();
        var policy = new DeterministicCommandPolicy();
        var context = new CommandExecutionContext(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new IntPtr(100),
            1234,
            "notepad",
            FocusedControlInfo.Empty,
            "sample selection",
            ApplicationCategory.GeneralProse
        );

        for (int i = 0; i < iterations; i++)
        {
            // Generate random string
            int tokenCount = rng.Next(1, 10);
            var sb = new StringBuilder();
            for (int t = 0; t < tokenCount; t++)
            {
                if (t > 0) sb.Append(rng.Next(0, 3) == 0 ? " " : (rng.Next(0, 2) == 0 ? "" : "  "));
                sb.Append(SeedTokens[rng.Next(SeedTokens.Length)]);
            }

            // Occasionally inject random noise characters
            if (rng.Next(0, 3) == 0)
            {
                sb.Append((char)rng.Next(32, 126));
            }

            string fuzzInput = sb.ToString();

            // 1. Parsing must never throw unhandled exceptions
            CommandIntent intent;
            try
            {
                intent = parser.Parse(fuzzInput);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Parser crashed on iteration {i} with input '{fuzzInput}': {ex.Message}", ex);
            }

            Assert.NotNull(intent);

            // 2. Policy evaluation must never throw unhandled exceptions
            CommandPolicyEvaluation evaluation;
            try
            {
                evaluation = policy.Evaluate(intent, context);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Policy evaluation crashed on iteration {i} with intent {intent.GetType().Name}: {ex.Message}", ex);
            }

            Assert.NotNull(evaluation);

            // 3. Invariant: If input contains shell syntax, verdict MUST NOT be Allow
            if (fuzzInput.Contains("shutdown", StringComparison.OrdinalIgnoreCase) ||
                fuzzInput.Contains("cmd", StringComparison.OrdinalIgnoreCase) ||
                fuzzInput.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
                fuzzInput.Contains("format C:", StringComparison.OrdinalIgnoreCase) ||
                fuzzInput.Contains("kill", StringComparison.OrdinalIgnoreCase))
                Assert.True(evaluation.Decision != CommandPolicyDecision.Allow, $"Failed on iteration {i}: fuzzInput='{fuzzInput}', intent={intent.GetType().Name}, decision={evaluation.Decision}");
        }
    }
}
