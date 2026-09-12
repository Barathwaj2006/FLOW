using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Scratchpad;

/// <summary>
/// Persistence contract for local Scratchpad storage in SQLite.
/// </summary>
public interface IScratchpadRepository
{
    Task<string> InsertAsync(ScratchpadEntry entry, CancellationToken ct = default);
    Task<ScratchpadEntry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<bool> UpdateAsync(ScratchpadEntry entry, CancellationToken ct = default);
    Task<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(string id, CancellationToken ct = default);
    Task<bool> RestoreAsync(string id, CancellationToken ct = default);
    Task<bool> DeletePermanentlyAsync(string id, CancellationToken ct = default);
    Task<ScratchpadPage> GetPagedAsync(ScratchpadFilter filter, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<int> GetCountAsync(ScratchpadFilter? filter = null, CancellationToken ct = default);
    Task<IReadOnlyList<ScratchpadEntry>> GetAllForExportAsync(ScratchpadFilter? filter = null, CancellationToken ct = default);
}

/// <summary>
/// Fast local full-text search across Scratchpad titles and contents using SQLite FTS5.
/// </summary>
public interface IScratchpadSearchService
{
    Task<ScratchpadPage> SearchAsync(string query, ScratchpadFilter? filter = null, int pageIndex = 0, int pageSize = 50, CancellationToken ct = default);
}

/// <summary>
/// Offline file export for Scratchpad entries (Plain Text, Markdown, JSON).
/// </summary>
public interface IScratchpadExportService
{
    Task<string> ExportAsync(ScratchpadEntry entry, string destinationFilePath, ScratchpadExportFormat format, bool overwrite = false, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ExportAllAsync(IReadOnlyList<ScratchpadEntry> entries, string destinationDirectory, ScratchpadExportFormat format, bool overwrite = false, CancellationToken ct = default);
}

/// <summary>
/// Primary domain coordinator for Scratchpad & Quick Capture operations.
/// </summary>
public interface IScratchpadService
{
    IScratchpadRepository Repository { get; }
    IScratchpadSearchService Search { get; }
    IScratchpadExportService Export { get; }

    Task<ScratchpadEntry> CreateScratchpadAsync(string title, string content, bool isPinned = false, CancellationToken ct = default);
    Task<ScratchpadEntry?> GetScratchpadAsync(string id, CancellationToken ct = default);
    Task<ScratchpadEntry?> UpdateScratchpadAsync(string id, string? title, string content, CancellationToken ct = default);
    Task<bool> PinScratchpadAsync(string id, CancellationToken ct = default);
    Task<bool> UnpinScratchpadAsync(string id, CancellationToken ct = default);
    Task<bool> DeleteScratchpadAsync(string id, CancellationToken ct = default);
    Task<bool> RestoreScratchpadAsync(string id, CancellationToken ct = default);
    Task<ScratchpadPage> ListScratchpadsAsync(ScratchpadFilter filter, int pageIndex = 0, int pageSize = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ScratchpadEntry>> GetRecentScratchpadsAsync(int count = 20, CancellationToken ct = default);
    Task<IReadOnlyList<ScratchpadEntry>> GetPinnedScratchpadsAsync(CancellationToken ct = default);
    Task<string> ExportScratchpadAsync(string id, string destinationFilePath, ScratchpadExportFormat format, bool overwrite = false, CancellationToken ct = default);
}
