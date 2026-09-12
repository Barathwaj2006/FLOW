using System;
using System.Text.RegularExpressions;
using Flow.Core.Language;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage for detecting and executing explicit spoken casing commands (WF-032B).
/// Recognizes triggers like "camel case <words>", "snake case <words>", "pascal case <words>",
/// "kebab case <words>", and "screaming snake case <words>" and deterministically transforms
/// only the target phrase into programming language identifiers without modifying surrounding text.
/// </summary>
public sealed class SpokenCasingStage : ITranscriptStage
{
    private static readonly Regex SpokenCasingRegex = new(
        @"(?<!\b(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its|studied|analyzed|discussed|mentioned|learned|every|some|any|all|use|uses|using|prefer|prefers|preferring|support|supports|into|in)\s+)" +
        @"\b(?<trigger>(?:camel|snake|pascal|kebab|constant|screaming\s+snake|screaming-snake|upper\s+snake|upper-snake)[-\s]+case)\b\s+" +
        @"(?!(?:is|are|was|were|will|should|would|can|could|may|might|must|refers|means|represents|indicates|describes|used|when|where|while|because|although|since|if|unless|until|in|of|for|about|at|by|from|with|between|into|through|during|before|after|above|below|to|upon|yesterday|today|tomorrow|now|always|never|often|sometimes|formatting|style)\b)" +
        @"(?<phrase>[a-zA-Z0-9_\-]+(?:\s+(?!(?:camel|snake|pascal|kebab|constant|screaming|upper)[-\s]+case\b|\b(?:and|then|with|or|which|return|is|was|are|were|karo|karlo|kijie|karein|pannunga|pannu|seiyunga|podu|hai|irukku)\b|\b(?:to|in|of|for|at|by|from|into|when|where)\s+(?:the|a|an|this|that|these|those|my|your|our|their|his|her|its)\b)[a-zA-Z0-9_\-]+){0,6})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return SpokenCasingRegex.Replace(text, match =>
        {
            string trigger = match.Groups["trigger"].Value.ToLowerInvariant().Replace(" ", "").Replace("-", "");
            string phrase = match.Groups["phrase"].Value.Trim();

            // Do not alter if empty
            if (string.IsNullOrEmpty(phrase))
            {
                return match.Value;
            }

            // Technical token guard: If phrase contains protected entity tokens, preserve them
            if (phrase.Contains('\uE000') || phrase.Contains("__TECH_ENT_"))
            {
                return match.Value;
            }

            return trigger switch
            {
                "camelcase" => CasingTransformer.ToCamelCase(phrase),
                "snakecase" => CasingTransformer.ToSnakeCase(phrase),
                "pascalcase" => CasingTransformer.ToPascalCase(phrase),
                "kebabcase" => CasingTransformer.ToKebabCase(phrase),
                "constantcase" => CasingTransformer.ToConstantCase(phrase),
                "screamingsnakecase" => CasingTransformer.ToConstantCase(phrase),
                "uppersnakecase" => CasingTransformer.ToConstantCase(phrase),
                _ => match.Value
            };
        });
    }
}
