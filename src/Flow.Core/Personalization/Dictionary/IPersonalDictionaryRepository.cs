using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Personalization.Dictionary;

/// <summary>
/// Repository interface for persistent storage and retrieval of personal dictionary entries.
/// </summary>
public interface IPersonalDictionaryRepository
{
    Task<IReadOnlyList<DictionaryEntry>> GetAllAsync(CancellationToken ct = default);
    Task<DictionaryEntry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<DictionaryEntry?> GetByTermAsync(string term, CancellationToken ct = default);
    Task AddAsync(DictionaryEntry entry, CancellationToken ct = default);
    Task UpdateAsync(DictionaryEntry entry, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<string> ExportToJsonAsync(CancellationToken ct = default);
    Task ImportFromJsonAsync(string json, bool overwrite = false, CancellationToken ct = default);
    Task<string> ExportToCsvAsync(CancellationToken ct = default);
    Task ImportFromCsvAsync(string csv, bool overwrite = false, CancellationToken ct = default);
}
