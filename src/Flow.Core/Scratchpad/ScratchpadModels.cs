using System;
using System.Collections.Generic;

namespace Flow.Core.Scratchpad;

/// <summary>
/// Sorting options for querying Scratchpad entries.
/// </summary>
public enum ScratchpadSortOrder
{
    PinnedFirstThenUpdated,
    UpdatedAtDesc,
    CreatedAtDesc,
    TitleAsc
}

/// <summary>
/// Supported local export formats for Scratchpad entries.
/// </summary>
public enum ScratchpadExportFormat
{
    PlainText,
    Markdown,
    Json
}

/// <summary>
/// Immutable record representing a persistent Scratchpad entry in local storage.
/// Stored 100% offline in SQLite with full UTC timestamp precision.
/// </summary>
public sealed record ScratchpadEntry(
    string Id,
    string Title,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool IsPinned = false,
    bool IsDeleted = false,
    DateTimeOffset? DeletedAt = null,
    int WordCount = 0,
    int CharacterCount = 0
)
{
    /// <summary>
    /// Creates a new Scratchpad entry with automatically calculated word and character counts.
    /// </summary>
    public static ScratchpadEntry Create(
        string title,
        string content,
        bool isPinned = false,
        string? id = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var effectiveCreatedAt = createdAt ?? now;
        var effectiveUpdatedAt = updatedAt ?? now;

        string normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? DeriveTitleFromContent(content)
            : title.Trim();

        string effectiveContent = content ?? string.Empty;
        int charCount = effectiveContent.Length;
        int wordCount = CalculateWordCount(effectiveContent);

        return new ScratchpadEntry(
            Id: id ?? Guid.NewGuid().ToString("D"),
            Title: normalizedTitle,
            Content: effectiveContent,
            CreatedAt: effectiveCreatedAt,
            UpdatedAt: effectiveUpdatedAt,
            IsPinned: isPinned,
            IsDeleted: false,
            DeletedAt: null,
            WordCount: wordCount,
            CharacterCount: charCount
        );
    }

    /// <summary>
    /// Updates the content and/or title of the Scratchpad entry, recalculating counts and updating the timestamp.
    /// </summary>
    public ScratchpadEntry WithUpdatedContent(string? newTitle, string newContent, DateTimeOffset? updatedAt = null)
    {
        string effectiveContent = newContent ?? string.Empty;
        string effectiveTitle = string.IsNullOrWhiteSpace(newTitle)
            ? (string.IsNullOrWhiteSpace(Title) ? DeriveTitleFromContent(effectiveContent) : Title)
            : newTitle.Trim();

        return this with
        {
            Title = effectiveTitle,
            Content = effectiveContent,
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow,
            WordCount = CalculateWordCount(effectiveContent),
            CharacterCount = effectiveContent.Length
        };
    }

    /// <summary>
    /// Calculates word count using standard whitespace delimiters.
    /// </summary>
    public static int CalculateWordCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Generates a reasonable fallback title from the initial content line.
    /// </summary>
    public static string DeriveTitleFromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "Untitled Scratchpad";
        }

        using var reader = new System.IO.StringReader(content);
        string? firstLine;
        while ((firstLine = reader.ReadLine()) != null)
        {
            firstLine = firstLine.Trim('#', ' ', '\t', '*', '-', '>');
            if (!string.IsNullOrWhiteSpace(firstLine))
            {
                return firstLine.Length <= 60 ? firstLine : firstLine[..57] + "...";
            }
        }

        return "Untitled Scratchpad";
    }
}

/// <summary>
/// Query filter options for retrieving Scratchpad entries.
/// </summary>
public sealed record ScratchpadFilter(
    string? SearchQuery = null,
    bool? IsPinned = null,
    bool IsDeleted = false,
    ScratchpadSortOrder SortBy = ScratchpadSortOrder.PinnedFirstThenUpdated
);

/// <summary>
/// Paged result container for Scratchpad queries.
/// </summary>
public sealed record ScratchpadPage(
    IReadOnlyList<ScratchpadEntry> Items,
    int TotalCount,
    int PageIndex,
    int PageSize
)
{
    public bool HasNextPage => (PageIndex + 1) * PageSize < TotalCount;
    public bool HasPreviousPage => PageIndex > 0;
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
