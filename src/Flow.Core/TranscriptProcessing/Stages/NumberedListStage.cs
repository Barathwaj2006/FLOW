using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Flow.Core.TranscriptProcessing.Stages;

/// <summary>
/// State-driven spoken numbered list recognition stage.
/// Formats sequences like "one buy milk two finish report" or "first check logs second run tests"
/// into "1. Buy milk 2. Finish report".
/// 
/// Employs a strict multi-item verification gate to prevent false positives:
/// A single occurrence of "one" (e.g. "one important thing") will NEVER trigger a numbered list.
/// </summary>
public sealed class NumberedListStage : ITranscriptStage
{
    private static readonly Dictionary<string, int> CardinalNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "one", 1 }, { "two", 2 }, { "three", 3 }, { "four", 4 }, { "five", 5 },
        { "six", 6 }, { "seven", 7 }, { "eight", 8 }, { "nine", 9 }, { "ten", 10 }
    };

    private static readonly Dictionary<string, int> OrdinalNumbers = new(StringComparer.OrdinalIgnoreCase)
    {
        { "first", 1 }, { "second", 2 }, { "third", 3 }, { "fourth", 4 }, { "fifth", 5 },
        { "sixth", 6 }, { "seventh", 7 }, { "eighth", 8 }, { "ninth", 9 }, { "tenth", 10 }
    };

    public string Process(string text, TranscriptProcessingContext context)
    {
        if (string.IsNullOrWhiteSpace(text) || !context.Options.FormatNumberedLists)
        {
            return text;
        }

        // Check for cardinal list first ("one ... two ...")
        if (TryFormatList(text, CardinalNumbers, context, out string formattedCardinal))
        {
            return formattedCardinal;
        }

        // Check for ordinal list ("first ... second ...")
        if (TryFormatList(text, OrdinalNumbers, context, out string formattedOrdinal))
        {
            return formattedOrdinal;
        }

        return text;
    }

    private static bool TryFormatList(
        string text,
        Dictionary<string, int> numberMap,
        TranscriptProcessingContext context,
        out string result)
    {
        result = text;

        // Tokenize by whitespace while tracking original spans
        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3) return false;

        // Find candidate occurrences of item 1 and item 2
        int firstIndex = -1;
        int secondIndex = -1;

        for (int i = 0; i < words.Length; i++)
        {
            string cleanWord = CleanWord(words[i]);
            if (numberMap.TryGetValue(cleanWord, out int num))
            {
                if (num == 1 && firstIndex == -1)
                {
                    firstIndex = i;
                }
                else if (num == 2 && firstIndex != -1 && i > firstIndex)
                {
                    secondIndex = i;
                    break; // Verified list intent!
                }
            }
        }

        // Gate: We MUST have at least Item 1 AND Item 2 to confirm list intent
        if (firstIndex == -1 || secondIndex == -1)
        {
            context.CurrentListState = ListState.NotInList;
            return false;
        }

        // Confirmed list intent: Build formatted string
        context.CurrentListState = ListState.InNumberedList;
        context.CurrentListIndex = 0;

        var sb = new StringBuilder();

        // Any preamble before item 1
        for (int i = 0; i < firstIndex; i++)
        {
            sb.Append(words[i]).Append(' ');
        }

        int expectedNext = 1;
        bool capitalizeNextWord = false;

        for (int i = firstIndex; i < words.Length; i++)
        {
            string cleanWord = CleanWord(words[i]);
            if (numberMap.TryGetValue(cleanWord, out int num) && num == expectedNext)
            {
                if (expectedNext > 1)
                {
                    sb.Append(' ');
                }
                sb.Append(expectedNext).Append(". ");
                context.CurrentListIndex = expectedNext;
                expectedNext++;
                capitalizeNextWord = true;
            }
            else
            {
                string wordToAppend = words[i];
                if (capitalizeNextWord && wordToAppend.Length > 0 && char.IsLower(wordToAppend[0]))
                {
                    wordToAppend = char.ToUpperInvariant(wordToAppend[0]) + wordToAppend.Substring(1);
                    capitalizeNextWord = false;
                }
                else if (capitalizeNextWord && wordToAppend.Length > 0)
                {
                    capitalizeNextWord = false;
                }

                sb.Append(wordToAppend);
                if (i < words.Length - 1)
                {
                    sb.Append(' ');
                }
            }
        }

        result = sb.ToString().TrimEnd();
        return true;
    }

    private static string CleanWord(string word)
    {
        return word.Trim('.', ',', ':', ';', '!', '?').ToLowerInvariant();
    }
}
