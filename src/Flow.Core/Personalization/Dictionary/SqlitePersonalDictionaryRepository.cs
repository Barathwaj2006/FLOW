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
            SELECT Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt, IsEnabled, Language, ApplicationScope
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
            SELECT Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt, IsEnabled, Language, ApplicationScope
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
            SELECT Id, Term, Replacement, IsStarred, Category, CaseSensitive, CreatedAt, UpdatedAt, IsEnabled, Language, ApplicationScope
            FROM DictionaryEntries
            WHERE Term = @term COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@term", term.Trim());

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
        if (entry.Term.Length > 500)
        {
            throw new ArgumentException("Term cannot exceed 500 characters.", nameof(entry));
        }
        if (entry.Replacement != null)
        {
            if (string.IsNullOrWhiteSpace(entry.Replacement))
            {
                throw new ArgumentException("Replacement cannot be whitespace-only.", nameof(entry));
            }
            if (entry.Replacement.Length > 1000)
            {
                throw new ArgumentException("Replacement cannot exceed 1000 characters.", nameof(entry));
            }
        }

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO DictionaryEntries (Id, Term, Replacement, IsStarred, Category, CaseSensitive, IsEnabled, Language, ApplicationScope, CreatedAt, UpdatedAt)
            VALUES (@id, @term, @replacement, @isStarred, @category, @caseSensitive, @isEnabled, @language, @applicationScope, @createdAt, @updatedAt)
            ON CONFLICT(Term COLLATE NOCASE) DO UPDATE SET
                Replacement = excluded.Replacement,
                IsStarred = excluded.IsStarred,
                Category = excluded.Category,
                CaseSensitive = excluded.CaseSensitive,
                IsEnabled = excluded.IsEnabled,
                Language = excluded.Language,
                ApplicationScope = excluded.ApplicationScope,
                UpdatedAt = excluded.UpdatedAt;
        ";
        BindEntryParameters(cmd, entry);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(DictionaryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Term))
        {
            throw new ArgumentException("Term cannot be empty.", nameof(entry));
        }
        if (entry.Term.Length > 500)
        {
            throw new ArgumentException("Term cannot exceed 500 characters.", nameof(entry));
        }
        if (entry.Replacement != null && entry.Replacement.Length > 1000)
        {
            throw new ArgumentException("Replacement cannot exceed 1000 characters.", nameof(entry));
        }

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
                IsEnabled = @isEnabled,
                Language = @language,
                ApplicationScope = @applicationScope,
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
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        return JsonSerializer.Serialize(entries, options);
    }

    public async Task ImportFromJsonAsync(string json, bool overwrite = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var entries = JsonSerializer.Deserialize<List<DictionaryEntry>>(json);
        if (entries == null || entries.Count == 0) return;

        await using var conn = _database.CreateConnection();
        await using var tx = conn.BeginTransaction();

        try
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.Term) || entry.Term.Length > 500) continue;
                if (entry.Replacement != null && entry.Replacement.Length > 1000) continue;

                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                if (overwrite)
                {
                    cmd.CommandText = @"
                        INSERT INTO DictionaryEntries (Id, Term, Replacement, IsStarred, Category, CaseSensitive, IsEnabled, Language, ApplicationScope, CreatedAt, UpdatedAt)
                        VALUES (@id, @term, @replacement, @isStarred, @category, @caseSensitive, @isEnabled, @language, @applicationScope, @createdAt, @updatedAt)
                        ON CONFLICT(Term COLLATE NOCASE) DO UPDATE SET
                            Replacement = excluded.Replacement,
                            IsStarred = excluded.IsStarred,
                            Category = excluded.Category,
                            CaseSensitive = excluded.CaseSensitive,
                            IsEnabled = excluded.IsEnabled,
                            Language = excluded.Language,
                            ApplicationScope = excluded.ApplicationScope,
                            UpdatedAt = excluded.UpdatedAt;
                    ";
                }
                else
                {
                    cmd.CommandText = @"
                        INSERT INTO DictionaryEntries (Id, Term, Replacement, IsStarred, Category, CaseSensitive, IsEnabled, Language, ApplicationScope, CreatedAt, UpdatedAt)
                        VALUES (@id, @term, @replacement, @isStarred, @category, @caseSensitive, @isEnabled, @language, @applicationScope, @createdAt, @updatedAt)
                        ON CONFLICT(Term COLLATE NOCASE) DO NOTHING;
                    ";
                }

                BindEntryParameters(cmd, entry);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<string> ExportToCsvAsync(CancellationToken ct = default)
    {
        var entries = await GetAllAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Term,Replacement,IsStarred,Category,CaseSensitive,IsEnabled,Language,ApplicationScope");

        foreach (var e in entries)
        {
            string escapeCsv(string? val) =>
                val == null ? "" : $"\"{val.Replace("\"", "\"\"")}\"";

            sb.AppendLine($"{escapeCsv(e.Term)},{escapeCsv(e.Replacement)},{e.IsStarred},{escapeCsv(e.Category)},{e.CaseSensitive},{e.IsEnabled},{escapeCsv(e.Language)},{escapeCsv(e.ApplicationScope)}");
        }

        return sb.ToString();
    }

    public async Task ImportFromCsvAsync(string csv, bool overwrite = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(csv)) return;

        using var reader = new StringReader(csv);
        string? header = await reader.ReadLineAsync(ct);
        if (header == null) return;

        var headerParts = ParseCsvLine(header);
        if (headerParts.Count < 2 || !headerParts[0].Contains("Term", StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Malformed CSV: Expected at least 'Term' and 'Replacement' columns in header.");
        }

        await using var conn = _database.CreateConnection();
        await using var tx = conn.BeginTransaction();

        try
        {
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = ParseCsvLine(line);
                if (parts.Count < 1 || string.IsNullOrWhiteSpace(parts[0])) continue;

                string term = parts[0].Trim();
                if (term.Length > 500) continue;
                string? replacement = parts.Count > 1 && !string.IsNullOrWhiteSpace(parts[1]) ? parts[1] : null;
                if (replacement != null && replacement.Length > 1000) continue;
                bool isStarred = parts.Count > 2 && bool.TryParse(parts[2], out bool s) && s;
                string? category = parts.Count > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3] : null;
                bool caseSensitive = parts.Count > 4 && bool.TryParse(parts[4], out bool cs) && cs;
                bool isEnabled = parts.Count <= 5 || !bool.TryParse(parts[5], out bool en) || en;
                string? language = parts.Count > 6 && !string.IsNullOrWhiteSpace(parts[6]) ? parts[6] : null;
                string? appScope = parts.Count > 7 && !string.IsNullOrWhiteSpace(parts[7]) ? parts[7] : null;

                var entry = new DictionaryEntry
                {
                    Term = term,
                    Replacement = replacement,
                    IsStarred = isStarred,
                    Category = category,
                    CaseSensitive = caseSensitive,
                    IsEnabled = isEnabled,
                    Language = language,
                    ApplicationScope = appScope
                };

                await using var cmd = conn.CreateCommand();
                cmd.Transaction = tx;

                if (overwrite)
                {
                    cmd.CommandText = @"
                        INSERT INTO DictionaryEntries (Id, Term, Replacement, IsStarred, Category, CaseSensitive, IsEnabled, Language, ApplicationScope, CreatedAt, UpdatedAt)
                        VALUES (@id, @term, @replacement, @isStarred, @category, @caseSensitive, @isEnabled, @language, @applicationScope, @createdAt, @updatedAt)
                        ON CONFLICT(Term COLLATE NOCASE) DO UPDATE SET
                            Replacement = excluded.Replacement,
                            IsStarred = excluded.IsStarred,
                            Category = excluded.Category,
                            CaseSensitive = excluded.CaseSensitive,
                            IsEnabled = excluded.IsEnabled,
                            Language = excluded.Language,
                            ApplicationScope = excluded.ApplicationScope,
                            UpdatedAt = excluded.UpdatedAt;
                    ";
                }
                else
                {
                    cmd.CommandText = @"
                        INSERT INTO DictionaryEntries (Id, Term, Replacement, IsStarred, Category, CaseSensitive, IsEnabled, Language, ApplicationScope, CreatedAt, UpdatedAt)
                        VALUES (@id, @term, @replacement, @isStarred, @category, @caseSensitive, @isEnabled, @language, @applicationScope, @createdAt, @updatedAt)
                        ON CONFLICT(Term COLLATE NOCASE) DO NOTHING;
                    ";
                }

                BindEntryParameters(cmd, entry);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
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
        var entry = new DictionaryEntry
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

        if (reader.FieldCount > 8 && !reader.IsDBNull(8)) entry.IsEnabled = reader.GetInt32(8) == 1;
        if (reader.FieldCount > 9 && !reader.IsDBNull(9)) entry.Language = reader.GetString(9);
        if (reader.FieldCount > 10 && !reader.IsDBNull(10)) entry.ApplicationScope = reader.GetString(10);

        return entry;
    }

    private static void BindEntryParameters(SqliteCommand cmd, DictionaryEntry entry)
    {
        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@term", entry.Term);
        cmd.Parameters.AddWithValue("@replacement", (object?)entry.Replacement ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isStarred", entry.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("@category", (object?)entry.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@caseSensitive", entry.CaseSensitive ? 1 : 0);
        cmd.Parameters.AddWithValue("@isEnabled", entry.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@language", (object?)entry.Language ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@applicationScope", (object?)entry.ApplicationScope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", entry.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("O"));
    }
}
