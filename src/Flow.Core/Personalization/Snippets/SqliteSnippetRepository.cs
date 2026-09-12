using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Flow.Core.Personalization.Snippets;

/// <summary>
/// SQLite implementation of ISnippetRepository.
/// </summary>
public sealed class SqliteSnippetRepository : ISnippetRepository
{
    private readonly SqlitePersonalizationDatabase _database;

    public SqliteSnippetRepository(SqlitePersonalizationDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<SnippetEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var list = new List<SnippetEntry>();
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, TriggerPhrase, ExpansionText, IsEnabled, Category, CreatedAt, UpdatedAt
            FROM Snippets
            ORDER BY TriggerPhrase ASC;
        ";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadSnippet(reader));
        }

        return list;
    }

    public async Task<SnippetEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, TriggerPhrase, ExpansionText, IsEnabled, Category, CreatedAt, UpdatedAt
            FROM Snippets
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadSnippet(reader);
        }

        return null;
    }

    public async Task<SnippetEntry?> GetByTriggerAsync(string trigger, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(trigger)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, TriggerPhrase, ExpansionText, IsEnabled, Category, CreatedAt, UpdatedAt
            FROM Snippets
            WHERE TriggerPhrase = @trigger COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@trigger", trigger.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadSnippet(reader);
        }

        return null;
    }

    public async Task AddAsync(SnippetEntry snippet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        if (string.IsNullOrWhiteSpace(snippet.TriggerPhrase))
        {
            throw new ArgumentException("Trigger phrase cannot be empty.", nameof(snippet));
        }
        if (snippet.ExpansionText.Length > 4000)
        {
            throw new ArgumentException("Expansion text cannot exceed 4,000 characters.", nameof(snippet));
        }

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO Snippets (Id, TriggerPhrase, ExpansionText, IsEnabled, Category, CreatedAt, UpdatedAt)
            VALUES (@id, @triggerPhrase, @expansionText, @isEnabled, @category, @createdAt, @updatedAt);
        ";
        BindSnippetParameters(cmd, snippet);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(SnippetEntry snippet, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(snippet);
        if (string.IsNullOrWhiteSpace(snippet.TriggerPhrase))
        {
            throw new ArgumentException("Trigger phrase cannot be empty.", nameof(snippet));
        }
        if (snippet.ExpansionText.Length > 4000)
        {
            throw new ArgumentException("Expansion text cannot exceed 4,000 characters.", nameof(snippet));
        }

        snippet.UpdatedAt = DateTime.UtcNow;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE Snippets
            SET TriggerPhrase = @triggerPhrase,
                ExpansionText = @expansionText,
                IsEnabled = @isEnabled,
                Category = @category,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
        ";
        BindSnippetParameters(cmd, snippet);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM Snippets WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string> ExportToJsonAsync(CancellationToken ct = default)
    {
        var snippets = await GetAllAsync(ct);
        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.Serialize(snippets, options);
    }

    public async Task ImportFromJsonAsync(string json, bool overwrite = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var snippets = JsonSerializer.Deserialize<List<SnippetEntry>>(json);
        if (snippets == null || snippets.Count == 0) return;

        foreach (var s in snippets)
        {
            if (string.IsNullOrWhiteSpace(s.TriggerPhrase)) continue;

            var existing = await GetByTriggerAsync(s.TriggerPhrase, ct);
            if (existing != null)
            {
                if (overwrite)
                {
                    existing.ExpansionText = s.ExpansionText;
                    existing.IsEnabled = s.IsEnabled;
                    existing.Category = s.Category;
                    await UpdateAsync(existing, ct);
                }
            }
            else
            {
                await AddAsync(s, ct);
            }
        }
    }

    private static SnippetEntry ReadSnippet(SqliteDataReader reader)
    {
        return new SnippetEntry
        {
            Id = reader.GetString(0),
            TriggerPhrase = reader.GetString(1),
            ExpansionText = reader.GetString(2),
            IsEnabled = reader.GetInt32(3) == 1,
            Category = reader.IsDBNull(4) ? null : reader.GetString(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            UpdatedAt = DateTime.Parse(reader.GetString(6))
        };
    }

    private static void BindSnippetParameters(SqliteCommand cmd, SnippetEntry snippet)
    {
        cmd.Parameters.AddWithValue("@id", snippet.Id);
        cmd.Parameters.AddWithValue("@triggerPhrase", snippet.TriggerPhrase.Trim());
        cmd.Parameters.AddWithValue("@expansionText", snippet.ExpansionText);
        cmd.Parameters.AddWithValue("@isEnabled", snippet.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@category", (object?)snippet.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", snippet.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", snippet.UpdatedAt.ToString("O"));
    }
}
