using System;
using System.Text.RegularExpressions;
using Flow.Core.Context;
using Flow.Core.Developer;
using Flow.Core.Language;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage for structured developer syntax recognition (WF-032, WF-033, WF-035).
/// Leverages DeveloperLanguageProfile and DeveloperConfidenceModel to deterministically format
/// functions, classes, interfaces, and code punctuation in code contexts without corrupting prose.
/// </summary>
public sealed class DeveloperSyntaxStage : ITranscriptStage
{
    private static readonly Regex FunctionRegex = new(
        @"(?<!\b(?:a|an|the|this|that|these|those|my|your|our|their|his|her|its|studied|analyzed|every|what|which|is|was|each|some|one|new|old|such|same)\s+)" +
        @"\b(?<prefix>async\s+)?(?:function|method)\s+(?<name>[a-zA-Z0-9_\-]+(?:\s+(?!(?:and|then|with|or|which|return|called|named|git|dotnet|npm|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|\b(?:class|interface|struct|record|enum)\s+(?!(?:karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)[a-zA-Z]|\bto\s+(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its)\b)[a-zA-Z0-9_\-]+){0,5})(?:\s+(?<async>async))?" +
        @"(?=\s*[,;:\.\?!]|\s*[\uE000\uE001]|\s+[\u0B80-\u0BFF\u0900-\u097F]|\s+(?:and|then|with|or|which|return|called|named|git|dotnet|npm|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|\s+(?:class|interface|struct|record|enum)\s+(?!(?:karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)[a-zA-Z]|\bto\s+(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its)\b|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex ClassRegex = new(
        @"(?<!\b(?:a|an|the|this|that|these|those|my|your|our|their|his|her|its|first|world|middle|working|upper|in|during|attend|attending|taking|every|each|some|new|old|same)\s+)" +
        @"\b(?:class|struct|record|enum)\s+(?!(?:fat\s+arrow|thin\s+arrow|arrow|equals|dot|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)(?<name>[a-zA-Z0-9_\-]+(?:\s+(?!(?:and|then|with|or|which|return|called|named|implements|extends|function|method|git|dotnet|npm|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|\b(?:class|interface|struct|record|enum)\s+(?!(?:karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)[a-zA-Z]|\bto\s+(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its)\b)[a-zA-Z0-9_\-]+){0,5})" +
        @"(?=\s*[,;:\.\?!]|\s*[\uE000\uE001]|\s+[\u0B80-\u0BFF\u0900-\u097F]|\s+(?:and|then|with|or|which|return|called|named|implements|extends|function|method|git|dotnet|npm|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|\s+(?:class|interface|struct|record|enum)\s+(?!(?:karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)[a-zA-Z]|\bto\s+(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its)\b|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    private static readonly Regex InterfaceRegex = new(
        @"(?<!\b(?:a|an|the|this|that|these|those|my|your|our|their|his|her|its|user|graphic|hardware|audio|network|desktop|graphical|voice|web|system|between|every|each|some|new|old)\s+)" +
        @"\b(?:interface)\s+(?!(?:karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)(?<name>[a-zA-Z0-9_\-]+(?:\s+(?!(?:and|then|with|or|which|return|called|named|implements|extends|function|method|git|dotnet|npm|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|\b(?:class|interface|struct|record|enum)\s+(?!(?:karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b)[a-zA-Z]|\bto\s+(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its)\b)[a-zA-Z0-9_\-]+){0,5})" +
        @"(?=\s*[,;:\.\?!]|\s*[\uE000\uE001]|\s+[\u0B80-\u0BFF\u0900-\u097F]|\s+(?:and|then|with|or|which|to|return|called|named|implements|extends|function|method|interface|class|git|dotnet|npm|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    // Operator regexes
    private static readonly Regex ThinArrowRegex = new(@"\s*\bthin\s+arrow\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FatArrowRegex = new(@"(?<!thin\s+)\s*\bfat\s+arrow\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ExplicitArrowRegex = new(@"(?<!\b(?:the|a|an|this|that|red|green|blue)\s+)\s*\barrow\b\s*(?!\s*(?:points|pointing|pointed|upward|downward|left|right)\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DoubleEqualsRegex = new(@"\s*\bdouble\s+equals\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NotEqualsRegex = new(@"\s*\bnot\s+equals\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GreaterThanOrEqualRegex = new(@"\s*\bgreater\s+than\s+(?:or\s+equal(?:\s+to)?|equals)\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LessThanOrEqualRegex = new(@"\s*\bless\s+than\s+(?:or\s+equal(?:\s+to)?|equals)\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GreaterThanRegex = new(@"(?<=[a-zA-Z0-9_\)\]])\s+\bgreater\s+than\b\s+(?=[a-zA-Z0-9_\(\[])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LessThanRegex = new(@"(?<=[a-zA-Z0-9_\)\]])\s+\bless\s+than\b\s+(?=[a-zA-Z0-9_\(\[])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ScopeResolutionRegex = new(@"\s*\b(?:scope\s+resolution|double\s+colon)\b\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex BacktickWordRegex = new(@"\bbacktick\s*([^`]+?)\s*backtick\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex StandaloneBacktickRegex = new(@"\bbacktick\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EqualsCodeRegex = new(@"(?<!\b(?:the|this|that|which|constant|variable)\s+[a-zA-Z0-9_]+\s+)(?<!\b(?:constant|variable)\s*)(?<=[a-zA-Z0-9_\)])\s+\b(?:equals|equal\s+sign)\b\s+(?=[a-zA-Z0-9_\(""])", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DotCodeRegex = new(@"(?<!\b(?:a|an|the|this|that)\s+)(?<=[a-zA-Z0-9_])\s+dot\s+(?=[a-zA-Z0-9_])(?!\s+(?:after|at|in|on|of|before|over|under)\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex UnderscoreCodeRegex = new(@"(?<!\b(?:a|an|the|this|that)\s+)(?<=[a-zA-Z0-9])\s+underscore\s+(?=[a-zA-Z0-9])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        bool isCodeEditor = context.DeveloperContext.IsCodeEditor || context.Options.Category == ApplicationCategory.Code;
        var profile = LanguageProfileCatalog.Resolve(context.DeveloperContext.FileContext, context.DeveloperContext.Language?.Code);

        string result = text;

        // 1. Function recognition
        result = FunctionRegex.Replace(result, match =>
        {
            string rawName = match.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(rawName) || IsProsePhrase(rawName))
            {
                return match.Value;
            }

            if (rawName.Contains('\uE000') || rawName.Contains("__TECH_ENT_"))
            {
                return match.Value;
            }

            var (shouldTransform, _) = DeveloperConfidenceModel.Evaluate(
                hasExplicitTrigger: true,
                isCodeContext: isCodeEditor,
                isKnownSyntaxPhrase: true,
                hasProseSuppression: false,
                isAmbiguous: false
            );

            if (!shouldTransform) return match.Value;

            bool hasAsyncPrefix = match.Groups["prefix"].Success;
            bool hasAsyncSuffix = match.Groups["async"].Success || rawName.EndsWith("async", StringComparison.OrdinalIgnoreCase);

            string formatted;
            // If explicit C# file context is active, methods are PascalCase; otherwise default camelCase
            if (profile == LanguageProfileCatalog.CSharp && !string.IsNullOrWhiteSpace(context.DeveloperContext.FileContext))
            {
                formatted = CasingTransformer.ToPascalCase(rawName);
            }
            else
            {
                formatted = CasingTransformer.ToCamelCase(rawName);
            }

            if (hasAsyncPrefix)
            {
                return "async " + formatted;
            }

            if (hasAsyncSuffix && !formatted.EndsWith("Async", StringComparison.OrdinalIgnoreCase) && !formatted.EndsWith("async", StringComparison.OrdinalIgnoreCase))
            {
                formatted += "Async";
            }

            return formatted;
        });

        // 2. Class recognition
        result = ClassRegex.Replace(result, match =>
        {
            string rawName = match.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(rawName) || IsProsePhrase(rawName))
            {
                return match.Value;
            }

            if (rawName.Contains('\uE000') || rawName.Contains("__TECH_ENT_"))
            {
                return match.Value;
            }

            return CasingTransformer.ToPascalCase(rawName);
        });

        // 3. Interface recognition: "interface user repository" -> IUserRepository
        result = InterfaceRegex.Replace(result, match =>
        {
            string rawName = match.Groups["name"].Value.Trim();
            if (string.IsNullOrEmpty(rawName) || IsProsePhrase(rawName))
            {
                return match.Value;
            }

            if (rawName.Contains('\uE000') || rawName.Contains("__TECH_ENT_"))
            {
                return match.Value;
            }

            if (rawName.StartsWith("i ", StringComparison.OrdinalIgnoreCase))
            {
                rawName = rawName[2..].Trim();
            }

            string pascal = CasingTransformer.ToPascalCase(rawName);
            if (!pascal.StartsWith("I") || (pascal.Length > 1 && !char.IsUpper(pascal[1])))
            {
                pascal = "I" + pascal;
            }

            return pascal;
        });

        // 4. Code punctuation
        if (isCodeEditor)
        {
            result = ThinArrowRegex.Replace(result, " -> ");
            result = FatArrowRegex.Replace(result, " => ");
            result = ExplicitArrowRegex.Replace(result, " -> ");
            result = DoubleEqualsRegex.Replace(result, " == ");
            result = NotEqualsRegex.Replace(result, " != ");
            result = GreaterThanOrEqualRegex.Replace(result, " >= ");
            result = LessThanOrEqualRegex.Replace(result, " <= ");
            result = GreaterThanRegex.Replace(result, " > ");
            result = LessThanRegex.Replace(result, " < ");
            result = ScopeResolutionRegex.Replace(result, "::");
            result = Regex.Replace(result, @"::\s*(unique|shared|weak)\s+ptr\b", "::$1_ptr", RegexOptions.IgnoreCase);
            result = BacktickWordRegex.Replace(result, "`$1`");
            result = StandaloneBacktickRegex.Replace(result, "`");
            result = EqualsCodeRegex.Replace(result, " = ");
            result = DotCodeRegex.Replace(result, ".");
            result = UnderscoreCodeRegex.Replace(result, "_");
        }
        else
        {
            result = DoubleEqualsRegex.Replace(result, " == ");
            result = NotEqualsRegex.Replace(result, " != ");
            result = ScopeResolutionRegex.Replace(result, "::");
            result = Regex.Replace(result, @"::\s*(unique|shared|weak)\s+ptr\b", "::$1_ptr", RegexOptions.IgnoreCase);
            result = BacktickWordRegex.Replace(result, "`$1`");
            result = StandaloneBacktickRegex.Replace(result, "`");
        }

        return result;
    }

    private static bool IsProsePhrase(string phrase)
    {
        string lower = phrase.ToLowerInvariant();
        return lower.StartsWith("of ") ||
               lower.StartsWith("used by") ||
               lower.StartsWith("uses ") ||
               lower.StartsWith("is ") ||
               lower.StartsWith("was ") ||
               lower.StartsWith("has ") ||
               lower.StartsWith("had ") ||
               lower.StartsWith("provides ") ||
               lower.StartsWith("enables ") ||
               lower.StartsWith("allows ") ||
               lower.StartsWith("connects ") ||
               lower.StartsWith("contains ") ||
               lower.StartsWith("requires ") ||
               lower.StartsWith("action ") ||
               lower.StartsWith("today ") ||
               lower.StartsWith("yesterday") ||
               lower.StartsWith("starts ") ||
               lower.StartsWith("schedule") ||
               lower.StartsWith("project is") ||
               lower.StartsWith("status is") ||
               lower.StartsWith("between ") ||
               lower.StartsWith("held ") ||
               lower.StartsWith("attended") ||
               lower.StartsWith("students") ||
               lower.StartsWith("flight") ||
               lower.StartsWith("ticket") ||
               lower.StartsWith("room") ||
               lower.StartsWith("was deleted") ||
               lower.Contains("was deleted") ||
               lower.Contains("starts at") ||
               lower.Contains("is pending") ||
               lower.Contains("of this ") ||
               lower.Contains("of the ") ||
               lower.Contains("of my ") ||
               lower.Contains("of our ") ||
               lower.Contains("of a ") ||
               lower.Contains("between the") ||
               lower.Contains("between teams") ||
               lower == "of" ||
               lower == "action" ||
               lower == "room" ||
               lower == "flight" ||
               lower == "meeting" ||
               lower == "lecture";
    }
}
