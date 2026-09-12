using System;
using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// Conservatively removes disfluent filler words (um, uh, erm, ah, hmm, er, uhm)
/// while strictly preserving natural speech verbs and prepositions like "like" in "I like Python".
/// </summary>
public sealed class ConservativeFillerRemovalStage : ITranscriptStage
{
    // Standard hesitation disfluencies surrounded by commas
    private static readonly Regex FillersWithCommasRegex = new(
        @"(?<=^|\s),\s*\b(um|uh|erm|ah|hmm|er|uhm)\b\s*,?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Standalone pure fillers
    private static readonly Regex PureFillersRegex = new(
        @"\b(um|uh|erm|ah|hmm|er|uhm)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Disfluent "like" pattern: only when used as an explicit pause/interjection e.g. ", like," or surrounded by fillers
    private static readonly Regex DisfluentLikeRegex = new(
        @"(?:,\s*\blike\b\s*,|\b(um|uh|hmm|ah)\s+like\b|\blike\s+(um|uh|hmm|ah)\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Disfluent "you know" pattern: only when used as an explicit interjection e.g. ", you know," or surrounded by fillers
    private static readonly Regex DisfluentYouKnowRegex = new(
        @"(?:,\s*\byou know\b\s*,|\b(um|uh|hmm|ah)\s+you know\b|\byou know\s+(um|uh|hmm|ah)\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MultipleSpacesRegex = new(@"[ \t]+", RegexOptions.Compiled);

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text) || !context.Options.RemoveFillerWords)
        {
            return text;
        }

        string result = text;

        // 1. Remove disfluent "like" and "you know" patterns first
        result = DisfluentLikeRegex.Replace(result, " ");
        result = DisfluentYouKnowRegex.Replace(result, " ");

        // 2. Remove fillers with commas e.g. ", um," -> " "
        result = FillersWithCommasRegex.Replace(result, " ");

        // 3. Remove standalone pure fillers
        result = PureFillersRegex.Replace(result, " ");

        // 4. Clean up any leftover punctuation artifacts like " , " or duplicate commas
        result = result.Replace(" ,", ",").Replace(",,", ",");

        // 5. Normalize whitespace
        return MultipleSpacesRegex.Replace(result, " ").Trim();
    }
}
