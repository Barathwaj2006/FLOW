using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Core.Commands;

/// <summary>
/// Definition of an approved, allowlisted Windows application that may be focused or opened via Command Mode.
/// </summary>
public sealed record AllowlistedApp(
    string Id,
    string DisplayName,
    string ExecutableName,
    IReadOnlyList<string> Aliases,
    string? ProtocolScheme = null
);

/// <summary>
/// Strict immutable allowlist of approved applications.
/// Inviolable rule: Arbitrary executable paths or unapproved applications are permanently rejected.
/// </summary>
public static class ApplicationAllowlist
{
    private static readonly List<AllowlistedApp> ApprovedApps = new()
    {
        new AllowlistedApp(
            "notepad",
            "Notepad",
            "notepad.exe",
            new[] { "notepad", "text editor", "notes" }
        ),
        new AllowlistedApp(
            "vscode",
            "Visual Studio Code",
            "Code.exe",
            new[] { "vscode", "vs code", "visual studio code", "code editor" },
            "vscode:"
        ),
        new AllowlistedApp(
            "terminal",
            "Windows Terminal",
            "wt.exe",
            new[] { "terminal", "windows terminal", "command line" }
        ),
        new AllowlistedApp(
            "calculator",
            "Calculator",
            "calc.exe",
            new[] { "calculator", "calc" },
            "calculator:"
        ),
        new AllowlistedApp(
            "explorer",
            "File Explorer",
            "explorer.exe",
            new[] { "file explorer", "explorer", "files", "my documents", "downloads" }
        )
    };

    public static IReadOnlyList<AllowlistedApp> All => ApprovedApps;

    public static bool IsAllowed(string appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return false;
        return TryGetAllowlistedApp(appName, out _);
    }

    public static bool TryGetAllowlistedApp(string appName, out AllowlistedApp? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(appName)) return false;

        string normalized = appName.Trim().ToLowerInvariant();

        // Strictly reject path traversal, absolute/relative paths, UNC paths, environment variables, and metacharacters
        if (normalized.Contains('\\') || normalized.Contains('/') || normalized.Contains('%') ||
            normalized.Contains(':') || normalized.Contains('&') || normalized.Contains('|') ||
            normalized.Contains(';') || normalized.Contains(".."))
        {
            return false;
        }

        foreach (var app in ApprovedApps)
        {
            if (app.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                app.DisplayName.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                app.ExecutableName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                result = app;
                return true;
            }

            if (app.Aliases.Any(alias => alias.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                result = app;
                return true;
            }
        }

        return false;
    }
}
