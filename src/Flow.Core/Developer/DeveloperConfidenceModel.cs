using System;
using Flow.Core.Context;

namespace Flow.Core.Developer;

/// <summary>
/// Deterministic developer syntax confidence model (no AI / local only).
/// Calculates whether a recognized spoken syntax construction possesses sufficient
/// evidence to be transformed into code or whether it should remain natural prose.
/// 
/// Scoring:
/// - Explicit trigger (e.g. 'camel case', 'screaming snake case'): +3
/// - Developer context (Code editor / Terminal): +2
/// - Known syntax phrase (e.g. 'function calculate tax'): +2
/// - Natural language suppression (determiners, possessives, prose collocations): -4
/// - Ambiguous construction (e.g. isolated 'equals', 'arrow', 'dot'): -3
/// 
/// Threshold: score >= 3 -> Transform; otherwise preserve original text.
/// </summary>
public static class DeveloperConfidenceModel
{
    public const float ActivationThreshold = 3.0f;

    public static (bool ShouldTransform, float Confidence) Evaluate(
        bool hasExplicitTrigger,
        bool isCodeContext,
        bool isKnownSyntaxPhrase,
        bool hasProseSuppression,
        bool isAmbiguous)
    {
        float score = 0.0f;

        if (hasExplicitTrigger) score += 3.0f;
        if (isCodeContext) score += 2.0f;
        if (isKnownSyntaxPhrase) score += 2.0f;
        if (hasProseSuppression) score -= 4.0f;
        if (isAmbiguous) score -= 3.0f;

        bool shouldTransform = score >= ActivationThreshold;
        return (shouldTransform, score);
    }
}
