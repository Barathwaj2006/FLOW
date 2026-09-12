using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Flow.Core.Language;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage for detecting explicit spoken file tagging syntax and file paths (WF-034).
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
        @"(?i)\b(?:at|@)\s+(?<filename>[a-zA-Z0-9_\-\s\/\\]+?)(?:\s+dot\s+|\s*\.\s*)(?<ext>[a-zA-Z0-9]{1,10})\b",
        RegexOptions.Compiled
    );

    private static readonly Regex SpokenWindowsPathRegex = new(
        @"(?i)\b(?<drive>[a-zA-Z])\s*(?:colon|:)\s*(?:backslash|slash|\/|\\)\s*(?<rest>[a-zA-Z0-9_\-\s\/\\]+)\b",
        RegexOptions.Compiled
    );

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Spoken Windows paths
        string result = SpokenWindowsPathRegex.Replace(text, match =>
        {
            string drive = match.Groups["drive"].Value.ToUpperInvariant();
            string rawRest = match.Groups["rest"].Value.Trim();

            if (rawRest.Contains("__TECH_ENT_")) return match.Value;

            string processed = Regex.Replace(rawRest, @"(?i)\b(?:backslash)\b", "\\");
            processed = Regex.Replace(processed, @"(?i)\b(?:slash)\b", "\\");
            processed = Regex.Replace(processed, @"\s*\\\s*", "\\");

            var segments = processed.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return match.Value;

            var cleanSegments = new List<string>();
            foreach (var seg in segments)
            {
                string trimmed = seg.Trim();
                if (trimmed.Equals("program files", StringComparison.OrdinalIgnoreCase))
                {
                    cleanSegments.Add("Program Files");
                }
                else if (trimmed.Equals("program files (x86)", StringComparison.OrdinalIgnoreCase))
                {
                    cleanSegments.Add("Program Files (x86)");
                }
                else
                {
                    string s = trimmed.Replace(" ", "");
                    if (!string.IsNullOrEmpty(s))
                    {
                        cleanSegments.Add(s);
                    }
                }
            }

            return $"{drive}:\\{string.Join("\\", cleanSegments)}";
        });

        // 2. Spoken file tagging: "at app dot ts" -> "@app.ts", "at src slash flow dot cs" -> "@src/flow.cs"
        result = FileTagRegex.Replace(result, match =>
        {
            string ext = match.Groups["ext"].Value.ToLowerInvariant();

            if (!RecognizedExtensions.Contains(ext))
            {
                return match.Value;
            }

            string rawFilename = match.Groups["filename"].Value.Trim();

            if (rawFilename.Contains("__TECH_ENT_"))
            {
                return match.Value;
            }

            string processed = Regex.Replace(rawFilename, @"(?i)\b(?:underscore)\b", "_");
            processed = Regex.Replace(processed, @"(?i)\b(?:hyphen|dash)\b", "-");
            processed = Regex.Replace(processed, @"(?i)\b(?:slash)\b", "/");
            processed = Regex.Replace(processed, @"(?i)\b(?:backslash)\b", "\\");

            processed = Regex.Replace(processed, @"\s*([_\-\/\\])\s*", "$1");

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
                finalFilename = CasingTransformer.ToCamelCase(processed);
            }

            if (string.IsNullOrEmpty(finalFilename))
            {
                return match.Value;
            }

            return $"@{finalFilename}.{ext}";
        });

        return result;
    }
}
