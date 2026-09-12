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
        @"(?i)\b(?<drive>[a-zA-Z])\s*(?:colon|:)\s*(?:backslash|slash|\/|\\)\s*(?<rest>[a-zA-Z0-9_\-\s\/\\(\)]+)\b",
        RegexOptions.Compiled
    );

    private static readonly Regex SpokenRelativePathRegex = new(
        @"(?i)\b(?:(?<prefix>dot\s+(?:dot\s+)?(?:backslash|slash)|(?:\.|\.\.)\s*\\)\s+)?(?<root>src|tests|test|docs|lib|bin|obj|scripts|packages)\s*(?:backslash|slash|\\)\s*(?<rest>[a-zA-Z0-9_\-\s\/\\(\)]+)\b",
        RegexOptions.Compiled
    );

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // 1. Spoken file tagging: "at app dot ts" -> "@app.ts", "at src slash flow dot cs" -> "@src/flow.cs"
        string result = FileTagRegex.Replace(text, match =>
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

        // 2. Spoken Windows paths
        result = SpokenWindowsPathRegex.Replace(result, match =>
        {
            string drive = match.Groups["drive"].Value.ToUpperInvariant();
            var cleanSegments = CleanSegments(match.Groups["rest"].Value.Trim());
            if (cleanSegments == null || cleanSegments.Count == 0) return match.Value;

            return $"{drive}:\\{string.Join("\\", cleanSegments)}";
        });

        // 3. Spoken relative paths (e.g. "src backslash flow dot core backslash program dot cs", "dot backslash src ...")
        result = SpokenRelativePathRegex.Replace(result, match =>
        {
            string prefix = match.Groups["prefix"].Success
                ? (match.Groups["prefix"].Value.Contains("..", StringComparison.OrdinalIgnoreCase) || match.Groups["prefix"].Value.Contains("dot dot", StringComparison.OrdinalIgnoreCase) ? "..\\" : ".\\")
                : "";
            string root = match.Groups["root"].Value.ToLowerInvariant();
            var cleanSegments = CleanSegments(match.Groups["rest"].Value.Trim());
            if (cleanSegments == null || cleanSegments.Count == 0) return match.Value;

            return $"{prefix}{root}\\{string.Join("\\", cleanSegments)}";
        });

        return result;
    }

    private static List<string>? CleanSegments(string rawRest)
    {
        if (rawRest.Contains("__TECH_ENT_")) return null;

        string processed = Regex.Replace(rawRest, @"(?i)\b(?:backslash)\b", "\\");
        processed = Regex.Replace(processed, @"(?i)\b(?:slash)\b", "\\");
        processed = Regex.Replace(processed, @"\s*\\\s*", "\\");

        var segments = processed.Split(new[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;

        var cleanSegments = new List<string>();
        for (int i = 0; i < segments.Length; i++)
        {
            string seg = segments[i].Trim();
            bool isLastSegment = (i == segments.Length - 1);

            if (seg.Equals("program files", StringComparison.OrdinalIgnoreCase))
            {
                cleanSegments.Add("Program Files");
            }
            else if (seg.Equals("program files (x86)", StringComparison.OrdinalIgnoreCase))
            {
                cleanSegments.Add("Program Files (x86)");
            }
            else if (seg.Equals("users", StringComparison.OrdinalIgnoreCase))
            {
                cleanSegments.Add("Users");
            }
            else if (seg.Equals("visual studio", StringComparison.OrdinalIgnoreCase))
            {
                cleanSegments.Add("visual studio");
            }
            else if (isLastSegment && Regex.IsMatch(seg, @"(?i)\s+(?:dot|\.)\s+[a-zA-Z0-9]+$"))
            {
                var fileMatch = Regex.Match(seg, @"(?i)^(?<name>.+?)\s+(?:dot|\.)\s+(?<ext>[a-zA-Z0-9]+)$");
                if (fileMatch.Success)
                {
                    string name = fileMatch.Groups["name"].Value.Trim();
                    string ext = fileMatch.Groups["ext"].Value.ToLowerInvariant();
                    if (name.Contains(' '))
                    {
                        name = name.Replace(" ", "_");
                    }
                    if (ext == "cs")
                    {
                        name = CasingTransformer.ToPascalCase(name);
                    }
                    cleanSegments.Add($"{name}.{ext}");
                }
                else
                {
                    cleanSegments.Add(seg.Replace(" ", ""));
                }
            }
            else if (Regex.IsMatch(seg, @"(?i)\s+dot\s+"))
            {
                var parts = Regex.Split(seg, @"(?i)\s+dot\s+");
                var cleanParts = new List<string>();
                foreach (var part in parts)
                {
                    string p = part.Trim();
                    if (Regex.IsMatch(p, @"(?i)^v\d+$"))
                    {
                        cleanParts.Add(p.ToLowerInvariant());
                    }
                    else
                    {
                        cleanParts.Add(CasingTransformer.ToPascalCase(p));
                    }
                }
                cleanSegments.Add(string.Join(".", cleanParts));
            }
            else
            {
                string s = Regex.Replace(seg, @"(?i)\b([a-zA-Z0-9]+)\s+dot\s+([a-zA-Z0-9]+)\b", "$1.$2");
                s = s.Replace(" ", "");
                if (!string.IsNullOrEmpty(s))
                {
                    cleanSegments.Add(s);
                }
            }
        }
        return cleanSegments;
    }
}
