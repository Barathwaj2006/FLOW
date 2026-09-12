using System;
using System.Collections.Generic;

namespace Flow.Core.Context;

/// <summary>
/// Deterministic rule-based application classifier.
/// Maps executable process names and window characteristics to application categories.
/// </summary>
public sealed class RuleBasedApplicationClassifier : IApplicationClassifier
{
    private static readonly HashSet<string> SensitiveProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "credentialuibroker",
        "consent",
        "keepass",
        "keepassxc",
        "1password",
        "bitwarden",
        "lastpass",
        "dashlane",
        "enpass",
        "robofrm"
    };

    private static readonly HashSet<string> CodeProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "code",
        "cursor",
        "windsurf",
        "devenv",
        "idea64",
        "pycharm64",
        "webstorm64",
        "clion64",
        "rider64",
        "goland64",
        "rustrover64",
        "sublime_text",
        "notepad++",
        "visualstudio",
        "vsimproc",
        "zed",
        "neovim",
        "nvim",
        "emacs"
    };

    private static readonly HashSet<string> TerminalProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "windowsterminal",
        "powershell",
        "pwsh",
        "cmd",
        "conhost",
        "mintty",
        "bash",
        "wsl",
        "alacritty",
        "wezterm",
        "hyper",
        "tabby",
        "wt"
    };

    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "vivaldi",
        "arc",
        "tor",
        "waterfox",
        "chromium"
    };

    private static readonly HashSet<string> DocumentProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "winword",
        "excel",
        "powerpnt",
        "onenote",
        "acrobat",
        "acrord32",
        "foxitreader",
        "wps",
        "soffice.bin",
        "libreoffice"
    };

    private static readonly HashSet<string> GeneralProseProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "notepad",
        "wordpad",
        "textpad",
        "stickynotes"
    };

    public ApplicationCategory Classify(ForegroundTargetInfo targetInfo)
    {
        if (targetInfo == null)
        {
            return ApplicationCategory.Unknown;
        }

        string processName = CleanProcessName(targetInfo.ProcessName);
        string title = targetInfo.WindowTitle?.ToLowerInvariant() ?? string.Empty;

        // 1. Sensitive process or window title check
        if (SensitiveProcesses.Contains(processName) ||
            title.Contains("windows security") ||
            title.Contains("credential") ||
            title.Contains("1password") ||
            title.Contains("bitwarden") ||
            title.Contains("keepass"))
        {
            return ApplicationCategory.Sensitive;
        }

        // 2. Code editors & IDEs
        if (CodeProcesses.Contains(processName))
        {
            return ApplicationCategory.Code;
        }

        // 3. Terminals & shells
        if (TerminalProcesses.Contains(processName))
        {
            return ApplicationCategory.Terminal;
        }

        // 4. Web browsers
        if (BrowserProcesses.Contains(processName))
        {
            return ApplicationCategory.Browser;
        }

        // 5. Office & document processors
        if (DocumentProcesses.Contains(processName))
        {
            return ApplicationCategory.Document;
        }

        // 6. Simple text prose editors
        if (GeneralProseProcesses.Contains(processName))
        {
            return ApplicationCategory.GeneralProse;
        }

        return ApplicationCategory.Unknown;
    }

    private static string CleanProcessName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;
        string name = rawName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return name;
    }
}
