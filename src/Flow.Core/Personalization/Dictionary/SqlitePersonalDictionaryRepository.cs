using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Flow.Core.Personalization.Dictionary;

/// <summary>
/// SQLite implementation of IPersonalDictionaryRepository.
/// </summary>
public sealed class SqlitePersonalDictionaryRepository : IPersonalDictionaryRepository
{
    private readonly SqlitePersonalizationDatabase _database;

    public SqlitePersonalDictionaryRepository(SqlitePersonalizationDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<DictionaryEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<DictionaryEntry>();
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt
            FROM DictionaryEntries
            ORDER BY IsStarred DESC, Term ASC;
        ";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadEntry(reader));
        }

        return list;
    }

    public async Task<DictionaryEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt
            FROM DictionaryEntries
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadEntry(reader);
        }

        return null;
    }

    public async Task<DictionaryEntry?> GetByTermAsync(string term, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(term)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt
            FROM DictionaryEntries
            WHERE Term = @term COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@term", term);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadEntry(reader);
        }

        return null;
    }

    public async Task AddAsync(DictionaryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Term))
        {
            throw new ArgumentException("Term cannot be empty.", nameof(entry));
        }

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO DictionaryEntries (Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt)
            VALUES (@id, @term, @replacement, @isStarred, @category, @caseSensitive, @createdAt, @updatedAt);
        ";
        BindEntryParameters(cmd, entry);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(DictionaryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        entry.UpdatedAt = DateTime.UtcNow;

        cmd.CommandText = @"
            UPDATE DictionaryEntries
            SET Term = @term,
                Replacement = @replacement,
                IsStarred = @isStarred,
                Category = @category,
                CaseSensitive = @caseSensitive,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
        ";
        BindEntryParameters(cmd, entry);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM DictionaryEntries WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string> ExportToJsonAsync(CancellationToken ct = default)
    {
        var entries = await GetAllAsync(ct);
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(entries, options);
    }

    public async Task ImportFromJsonAsync(string json, bool overwrite = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var entries = JsonSerializer.Deserialize<List<DictionaryEntry>>(json);
        if (entries == null || entries.Count == 0) return;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Term)) continue;

            var existing = await GetByTermAsync(entry.Term, ct);
            if (existing != null)
            {
                if (overwrite)
                {
                    existing.Replacement = entry.Replacement;
                    existing.IsStarred = entry.IsStarred;
                    existing.Category = entry.Category;
                    existing.CaseSensitive = entry.CaseSensitive;
                    await UpdateAsync(existing, ct);
                }
            }
            else
            {
                await AddAsync(entry, ct);
            }
        }
    }

    public async Task<string> ExportToCsvAsync(CancellationToken ct = default)
    {
        var entries = await GetAllAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Term,Replacement,IsStarred,Category,CaseSensitive");

        foreach (var e in entries)
        {
            string escapeCsv(string? val) =>
                val == null ? "" : $"\"{val.Replace("\"", "\"\"")}\"";

            sb.AppendLine($"{escapeCsv(e.Term)},{escapeCsv(e.Replacement)},{e.IsStarred},{escapeCsv(e.Category)},{e.CaseSensitive}");
        }

        return sb.ToString();
    }

    public async Task ImportFromCsvAsync(string csv, bool overwrite = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;

        using var reader = new StringReader(csv);
        string? header = await reader.ReadLineAsync(ct);
        if (header == null) return;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = ParseCsvLine(line);
            if (parts.Count < 1 || string.IsNullOrWhiteSpace(parts[0])) continue;

            string term = parts[0];
            string? replacement = parts.Count > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
            bool isStarred = parts.Count > 2 && bool.TryParse(parts[2], out bool s) && s;
            string? category = parts.Count > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;
            bool caseSensitive = parts.Count > 4 && bool.TryParse(parts[4], out bool cs) && cs;

            var existing = await GetByTermAsync(term, ct);
            if (existing != null)
            {
                if (overwrite)
                {
                    existing.Replacement = replacement;
                    existing.IsStarred = isStarred;
                    existing.Category = category;
                    existing.CaseSensitive = caseSensitive;
                    await UpdateAsync(existing, ct);
                }
            }
            else
            {
                await AddAsync(new DictionaryEntry
                {
                    Term = term,
                    Replacement = replacement,
                    IsStarred = isStarred,
                    Category = category,
                    CaseSensitive = caseSensitive
                }, ct);
            }
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var current = new StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static DictionaryEntry ReadEntry(SqliteDataReader reader)
    {
        return new DictionaryEntry
        {
            Id = reader.GetString(0),
            Term = reader.GetString(1),
            Replacement = reader.IsDBNull(2) ? null : reader.GetString(2),
            IsStarred = reader.GetInt32(3) == 1,
            Category = reader.IsDBNull(4) ? null : reader.GetString(4),
            CaseSensitive = reader.GetInt32(5) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(6)),
            UpdatedAt = DateTime.Parse(reader.GetString(7))
        };
    }

    private static void BindEntryParameters(SqliteCommand cmd, DictionaryEntry entry)
    {
        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@term", entry.Term);
        cmd.Parameters.AddWithValue("@replacement", (object?)entry.Replacement ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isStarred", entry.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("@category", (object?)entry.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@caseSensitive", entry.CaseSensitive ? 1 : 0);
        cmd.Parameters.AddWithValue("@createdAt", entry.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("O"));
    }
}
