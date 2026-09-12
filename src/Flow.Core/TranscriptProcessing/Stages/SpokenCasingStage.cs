using System;
using System.Text.RegularExpressions;
using Flow.Core.Language;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Pipeline stage for detecting and executing explicit spoken casing commands (WF-032B).
/// Recognizes triggers like "camel case <words>", "snake case <words>", "pascal case <words>",
/// and "kebab case <words>" and deterministically transforms only the target phrase into
/// programming language identifiers without modifying surrounding text or executing commands.
/// </summary>
public sealed class SpokenCasingStage : ITranscriptStage
{
    private static readonly Regex SpokenCasingRegex = new(
        @"(?<!\b(?:the|a|an|this|that|their|its|studied|analyzed)\s+)\b(?<trigger>camel\s+case|snake\s+case|pascal\s+case|kebab\s+case|constant\s+case)\s+(?!(?:in|of|for|about|at|by|from|with|between)\b)(?<phrase>[a-zA-Z0-9_\-]+(?:\s+(?!(?:camel|snake|pascal|kebab|constant)\s+case\b|\b(?:and|then|with|or|which|to|return)\b)[a-zA-Z0-9_\-]+){0,7})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        return SpokenCasingRegex.Replace(text, match =>
        {
            string trigger = match.Groups["trigger"].Value.ToLowerInvariant().Replace(" ", "");
            string phrase = match.Groups["phrase"].Value.Trim();

            // Do not alter if empty
            if (string.IsNullOrEmpty(phrase))
            {
                return match.Value;
            }

            // Technical token guard: If phrase contains protected entity tokens (e.g. __TECH_ENT_0__), preserve them
            if (phrase.Contains("__TECH_ENT_"))
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
                _ => match.Value
            };
        });
    }
}
