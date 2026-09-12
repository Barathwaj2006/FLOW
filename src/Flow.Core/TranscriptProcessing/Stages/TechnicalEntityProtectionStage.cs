using System;
using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Identifies and masks technical tokens (file paths, URLs, code identifiers, CLI commands, acronyms)
/// so that subsequent punctuation, filler-removal, and capitalization stages do not corrupt them (WF-033).
/// </summary>
public sealed class TechnicalEntityProtectionStage : ITranscriptStage
{
    // 1. URLs and Emails
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s]+|\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 2. Windows File Paths (e.g. C:\Users\... or .\foo\bar or relative src/...)
    private static readonly Regex FilePathRegex = new(
        @"\b[A-Za-z]:\\[A-Za-z0-9._\-\ ]+|\b(?:\.|\.\.)?\\[A-Za-z0-9._\-\\]+|\b(?:src|tests|docs|lib|bin|obj|scripts)/[A-Za-z0-9._\-/]+",
        RegexOptions.Compiled);

    // 3. Known CLI commands and PowerShell cmdlets
    private static readonly Regex CliCommandRegex = new(
        @"\b(git (?:status|checkout|commit|push|pull|merge|rebase|log|diff|branch|clone|fetch|init|reset|stash)|dotnet (?:test|build|run|restore|clean|publish|pack|tool|new)|npm (?:install|run|test|start|build|publish|init|update)|pip (?:install|list|freeze|uninstall)|docker (?:run|ps|compose|build|stop|exec|images|pull)|kubectl (?:get|apply|describe|delete|logs|exec)|cargo (?:build|test|run|check)|Get-Process|Remove-Item|Set-Content|Get-Content|Stop-Process|Start-Process|powershell|pwsh|cmd\.exe)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 4. CLI Flags: e.g. --no-incremental, --help, -v, /s, /t
    private static readonly Regex CliFlagRegex = new(
        @"(?<=\s|^)(?:--[a-zA-Z0-9_\-]+|-[a-zA-Z0-9]|/[a-zA-Z0-9])(?=\s|$)",
        RegexOptions.Compiled);

    // 5. Programming Languages, Frameworks & Libraries
    private static readonly Regex LangFrameworkRegex = new(
        @"(?:\bC#\b|\bC\+\+\b|\.NET(?:\s+[0-9]+)?\b|\bPython\b|\bTypeScript\b|\bJavaScript\b|\bRust\b|\bGolang\b|\bReact(?:JS)?\b|\bFlutter\b|\bWinUI(?:\s*3)?\b|\bWPF\b|\bWin32\b|\bFastAPI\b|\bNext\.js\b|\bNode\.js\b|\bWebSocket\b|\bWebSockets\b|\bGitHub\b|\bDocker\b|\bnpm\b|\bKubernetes\b|\bFastify\b|\bGraphQL\b|\bRedis\b|\bPostgreSQL\b|\bMongoDB\b|\bDirectML\b|\bONNX\b|\bOpenAI\b|\bgRPC\b|\bASP\.NET(?:\s+Core)?\b|\bWASAPI\b|\bSQLite\b|\bAntigravity\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 6. File Names with recognized developer extensions
    private static readonly Regex FileNameRegex = new(
        @"\b[a-zA-Z0-9_\-]+\.(?:cs|csproj|json|yaml|yml|ts|tsx|js|jsx|py|rs|go|cpp|h|hpp|html|css|sql|toml|sln|md|env|sh|ps1|proto|wasm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 7. Code identifiers with optional Indic postposition/case suffixes (Tamil \u0B80-\u0BFF, Devanagari \u0900-\u097F)
    private static readonly Regex CodeSwitchedIndicSuffixRegex = new(
        @"\b[A-Za-z0-9_]+(?=-?[\u0900-\u097F\u0B80-\u0BFF]+)",
        RegexOptions.Compiled);

    // 8. Code identifiers: dotted symbols (Flow.Core), camelCase, PascalCase, snake_case, SCREAMING_SNAKE, kebab-case
    private static readonly Regex CodeIdentifierRegex = new(
        @"\b[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_.]*\b|\b[a-z]+[A-Z][A-Za-z0-9]*\b|\b[A-Z][a-z0-9]+(?:[A-Z][a-z0-9]*)+\b|\b[a-z0-9]+_[a-z0-9_]+\b|\b[A-Z0-9]+_[A-Z0-9_]+\b|\b[a-z0-9]+(?:-[a-z0-9]+)+\b",
        RegexOptions.Compiled);

    // 9. Common Technical Acronyms
    private static readonly Regex AcronymRegex = new(
        @"\b(HTTP|HTTPS|JSON|API|ASR|WASAPI|VAD|UIA|CLI|GPU|CPU|AVX2|DPAPI|REST|SDK|URL|RAM|WAV|PCM|HTML|CSS|SQL|XML|UUID|GUID|HWND|PID|IDE|SSH|SSL|TLS|DNS|TCP|UDP|JWT|FIFO|LRU)\b",
        RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        int tokenCounter = context.ProtectedTokens.Count;

        string Mask(Match m)
        {
            string original = m.Value;
            string placeholder = $"\uE000{tokenCounter++}\uE001";
            context.ProtectedTokens[placeholder] = original;
            return placeholder;
        }

        // Apply in priority order: URLs -> File Paths -> CLI Commands -> CLI Flags -> Languages/Frameworks -> File Names -> Indic Code-Switched -> Code Identifiers -> Acronyms
        string protectedText = UrlRegex.Replace(text, Mask);
        protectedText = FilePathRegex.Replace(protectedText, Mask);
        protectedText = CliCommandRegex.Replace(protectedText, Mask);
        protectedText = CliFlagRegex.Replace(protectedText, Mask);
        protectedText = LangFrameworkRegex.Replace(protectedText, Mask);
        protectedText = FileNameRegex.Replace(protectedText, Mask);
        protectedText = CodeSwitchedIndicSuffixRegex.Replace(protectedText, Mask);
        protectedText = CodeIdentifierRegex.Replace(protectedText, Mask);
        protectedText = AcronymRegex.Replace(protectedText, Mask);

        return protectedText;
    }
}
