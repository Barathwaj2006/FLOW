using System;
using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Identifies and masks technical tokens (file paths, URLs, code identifiers, CLI commands, acronyms)
/// so that subsequent punctuation, filler-removal, and capitalization stages do not corrupt them.
/// </summary>
public sealed class TechnicalEntityProtectionStage : ITranscriptStage
{
    // 1. URLs and Emails
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s]+|\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 2. Windows File Paths (e.g. C:\Users\... or .\foo\bar)
    private static readonly Regex FilePathRegex = new(
        @"\b[A-Za-z]:\\[A-Za-z0-9._\-\\ ]+|\b(?:\.|\.\.)?\\[A-Za-z0-9._\-\\]+",
        RegexOptions.Compiled);

    // 3. Known CLI and PowerShell commands
    private static readonly Regex CliCommandRegex = new(
        @"\b(dotnet test|dotnet build|dotnet run|npm install|npm run|npm test|git status|git commit|git push|git pull|docker run|docker ps|Get-Process|Remove-Item|Set-Content|Get-Content|Stop-Process|Start-Process)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 4. Code identifiers: dotted symbols (Flow.Core), camelCase, snake_case, SCREAMING_SNAKE
    private static readonly Regex CodeIdentifierRegex = new(
        @"\b[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_.]*\b|\b[a-z]+[A-Z][A-Za-z0-9]*\b|\b[a-z0-9]+_[a-z0-9_]+\b|\b[A-Z0-9]+_[A-Z0-9_]+\b",
        RegexOptions.Compiled);

    // 5. Common Technical Acronyms
    private static readonly Regex AcronymRegex = new(
        @"\b(HTTP|HTTPS|JSON|API|ASR|WASAPI|VAD|UIA|CLI|GPU|CPU|AVX2|DPAPI|REST|SDK|URL|RAM|WAV|PCM)\b",
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

        // Apply in priority order: URLs -> File Paths -> CLI Commands -> Code Identifiers -> Acronyms
        string protectedText = UrlRegex.Replace(text, Mask);
        protectedText = FilePathRegex.Replace(protectedText, Mask);
        protectedText = CliCommandRegex.Replace(protectedText, Mask);
        protectedText = CodeIdentifierRegex.Replace(protectedText, Mask);
        protectedText = AcronymRegex.Replace(protectedText, Mask);

        return protectedText;
    }
}
