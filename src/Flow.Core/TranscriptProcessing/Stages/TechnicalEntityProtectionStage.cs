using System;
using System.Text.RegularExpressions;
using Flow.Core.Developer.Spans;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Identifies and shields technical tokens (file paths, URLs, code identifiers, CLI commands, acronyms)
/// using structured spans so that subsequent stages do not corrupt them (WF-033).
/// </summary>
public sealed class TechnicalEntityProtectionStage : ITranscriptStage
{
    // 1. URLs and Emails
    private static readonly Regex UrlRegex = new(
        @"(?:https?://|ftp://)[^\s,;!?]+|\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 2. Windows File Paths & Unix Paths (e.g. C:\Users\... or C:\Program Files\... or .\src\... or /usr/local/bin/...)
    private static readonly Regex FilePathRegex = new(
        @"\b[A-Za-z]:\\(?:[A-Za-z0-9._\-]+(?:\s+[A-Za-z0-9._\-]+)*\\)*[A-Za-z0-9._\-]+(?<![.,;:\?!])|" +
        @"(?:\.\.?[\\/]|(?:src|tests|docs|lib|bin|obj|scripts|packages)[\\/])[A-Za-z0-9._\-\\/]+(?<![.,;:\?!])|" +
        @"/(?:usr|home|etc|var|opt|bin|dev|tmp)/[A-Za-z0-9._\-/]+(?<![.,;:\?!])",
        RegexOptions.Compiled);

    // 3. Known CLI commands and PowerShell cmdlets
    private static readonly Regex CliCommandRegex = new(
        @"\b(git (?:status|checkout|commit|push|pull|merge|rebase|log|diff|branch|clone|fetch|init|reset|stash)|" +
        @"dotnet (?:test|build|run|restore|clean|publish|pack|tool|new)|" +
        @"npm (?:install|run|test|start|build|publish|init|update)|" +
        @"pip (?:install|list|freeze|uninstall)|" +
        @"docker (?:run|ps|compose|build|stop|exec|images|pull|rm)|" +
        @"kubectl (?:get|apply|describe|delete|logs|exec)|" +
        @"cargo (?:build|test|run|check)|" +
        @"shutdown\s+/[a-zA-Z0-9\s/]+|" +
        @"Remove-Item|Set-Content|Get-Content|Get-Process|Stop-Process|Start-Process|" +
        @"format\s+[A-Za-z]:|del\s+/[a-zA-Z0-9\s/*]+|powershell|pwsh|cmd\.exe)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 4. CLI Flags: e.g. --no-incremental, --help, --force, -v, /s, /t, -p 8080
    private static readonly Regex CliFlagRegex = new(
        @"(?<=\s|^)(?:--[a-zA-Z0-9_\-]+|-[a-zA-Z0-9]|/[a-zA-Z0-9])(?=[\s,;:\.\?!]|$)",
        RegexOptions.Compiled);

    // 5. Programming Languages, Frameworks & Libraries
    private static readonly Regex LangFrameworkRegex = new(
        @"(?:\bC#\b|\bC\+\+\b|\.NET(?:\s+[0-9]+)?\b|\bASP\.NET(?:\s+Core)?\b|\bWinUI(?:\s*3)?\b|\bWPF\b|\bWin32\b|" +
        @"\bPython\b|\bTypeScript\b|\bJavaScript\b|\bRust\b|\bGolang\b|\bKotlin\b|\bSwift\b|\bJava\b|" +
        @"\bReact(?:JS)?\b|\bFlutter\b|\bFastAPI\b|\bNext\.js\b|\bNode\.js\b|\bWebSocket\b|\bWebSockets\b|" +
        @"\bGitHub\b|\bDocker\b|\bnpm\b|\bKubernetes\b|\bFastify\b|\bGraphQL\b|\bRedis\b|\bPostgreSQL\b|" +
        @"\bMongoDB\b|\bDirectML\b|\bONNX\b|\bOpenAI\b|\bgRPC\b|\bWASAPI\b|\bSQLite\b|\bAntigravity\b|" +
        @"\bWindows UI Automation\b|\bJetBrains Rider\b|\bVisual Studio\b|\bVS Code\b|\bWindsurf\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 6. File Names with recognized developer extensions
    private static readonly Regex FileNameRegex = new(
        @"\b[a-zA-Z0-9_\-]+\.(?:cs|csproj|json|yaml|yml|ts|tsx|js|jsx|py|rs|go|cpp|h|hpp|html|css|scss|less|sql|toml|sln|md|env|sh|ps1|proto|wasm)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // 7. Environment Variables: e.g. %PATH%, $PATH, $env:USERPROFILE
    private static readonly Regex EnvVarRegex = new(
        @"%[A-Za-z0-9_]+%|\$[A-Za-z_][A-Za-z0-9_]*|\$env:[A-Za-z0-9_]+",
        RegexOptions.Compiled);

    // 8. Code identifiers with optional Indic postposition/case suffixes (Tamil \u0B80-\u0BFF, Devanagari \u0900-\u097F)
    private static readonly Regex CodeSwitchedIndicSuffixRegex = new(
        @"\b[A-Za-z0-9_]+(?=-?[\u0900-\u097F\u0B80-\u0BFF]+)",
        RegexOptions.Compiled);

    // 9. Structured Code Identifiers (WF-033):
    //    - Dotted namespaces: Flow.Core.Context
    //    - camelCase: getUserProfile, parseJSONResponse, apiClientV2
    //    - PascalCase: UserProfileService, IUserRepository, Point2D
    //    - Acronym-prefix PascalCase: APIClient, APIClientV2, JSONParser, XMLHttpRequest, HTTPRequestHandler
    //    - Acronym-number identifiers: HTTP2Client, UTF8Parser, IPv6Parser, H264Decoder, V2Endpoint, OAuth2Token
    //    - snake_case & SCREAMING_SNAKE_CASE: user_profile_service, MAX_RETRY_COUNT
    //    - kebab-case: user-auth-service, header-component
    private static readonly Regex CodeIdentifierRegex = new(
        @"\b[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_.]*\b|" +
        @"\b[a-z]+[A-Z0-9][A-Za-z0-9]*\b|" +
        @"\b[A-Z][a-z0-9]+(?:[A-Z0-9][a-z0-9]*)+\b|" +
        @"\b[A-Z]{2,}[a-z0-9]+[A-Za-z0-9]*\b|" +
        @"\b[A-Z]{2,}[0-9]+[A-Za-z0-9]*\b|" +
        @"\b(?:OAuth|OAuth2|IPv4|IPv6|H264|H265|V2|V3|I18N|L10N)[A-Za-z0-9]*\b|" +
        @"\b[a-z0-9]+_[a-z0-9_]+\b|" +
        @"\b[A-Z0-9]+_[A-Z0-9_]+\b|" +
        @"(?!(?:camel|snake|pascal|kebab|constant|screaming-snake|upper-snake)-case\b)\b[a-z0-9]+(?:-[a-z0-9]+)+\b",
        RegexOptions.Compiled);

    // 10. Common Technical Acronyms
    private static readonly Regex AcronymRegex = new(
        @"\b(HTTP|HTTPS|JSON|API|ASR|WASAPI|VAD|UIA|CLI|GPU|CPU|AVX2|DPAPI|REST|SDK|URL|RAM|WAV|PCM|HTML|CSS|SQL|XML|UUID|GUID|HWND|PID|IDE|SSH|SSL|TLS|DNS|TCP|UDP|JWT|FIFO|LRU)\b",
        RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        int tokenCounter = context.ProtectedTokens.Count;

        string Mask(Match m, TechnicalSpanCategory category)
        {
            string original = m.Value;
            string placeholder = $"\uE000{tokenCounter++}\uE001";
            context.ProtectedTokens[placeholder] = original;
            return placeholder;
        }

        string result = text;
        result = UrlRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.URLToken));
        result = FilePathRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.PathToken));
        result = CliCommandRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.FlagToken));
        result = CliFlagRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.FlagToken));
        result = LangFrameworkRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.FrameworkToken));
        result = FileNameRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.PathToken));
        result = EnvVarRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.EnvVarToken));
        result = CodeSwitchedIndicSuffixRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.IdentifierToken));
        result = CodeIdentifierRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.IdentifierToken));
        result = AcronymRegex.Replace(result, m => Mask(m, TechnicalSpanCategory.AcronymToken));

        return result;
    }
}
