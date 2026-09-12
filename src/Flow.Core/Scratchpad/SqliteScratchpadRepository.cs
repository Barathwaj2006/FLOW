using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Flow.Core.Scratchpad;

/// <summary>
/// SQLite-backed persistent repository for FLOW Scratchpads.
/// Runs 100% offline with zero cloud transmission.
/// Leverages SQLite FTS5 for sub-millisecond search across thousands of scratchpads.
/// </summary>
public sealed class SqliteScratchpadRepository : IScratchpadRepository, IScratchpadSearchService
{
    private readonly SqlitePersonalizationDatabase _database;

    public SqliteScratchpadRepository(SqlitePersonalizationDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<string> InsertAsync(ScratchpadEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO Scratchpads (
                Id, Title, Content, CreatedAt, UpdatedAt, IsPinned, IsDeleted, DeletedAt, WordCount, CharacterCount
            ) VALUES (
                @id, @title, @content, @createdAt, @updatedAt, @isPinned, @isDeleted, @deletedAt, @wordCount, @characterCount
            );
        ";

        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@title", entry.Title);
        cmd.Parameters.AddWithValue("@content", entry.Content);
        cmd.Parameters.AddWithValue("@createdAt", entry.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@isPinned", entry.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@isDeleted", entry.IsDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@deletedAt", (object?)entry.DeletedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@wordCount", entry.WordCount);
        cmd.Parameters.AddWithValue("@characterCount", entry.CharacterCount);

        await cmd.ExecuteNonQueryAsync(ct);
        return entry.Id;
    }

    public async Task<ScratchpadEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Title, Content, CreatedAt, UpdatedAt, IsPinned, IsDeleted, DeletedAt, WordCount, CharacterCount
            FROM Scratchpads
            WHERE Id = @id
            LIMIT 1;
        ";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadEntry(reader);
        }

        return null;
    }

    public async Task<bool> UpdateAsync(ScratchpadEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            UPDATE Scratchpads
            SET Title = @title,
                Content = @content,
                UpdatedAt = @updatedAt,
                IsPinned = @isPinned,
                IsDeleted = @isDeleted,
                DeletedAt = @deletedAt,
                WordCount = @wordCount,
                CharacterCount = @characterCount
            WHERE Id = @id;
        ";

        cmd.Parameters.AddWithValue("@id", entry.Id);
        cmd.Parameters.AddWithValue("@title", entry.Title);
        cmd.Parameters.AddWithValue("@content", entry.Content);
        cmd.Parameters.AddWithValue("@updatedAt", entry.UpdatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@isPinned", entry.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@isDeleted", entry.IsDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@deletedAt", (object?)entry.DeletedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@wordCount", entry.WordCount);
        cmd.Parameters.AddWithValue("@characterCount", entry.CharacterCount);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> SetPinnedAsync(string id, bool isPinned, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        string now = DateTimeOffset.UtcNow.ToString("O");
        cmd.CommandText = @"
            UPDATE Scratchpads
            SET IsPinned = @isPinned,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@isPinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@updatedAt", now);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> SoftDeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        string now = DateTimeOffset.UtcNow.ToString("O");
        cmd.CommandText = @"
            UPDATE Scratchpads
            SET IsDeleted = 1,
                DeletedAt = @deletedAt,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@deletedAt", now);
        cmd.Parameters.AddWithValue("@updatedAt", now);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> RestoreAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        string now = DateTimeOffset.UtcNow.ToString("O");
        cmd.CommandText = @"
            UPDATE Scratchpads
            SET IsDeleted = 0,
                DeletedAt = NULL,
                UpdatedAt = @updatedAt
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@updatedAt", now);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> DeletePermanentlyAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM Scratchpads WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<ScratchpadPage> GetPagedAsync(ScratchpadFilter filter, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        filter ??= new ScratchpadFilter();
        int safePageIndex = Math.Max(0, pageIndex);
        int safePageSize = Math.Clamp(pageSize, 1, 500);
        int offset = safePageIndex * safePageSize;

        await using var conn = _database.CreateConnection();

        // 1. Total Count query
        var (whereSql, countParams) = BuildWhereClause(filter);
        int totalCount = 0;
        try
        {
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM Scratchpads {whereSql};";
            foreach (var (k, v) in countParams)
            {
                countCmd.Parameters.AddWithValue(k, v);
            }
            object? scalar = await countCmd.ExecuteScalarAsync(ct);
            totalCount = scalar != null ? Convert.ToInt32(scalar) : 0;
        }
        catch (SqliteException) when (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            // FTS table might be unavailable or syntax error; fallback to LIKE
            var (fallbackWhere, fallbackParams) = BuildWhereClause(filter, forceLikeFallback: true);
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM Scratchpads {fallbackWhere};";
            foreach (var (k, v) in fallbackParams)
            {
                countCmd.Parameters.AddWithValue(k, v);
            }
            object? scalar = await countCmd.ExecuteScalarAsync(ct);
            totalCount = scalar != null ? Convert.ToInt32(scalar) : 0;
            whereSql = fallbackWhere;
            countParams = fallbackParams;
        }

        // 2. Select Items query
        string orderClause = filter.SortBy switch
        {
            ScratchpadSortOrder.PinnedFirstThenUpdated => "ORDER BY IsPinned DESC, UpdatedAt DESC",
            ScratchpadSortOrder.UpdatedAtDesc => "ORDER BY UpdatedAt DESC",
            ScratchpadSortOrder.CreatedAtDesc => "ORDER BY CreatedAt DESC",
            ScratchpadSortOrder.TitleAsc => "ORDER BY Title COLLATE NOCASE ASC",
            _ => "ORDER BY IsPinned DESC, UpdatedAt DESC"
        };

        string querySql = $@"
            SELECT Id, Title, Content, CreatedAt, UpdatedAt, IsPinned, IsDeleted, DeletedAt, WordCount, CharacterCount
            FROM Scratchpads
            {whereSql}
            {orderClause}
            LIMIT @limit OFFSET @offset;
        ";

        var list = new List<ScratchpadEntry>();
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = querySql;
            foreach (var (k, v) in countParams)
            {
                cmd.Parameters.AddWithValue(k, v);
            }
            cmd.Parameters.AddWithValue("@limit", safePageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadEntry(reader));
            }
        }
        catch (SqliteException) when (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            // Fallback to LIKE query on error
            var (fallbackWhere, fallbackParams) = BuildWhereClause(filter, forceLikeFallback: true);
            string fallbackSql = $@"
                SELECT Id, Title, Content, CreatedAt, UpdatedAt, IsPinned, IsDeleted, DeletedAt, WordCount, CharacterCount
                FROM Scratchpads
                {fallbackWhere}
                {orderClause}
                LIMIT @limit OFFSET @offset;
            ";

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = fallbackSql;
            foreach (var (k, v) in fallbackParams)
            {
                cmd.Parameters.AddWithValue(k, v);
            }
            cmd.Parameters.AddWithValue("@limit", safePageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            list.Clear();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                list.Add(ReadEntry(reader));
            }
        }

        return new ScratchpadPage(list, totalCount, safePageIndex, safePageSize);
    }

    public async Task<ScratchpadPage> SearchAsync(string query, ScratchpadFilter? filter = null, int pageIndex = 0, int pageSize = 50, CancellationToken ct = default)
    {
        var effectiveFilter = (filter ?? new ScratchpadFilter()) with { SearchQuery = query };
        return await GetPagedAsync(effectiveFilter, pageIndex, pageSize, ct);
    }

    public async Task<int> GetCountAsync(ScratchpadFilter? filter = null, CancellationToken ct = default)
    {
        filter ??= new ScratchpadFilter();
        await using var conn = _database.CreateConnection();
        var (whereSql, parameters) = BuildWhereClause(filter);

        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM Scratchpads {whereSql};";
            foreach (var (k, v) in parameters)
            {
                cmd.Parameters.AddWithValue(k, v);
            }
            object? result = await cmd.ExecuteScalarAsync(ct);
            return result != null ? Convert.ToInt32(result) : 0;
        }
        catch (SqliteException) when (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            var (fallbackWhere, fallbackParams) = BuildWhereClause(filter, forceLikeFallback: true);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM Scratchpads {fallbackWhere};";
            foreach (var (k, v) in fallbackParams)
            {
                cmd.Parameters.AddWithValue(k, v);
            }
            object? result = await cmd.ExecuteScalarAsync(ct);
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }

    public async Task<IReadOnlyList<ScratchpadEntry>> GetAllForExportAsync(ScratchpadFilter? filter = null, CancellationToken ct = default)
    {
        filter ??= new ScratchpadFilter(IsDeleted: false);
        await using var conn = _database.CreateConnection();
        var (whereSql, parameters) = BuildWhereClause(filter);

        string querySql = $@"
            SELECT Id, Title, Content, CreatedAt, UpdatedAt, IsPinned, IsDeleted, DeletedAt, WordCount, CharacterCount
            FROM Scratchpads
            {whereSql}
            ORDER BY IsPinned DESC, UpdatedAt DESC;
        ";

        var list = new List<ScratchpadEntry>();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = querySql;
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadEntry(reader));
        }

        return list;
    }

    private static (string WhereSql, Dictionary<string, object> Parameters) BuildWhereClause(ScratchpadFilter filter, bool forceLikeFallback = false)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object>();

        clauses.Add("IsDeleted = @isDel");
        parameters["@isDel"] = filter.IsDeleted ? 1 : 0;

        if (filter.IsPinned.HasValue)
        {
            clauses.Add("IsPinned = @isPinned");
            parameters["@isPinned"] = filter.IsPinned.Value ? 1 : 0;
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            string query = filter.SearchQuery.Trim();
            string clean = query.Replace("*", "");
            string? fts = forceLikeFallback ? null : BuildFtsQuery(query);

            if (!string.IsNullOrWhiteSpace(fts))
            {
                clauses.Add("rowid IN (SELECT rowid FROM ScratchpadsFts WHERE ScratchpadsFts MATCH @fts)");
                parameters["@fts"] = fts;
            }
            else
            {
                clauses.Add("(Title LIKE @search OR Content LIKE @search)");
                parameters["@search"] = $"%{clean}%";
            }
        }

        string whereSql = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";
        return (whereSql, parameters);
    }

    private static ScratchpadEntry ReadEntry(SqliteDataReader reader)
    {
        return new ScratchpadEntry(
            Id: reader.GetString(0),
            Title: reader.GetString(1),
            Content: reader.GetString(2),
            CreatedAt: DateTimeOffset.Parse(reader.GetString(3)),
            UpdatedAt: DateTimeOffset.Parse(reader.GetString(4)),
            IsPinned: reader.GetInt32(5) == 1,
            IsDeleted: reader.GetInt32(6) == 1,
            DeletedAt: reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
            WordCount: reader.GetInt32(8),
            CharacterCount: reader.GetInt32(9)
        );
    }

    private static string? BuildFtsQuery(string rawQuery)
    {
        if (string.IsNullOrWhiteSpace(rawQuery)) return null;
        var tokens = new List<string>();
        var sb = new StringBuilder();
        bool hasWildcard = false;

        foreach (char ch in rawQuery)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-')
            {
                sb.Append(ch);
            }
            else if (ch == '*')
            {
                hasWildcard = true;
            }
            else
            {
                if (sb.Length > 0)
                {
                    tokens.Add(hasWildcard ? $"\"{sb}\"*" : $"\"{sb}\"");
                    sb.Clear();
                    hasWildcard = false;
                }
            }
        }

        if (sb.Length > 0)
        {
            tokens.Add(hasWildcard ? $"\"{sb}\"*" : $"\"{sb}\"");
        }

        return tokens.Count > 0 ? string.Join(" AND ", tokens) : null;
    }
}
