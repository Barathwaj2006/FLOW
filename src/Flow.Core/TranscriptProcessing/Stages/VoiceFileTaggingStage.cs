using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Flow.Core.Language;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage for detecting explicit spoken file tagging syntax (WF-034).
/// Transforms patterns like "open at app dot ts" -> "open @app.ts",
/// "at user underscore profile dot cs" -> "@user_profile.cs",
/// and "at config dot json" -> "@config.json" for IDE chat and code editors.
/// Strictly protects ordinary prose containing "at".
/// </summary>
public sealed class VoiceFileTaggingStage : ITranscriptStage
{
    private static readonly HashSet<string> RecognizedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "ts", "js", "tsx", "jsx", "cs", "py", "json", "html", "css", "scss", "less",
        "md", "rs", "go", "cpp", "c", "h", "hpp", "xml", "yaml", "yml", "sql",
        "sh", "ps1", "txt", "csv", "toml", "env", "dart", "java", "kt", "swift",
        "rb", "php", "razor", "xaml", "sln", "csproj", "config", "proto", "wasm"
    };

    private static readonly Regex FileTagRegex = new(
        @"(?i)\b(?:at|@)\s+(?<filename>[a-zA-Z0-9_\-\s]+?)\s+(?:dot|\.)\s+(?<ext>[a-zA-Z0-9]{1,10})\b",
        RegexOptions.Compiled
    );

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return FileTagRegex.Replace(text, match =>
        {
            string ext = match.Groups["ext"].Value.ToLowerInvariant();

            // Validate against recognized file extensions
            if (!RecognizedExtensions.Contains(ext))
            {
                return match.Value; // Not a file tag (e.g. "at two dot five")
            }

            string rawFilename = match.Groups["filename"].Value.Trim();

            // Guard against protected technical tokens
            if (rawFilename.Contains("__TECH_ENT_"))
            {
                return match.Value;
            }

            // Transform spoken connectors
            string processed = Regex.Replace(rawFilename, @"(?i)\b(?:underscore)\b", "_");
            processed = Regex.Replace(processed, @"(?i)\b(?:hyphen|dash)\b", "-");
            processed = Regex.Replace(processed, @"(?i)\b(?:slash)\b", "/");
            processed = Regex.Replace(processed, @"(?i)\b(?:backslash)\b", "\\");

            // Clean up whitespace around connectors: e.g. "user _ profile" -> "user_profile"
            processed = Regex.Replace(processed, @"\s*([_\-\/\\])\s*", "$1");

            // If there are still remaining spaces between words (e.g. "my component"), convert to camelCase or compact
            var words = processed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string finalFilename;
            if (words.Length == 1)
            {
                finalFilename = words[0];
            }
            else if (processed.Contains('_') || processed.Contains('-') || processed.Contains('/') || processed.Contains('\\'))
            {
                finalFilename = string.Join("", words);
            }
            else
            {
                // Plain multiple words like "app component" -> "appComponent"
                finalFilename = CasingTransformer.ToCamelCase(processed);
            }

            if (string.IsNullOrEmpty(finalFilename))
            {
                return match.Value;
            }

            return $"@{finalFilename}.{ext}";
        });
    }
}
