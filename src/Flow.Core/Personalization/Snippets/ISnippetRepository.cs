using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Personalization.Snippets;

/// <summary>
/// Repository interface for persistent storage and retrieval of voice snippets.
/// </summary>
public interface ISnippetRepository
{
    Task<IReadOnlyList<SnippetEntry>> GetAllAsync(CancellationToken ct = default);
    Task<SnippetEntry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<SnippetEntry?> GetByTriggerAsync(string trigger, CancellationToken ct = default);
    Task AddAsync(SnippetEntry snippet, CancellationToken ct = default);
    Task UpdateAsync(SnippetEntry snippet, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<string> ExportToJsonAsync(CancellationToken ct = default);
    Task ImportFromJsonAsync(string json, bool overwrite = false, CancellationToken ct = default);
}
