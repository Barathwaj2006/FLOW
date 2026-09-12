using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Scratchpad;

/// <summary>
/// Handles local file exports for Scratchpad entries in Plain Text, Markdown, or JSON.
/// Operates strictly locally with zero external execution or network requests.
/// </summary>
public sealed class ScratchpadExportService : IScratchpadExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<string> ExportAsync(
        ScratchpadEntry entry,
        string destinationFilePath,
        ScratchpadExportFormat format,
        bool overwrite = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(destinationFilePath))
        {
            throw new ArgumentException("Destination file path must be specified.", nameof(destinationFilePath));
        }

        string? dir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(destinationFilePath) && !overwrite)
        {
            throw new IOException($"Target file already exists and overwrite is false: '{destinationFilePath}'");
        }

        string contentToSave = format switch
        {
            ScratchpadExportFormat.PlainText => FormatAsPlainText(entry),
            ScratchpadExportFormat.Markdown => FormatAsMarkdown(entry),
            ScratchpadExportFormat.Json => JsonSerializer.Serialize(entry, JsonOptions),
            _ => FormatAsPlainText(entry)
        };

        await File.WriteAllTextAsync(destinationFilePath, contentToSave, Encoding.UTF8, ct);
        return destinationFilePath;
    }

    public async Task<IReadOnlyList<string>> ExportAllAsync(
        IReadOnlyList<ScratchpadEntry> entries,
        string destinationDirectory,
        ScratchpadExportFormat format,
        bool overwrite = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (string.IsNullOrWhiteSpace(destinationDirectory))
        {
            throw new ArgumentException("Destination directory must be specified.", nameof(destinationDirectory));
        }

        Directory.CreateDirectory(destinationDirectory);
        var exportedFiles = new List<string>();

        string ext = format switch
        {
            ScratchpadExportFormat.PlainText => ".txt",
            ScratchpadExportFormat.Markdown => ".md",
            ScratchpadExportFormat.Json => ".json",
            _ => ".txt"
        };

        foreach (var entry in entries)
        {
            string safeTitle = SanitizeFileName(entry.Title);
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "scratchpad";
            string fileName = $"{safeTitle}_{entry.Id[..8]}{ext}";
            string fullPath = Path.Combine(destinationDirectory, fileName);

            await ExportAsync(entry, fullPath, format, overwrite, ct);
            exportedFiles.Add(fullPath);
        }

        return exportedFiles;
    }

    private static string FormatAsPlainText(ScratchpadEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine(entry.Title);
        sb.AppendLine(new string('-', Math.Max(20, entry.Title.Length)));
        sb.AppendLine();
        sb.Append(entry.Content);
        return sb.ToString();
    }

    private static string FormatAsMarkdown(ScratchpadEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {entry.Title}");
        sb.AppendLine();
        sb.Append(entry.Content);
        return sb.ToString();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char ch in name)
        {
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }
        return sb.ToString().Trim();
    }
}

/// <summary>
/// Primary domain coordinator for Scratchpad operations.
/// </summary>
public sealed class ScratchpadService : IScratchpadService
{
    private readonly IScratchpadRepository _repository;
    private readonly IScratchpadSearchService _search;
    private readonly IScratchpadExportService _export;

    public IScratchpadRepository Repository => _repository;
    public IScratchpadSearchService Search => _search;
    public IScratchpadExportService Export => _export;

    public ScratchpadService(
        IScratchpadRepository repository,
        IScratchpadSearchService? search = null,
        IScratchpadExportService? export = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _search = search ?? (repository as IScratchpadSearchService ?? throw new ArgumentNullException(nameof(search)));
        _export = export ?? new ScratchpadExportService();
    }

    public async Task<ScratchpadEntry> CreateScratchpadAsync(
        string title,
        string content,
        bool isPinned = false,
        CancellationToken ct = default)
    {
        var entry = ScratchpadEntry.Create(title, content, isPinned);
        await _repository.InsertAsync(entry, ct);
        return entry;
    }

    public async Task<ScratchpadEntry?> GetScratchpadAsync(string id, CancellationToken ct = default)
    {
        return await _repository.GetByIdAsync(id, ct);
    }

    public async Task<ScratchpadEntry?> UpdateScratchpadAsync(
        string id,
        string? title,
        string content,
        CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, ct);
        if (existing == null) return null;

        var updated = existing.WithUpdatedContent(title, content);
        bool success = await _repository.UpdateAsync(updated, ct);
        return success ? updated : null;
    }

    public async Task<bool> PinScratchpadAsync(string id, CancellationToken ct = default)
    {
        return await _repository.SetPinnedAsync(id, isPinned: true, ct);
    }

    public async Task<bool> UnpinScratchpadAsync(string id, CancellationToken ct = default)
    {
        return await _repository.SetPinnedAsync(id, isPinned: false, ct);
    }

    public async Task<bool> DeleteScratchpadAsync(string id, CancellationToken ct = default)
    {
        return await _repository.SoftDeleteAsync(id, ct);
    }

    public async Task<bool> RestoreScratchpadAsync(string id, CancellationToken ct = default)
    {
        return await _repository.RestoreAsync(id, ct);
    }

    public async Task<ScratchpadPage> ListScratchpadsAsync(
        ScratchpadFilter filter,
        int pageIndex = 0,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return await _repository.GetPagedAsync(filter, pageIndex, pageSize, ct);
    }

    public async Task<ScratchpadPage> SearchScratchpadsAsync(
        string query,
        ScratchpadFilter? filter = null,
        int pageIndex = 0,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        return await _search.SearchAsync(query, filter, pageIndex, pageSize, ct);
    }

    public async Task<IReadOnlyList<ScratchpadEntry>> GetRecentScratchpadsAsync(int count = 20, CancellationToken ct = default)
    {
        var filter = new ScratchpadFilter(
            IsDeleted: false,
            SortBy: ScratchpadSortOrder.UpdatedAtDesc
        );
        var page = await _repository.GetPagedAsync(filter, pageIndex: 0, pageSize: Math.Clamp(count, 1, 100), ct);
        return page.Items;
    }

    public async Task<IReadOnlyList<ScratchpadEntry>> GetPinnedScratchpadsAsync(CancellationToken ct = default)
    {
        var filter = new ScratchpadFilter(
            IsPinned: true,
            IsDeleted: false,
            SortBy: ScratchpadSortOrder.UpdatedAtDesc
        );
        var page = await _repository.GetPagedAsync(filter, pageIndex: 0, pageSize: 100, ct);
        return page.Items;
    }

    public async Task<string> ExportScratchpadAsync(
        string id,
        string destinationFilePath,
        ScratchpadExportFormat format,
        bool overwrite = false,
        CancellationToken ct = default)
    {
        var entry = await _repository.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Scratchpad with ID '{id}' was not found.");

        return await _export.ExportAsync(entry, destinationFilePath, format, overwrite, ct);
    }
}
