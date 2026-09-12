using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Flow.Core.History;

/// <summary>
/// SQLite-backed implementation of IHistoryRepository and IHistorySearchService (WF-039, WF-040).
/// Thread-safe, local-only, parameterized queries, with FTS5 search and indexed fallback.
/// </summary>
public sealed class SqliteHistoryRepository : IHistoryRepository, IHistorySearchService
{
    private readonly SqlitePersonalizationDatabase _database;

    public SqliteHistoryRepository(SqlitePersonalizationDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<string> InsertAsync(DictationEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        string id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id;
        string? textHash = entry.TextHash ?? ComputeHash(entry.Text);

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO DictationHistory (
                Id, SessionId, CreatedAt, DurationMs, CharacterCount, WordCount,
                Language, Application, ApplicationCategory, Mode, State,
                WasEdited, IsFavorite, Text, TextHash, MetadataJson, IsDeleted, DeletedAt
            ) VALUES (
                @id, @sessionId, @createdAt, @durationMs, @charCount, @wordCount,
                @language, @app, @category, @mode, @state,
                @wasEdited, @isFavorite, @text, @textHash, @metadata, @isDeleted, @deletedAt
            );
        ";

        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@sessionId", entry.SessionId.ToString());
        cmd.Parameters.AddWithValue("@createdAt", entry.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@durationMs", entry.DurationMs);
        cmd.Parameters.AddWithValue("@charCount", entry.CharacterCount);
        cmd.Parameters.AddWithValue("@wordCount", entry.WordCount);
        cmd.Parameters.AddWithValue("@language", entry.Language);
        cmd.Parameters.AddWithValue("@app", entry.Application);
        cmd.Parameters.AddWithValue("@category", entry.ApplicationCategory);
        cmd.Parameters.AddWithValue("@mode", entry.Mode);
        cmd.Parameters.AddWithValue("@state", entry.State.ToString());
        cmd.Parameters.AddWithValue("@wasEdited", entry.WasEdited ? 1 : 0);
        cmd.Parameters.AddWithValue("@isFavorite", entry.IsFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@text", (object?)entry.Text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@textHash", (object?)textHash ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@metadata", (object?)entry.MetadataJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isDeleted", entry.IsDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@deletedAt", entry.DeletedAt.HasValue ? entry.DeletedAt.Value.ToString("O") : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
        return id;
    }

    public async Task<DictationEntry?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, SessionId, CreatedAt, DurationMs, CharacterCount, WordCount,
                   Language, Application, ApplicationCategory, Mode, State,
                   WasEdited, IsFavorite, Text, TextHash, MetadataJson, IsDeleted, DeletedAt
            FROM DictationHistory
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

    public async Task<HistoryPage> GetPagedAsync(HistoryFilter filter, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        // Enforce pagination bounds: default 50, maximum 200, page >= 0
        int safePageIndex = Math.Max(0, pageIndex);
        int safePageSize = Math.Clamp(pageSize, 1, 200);
        int offset = safePageIndex * safePageSize;

        await using var conn = _database.CreateConnection();

        // 1. Total count query
        int totalCount = await GetFilteredCountInternalAsync(conn, filter, ct);

        // 2. Page query
        var (whereSql, parameters) = BuildWhereClause(filter);
        string querySql = $@"
            SELECT Id, SessionId, CreatedAt, DurationMs, CharacterCount, WordCount,
                   Language, Application, ApplicationCategory, Mode, State,
                   WasEdited, IsFavorite, Text, TextHash, MetadataJson, IsDeleted, DeletedAt
            FROM DictationHistory
            {whereSql}
            ORDER BY CreatedAt DESC
            LIMIT @limit OFFSET @offset;
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = querySql;
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }
        cmd.Parameters.AddWithValue("@limit", safePageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        var list = new List<DictationEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadEntry(reader));
        }

        return new HistoryPage(list, safePageIndex, safePageSize, totalCount);
    }

    public async Task<HistoryPage> SearchAsync(string query, HistoryFilter? filter = null, int pageIndex = 0, int pageSize = 50, CancellationToken ct = default)
    {
        var effectiveFilter = (filter ?? new HistoryFilter()) with { SearchQuery = query };
        return await GetPagedAsync(effectiveFilter, pageIndex, pageSize, ct);
    }

    public async Task<bool> SetFavoriteAsync(string id, bool isFavorite, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "UPDATE DictationHistory SET IsFavorite = @fav WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@fav", isFavorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> SoftDeleteAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        string now = DateTimeOffset.UtcNow.ToString("O");
        cmd.CommandText = "UPDATE DictationHistory SET IsDeleted = 1, DeletedAt = @now WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@id", id);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> RestoreAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "UPDATE DictationHistory SET IsDeleted = 0, DeletedAt = NULL WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<bool> DeletePermanentlyAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM DictationHistory WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }

    public async Task<int> DeleteAllAsync(bool confirm, CancellationToken ct = default)
    {
        if (!confirm)
        {
            throw new InvalidOperationException("DeleteAll requires explicit confirmation parameter (confirm == true).");
        }

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        // Preserves starred/favorite entries unless user specifically removes favorite first
        cmd.CommandText = "DELETE FROM DictationHistory WHERE IsFavorite = 0;";
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> ApplyRetentionAsync(TimeSpan? maxAge, int? maxCount, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        await using var transaction = conn.BeginTransaction();
        int deletedTotal = 0;

        // 1. Age-based retention
        if (maxAge.HasValue && maxAge.Value > TimeSpan.Zero)
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow.Subtract(maxAge.Value);
            await using var ageCmd = conn.CreateCommand();
            ageCmd.Transaction = transaction;
            ageCmd.CommandText = @"
                DELETE FROM DictationHistory
                WHERE IsFavorite = 0 AND CreatedAt < @cutoff;
            ";
            ageCmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("O"));
            deletedTotal += await ageCmd.ExecuteNonQueryAsync(ct);
        }

        // 2. Count-based storage ceiling
        if (maxCount.HasValue && maxCount.Value > 0)
        {
            await using var countCmd = conn.CreateCommand();
            countCmd.Transaction = transaction;
            countCmd.CommandText = "SELECT COUNT(*) FROM DictationHistory WHERE IsFavorite = 0;";
            long currentCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync(ct));

            if (currentCount > maxCount.Value)
            {
                long excess = currentCount - maxCount.Value;
                await using var pruneCmd = conn.CreateCommand();
                pruneCmd.Transaction = transaction;
                pruneCmd.CommandText = @"
                    DELETE FROM DictationHistory
                    WHERE Id IN (
                        SELECT Id FROM DictationHistory
                        WHERE IsFavorite = 0
                        ORDER BY CreatedAt ASC
                        LIMIT @excess
                    );
                ";
                pruneCmd.Parameters.AddWithValue("@excess", excess);
                deletedTotal += await pruneCmd.ExecuteNonQueryAsync(ct);
            }
        }

        await transaction.CommitAsync(ct);
        return deletedTotal;
    }

    public async Task<int> GetCountAsync(HistoryFilter? filter = null, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        return await GetFilteredCountInternalAsync(conn, filter ?? new HistoryFilter(), ct);
    }

    public async Task<HistorySettings> GetSettingsAsync(CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT Key, Value FROM HistorySettings;";
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }

        bool historyEnabled = !dict.TryGetValue("HistoryEnabled", out var he) || !bool.TryParse(he, out var heb) || heb;
        bool saveTranscriptText = !dict.TryGetValue("SaveTranscriptText", out var st) || !bool.TryParse(st, out var stb) || stb;
        bool statsEnabled = !dict.TryGetValue("StatisticsCollectionEnabled", out var se) || !bool.TryParse(se, out var seb) || seb;

        RetentionPolicy retention = RetentionPolicy.Unlimited;
        if (dict.TryGetValue("RetentionPolicy", out var rp) && Enum.TryParse<RetentionPolicy>(rp, true, out var rpe))
        {
            retention = rpe;
        }

        int maxEntries = 10000;
        if (dict.TryGetValue("MaxHistoryEntries", out var me) && int.TryParse(me, out var meVal))
        {
            maxEntries = meVal;
        }

        string exportPerms = dict.TryGetValue("ExportPermissions", out var ep) ? ep : "LocalOnly";

        return new HistorySettings(historyEnabled, saveTranscriptText, statsEnabled, retention, maxEntries, exportPerms);
    }

    public async Task SaveSettingsAsync(HistorySettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await using var conn = _database.CreateConnection();
        await using var transaction = conn.BeginTransaction();
        string now = DateTimeOffset.UtcNow.ToString("O");

        var pairs = new (string Key, string Value)[]
        {
            ("HistoryEnabled", settings.HistoryEnabled ? "true" : "false"),
            ("SaveTranscriptText", settings.SaveTranscriptText ? "true" : "false"),
            ("StatisticsCollectionEnabled", settings.StatisticsCollectionEnabled ? "true" : "false"),
            ("RetentionPolicy", settings.Retention.ToString()),
            ("MaxHistoryEntries", settings.MaxHistoryEntries.ToString()),
            ("ExportPermissions", settings.ExportPermissions)
        };

        foreach (var (k, v) in pairs)
        {
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO HistorySettings (Key, Value, UpdatedAt)
                VALUES (@k, @v, @now)
                ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedAt = excluded.UpdatedAt;
            ";
            cmd.Parameters.AddWithValue("@k", k);
            cmd.Parameters.AddWithValue("@v", v);
            cmd.Parameters.AddWithValue("@now", now);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<DictationEntry>> GetAllForExportAsync(HistoryFilter? filter = null, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        var (whereSql, parameters) = BuildWhereClause(filter ?? new HistoryFilter());

        string querySql = $@"
            SELECT Id, SessionId, CreatedAt, DurationMs, CharacterCount, WordCount,
                   Language, Application, ApplicationCategory, Mode, State,
                   WasEdited, IsFavorite, Text, TextHash, MetadataJson, IsDeleted, DeletedAt
            FROM DictationHistory
            {whereSql}
            ORDER BY CreatedAt ASC;
        ";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = querySql;
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        var list = new List<DictationEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadEntry(reader));
        }

        return list;
    }

    public async Task<IReadOnlyList<DictationEntry>> GetEntriesForStatisticsAsync(DateTimeOffset? fromDate, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        if (fromDate.HasValue)
        {
            cmd.CommandText = @"
                SELECT Id, CreatedAt, DurationMs, CharacterCount, WordCount, Language, Application
                FROM DictationHistory
                WHERE IsDeleted = 0 AND State = 'Completed' AND CreatedAt >= @from
                ORDER BY CreatedAt ASC;
            ";
            cmd.Parameters.AddWithValue("@from", fromDate.Value.ToString("O"));
        }
        else
        {
            cmd.CommandText = @"
                SELECT Id, CreatedAt, DurationMs, CharacterCount, WordCount, Language, Application
                FROM DictationHistory
                WHERE IsDeleted = 0 AND State = 'Completed'
                ORDER BY CreatedAt ASC;
            ";
        }

        var list = new List<DictationEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DictationEntry(
                Id: reader.GetString(0),
                SessionId: Guid.Empty,
                CreatedAt: DateTimeOffset.Parse(reader.GetString(1)),
                DurationMs: reader.GetInt64(2),
                CharacterCount: reader.GetInt32(3),
                WordCount: reader.GetInt32(4),
                Language: reader.GetString(5),
                Application: reader.GetString(6),
                ApplicationCategory: "Productivity",
                Mode: "Dictation",
                State: HistoryState.Completed,
                WasEdited: false,
                IsFavorite: false,
                Text: null,
                TextHash: null,
                MetadataJson: null,
                IsDeleted: false,
                DeletedAt: null
            ));
        }

        return list;
    }

    public async Task<ProductivityMetrics> GetMetricsAsync(TimeRangeWindow window, DateTimeOffset? fromDate, TimeZoneInfo tz, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        string dateClause = fromDate.HasValue ? " AND CreatedAt >= @from" : "";

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT COUNT(*), COALESCE(SUM(WordCount), 0), COALESCE(SUM(CharacterCount), 0), COALESCE(SUM(DurationMs), 0)
            FROM DictationHistory WHERE IsDeleted = 0 AND State = 'Completed'{dateClause};

            SELECT Application, COUNT(*)
            FROM DictationHistory WHERE IsDeleted = 0 AND State = 'Completed'{dateClause}
            GROUP BY Application ORDER BY COUNT(*) DESC LIMIT 10;

            SELECT Language, COUNT(*)
            FROM DictationHistory WHERE IsDeleted = 0 AND State = 'Completed'{dateClause}
            GROUP BY Language ORDER BY COUNT(*) DESC LIMIT 5;

            SELECT substr(CreatedAt, 1, 10), SUM(WordCount), SUM(CharacterCount), COUNT(*), SUM(DurationMs)
            FROM DictationHistory WHERE IsDeleted = 0 AND State = 'Completed'{dateClause}
            GROUP BY substr(CreatedAt, 1, 10) ORDER BY substr(CreatedAt, 1, 10) ASC;
        ";
        if (fromDate.HasValue) cmd.Parameters.AddWithValue("@from", fromDate.Value.ToString("O"));

        int totalSessions = 0;
        int totalWords = 0;
        int totalChars = 0;
        long totalDurationMs = 0;
        var topApps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var topLangs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dailyList = new List<DailyUsageMetric>();

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        // Result 1: Aggregates
        if (await reader.ReadAsync(ct))
        {
            totalSessions = reader.GetInt32(0);
            totalWords = Convert.ToInt32(reader.GetInt64(1));
            totalChars = Convert.ToInt32(reader.GetInt64(2));
            totalDurationMs = reader.GetInt64(3);
        }

        // Result 2: Top Apps
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                topApps[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Result 3: Top Languages
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                topLangs[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Result 4: Daily Usage
        if (await reader.NextResultAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (DateOnly.TryParse(reader.GetString(0), out var date))
                {
                    dailyList.Add(new DailyUsageMetric(
                        date,
                        Convert.ToInt32(reader.GetInt64(1)),
                        Convert.ToInt32(reader.GetInt64(2)),
                        reader.GetInt32(3),
                        reader.GetInt64(4)
                    ));
                }
            }
        }

        double avgWordsPerSession = totalSessions > 0 ? Math.Round((double)totalWords / totalSessions, 1) : 0.0;
        double avgDurationSec = totalSessions > 0 ? Math.Round((double)totalDurationMs / (totalSessions * 1000.0), 1) : 0.0;
        double activeMinutes = totalDurationMs / 60000.0;
        double avgWpm = (totalDurationMs >= 10000 && totalWords > 0) ? Math.Round(totalWords / activeMinutes, 1) : 0.0;

        return new ProductivityMetrics(
            Window: window,
            TotalWords: totalWords,
            TotalCharacters: totalChars,
            TotalSessions: totalSessions,
            TotalActiveDurationMs: totalDurationMs,
            AverageWpm: avgWpm,
            AverageWordsPerSession: avgWordsPerSession,
            AverageSessionDurationSeconds: avgDurationSec,
            TopApplications: topApps,
            TopLanguages: topLangs,
            DailyUsage: dailyList
        );
    }

    public async Task<IReadOnlyList<DateOnly>> GetActiveDaysAsync(int minWordsThreshold, TimeZoneInfo tz, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT substr(CreatedAt, 1, 10)
            FROM DictationHistory
            WHERE IsDeleted = 0 AND State = 'Completed'
            GROUP BY substr(CreatedAt, 1, 10)
            HAVING SUM(WordCount) >= @minWords
            ORDER BY substr(CreatedAt, 1, 10) ASC;
        ";
        cmd.Parameters.AddWithValue("@minWords", Math.Max(1, minWordsThreshold));

        var list = new List<DateOnly>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (DateOnly.TryParse(reader.GetString(0), out var d))
            {
                list.Add(d);
            }
        }

        return list;
    }

    public async Task<long> GetMaxDurationMsAsync(DateTimeOffset? fromDate, CancellationToken ct = default)
    {
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();
        string dateClause = fromDate.HasValue ? " AND CreatedAt >= @from" : "";
        cmd.CommandText = $"SELECT COALESCE(MAX(DurationMs), 0) FROM DictationHistory WHERE IsDeleted = 0 AND State = 'Completed'{dateClause};";
        if (fromDate.HasValue) cmd.Parameters.AddWithValue("@from", fromDate.Value.ToString("O"));

        var result = await cmd.ExecuteScalarAsync(ct);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    private static async Task<int> GetFilteredCountInternalAsync(SqliteConnection conn, HistoryFilter filter, CancellationToken ct)
    {
        var (whereSql, parameters) = BuildWhereClause(filter);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM DictationHistory {whereSql};";
        foreach (var (k, v) in parameters)
        {
            cmd.Parameters.AddWithValue(k, v);
        }

        var val = await cmd.ExecuteScalarAsync(ct);
        return val != null ? Convert.ToInt32(val) : 0;
    }

    private static (string WhereSql, Dictionary<string, object> Parameters) BuildWhereClause(HistoryFilter filter)
    {
        var clauses = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (!filter.IncludeDeleted)
        {
            clauses.Add("IsDeleted = 0");
        }

        if (filter.IsFavorite.HasValue)
        {
            clauses.Add("IsFavorite = @fav");
            parameters["@fav"] = filter.IsFavorite.Value ? 1 : 0;
        }

        if (filter.State.HasValue)
        {
            clauses.Add("State = @state");
            parameters["@state"] = filter.State.Value.ToString();
        }

        if (!string.IsNullOrWhiteSpace(filter.Application))
        {
            clauses.Add("Application = @app");
            parameters["@app"] = filter.Application.Trim();
        }

        if (!string.IsNullOrWhiteSpace(filter.Language))
        {
            clauses.Add("Language = @lang");
            parameters["@lang"] = filter.Language.Trim();
        }

        if (!string.IsNullOrWhiteSpace(filter.Mode))
        {
            clauses.Add("Mode = @mode");
            parameters["@mode"] = filter.Mode.Trim();
        }

        if (filter.FromDate.HasValue)
        {
            clauses.Add("CreatedAt >= @from");
            parameters["@from"] = filter.FromDate.Value.ToString("O");
        }

        if (filter.ToDate.HasValue)
        {
            clauses.Add("CreatedAt <= @to");
            parameters["@to"] = filter.ToDate.Value.ToString("O");
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
        {
            // Parameterized search safe against SQL injection with FTS5 acceleration
            string query = filter.SearchQuery.Trim();
            string clean = query.Replace("*", "");
            string? fts = BuildFtsQuery(query);

            if (!string.IsNullOrWhiteSpace(fts))
            {
                clauses.Add("rowid IN (SELECT rowid FROM DictationHistoryFts WHERE DictationHistoryFts MATCH @fts)");
                parameters["@fts"] = fts;
            }
            else
            {
                clauses.Add("(Text LIKE @search OR Application LIKE @search OR Language LIKE @search)");
                parameters["@search"] = $"%{clean}%";
            }
        }

        string whereSql = clauses.Count > 0 ? "WHERE " + string.Join(" AND ", clauses) : "";
        return (whereSql, parameters);
    }

    private static DictationEntry ReadEntry(SqliteDataReader reader)
    {
        return new DictationEntry(
            Id: reader.GetString(0),
            SessionId: Guid.Parse(reader.GetString(1)),
            CreatedAt: DateTimeOffset.Parse(reader.GetString(2)),
            DurationMs: reader.GetInt64(3),
            CharacterCount: reader.GetInt32(4),
            WordCount: reader.GetInt32(5),
            Language: reader.GetString(6),
            Application: reader.GetString(7),
            ApplicationCategory: reader.GetString(8),
            Mode: reader.GetString(9),
            State: Enum.Parse<HistoryState>(reader.GetString(10)),
            WasEdited: reader.GetInt32(11) == 1,
            IsFavorite: reader.GetInt32(12) == 1,
            Text: reader.IsDBNull(13) ? null : reader.GetString(13),
            TextHash: reader.IsDBNull(14) ? null : reader.GetString(14),
            MetadataJson: reader.IsDBNull(15) ? null : reader.GetString(15),
            IsDeleted: reader.GetInt32(16) == 1,
            DeletedAt: reader.IsDBNull(17) ? null : DateTimeOffset.Parse(reader.GetString(17))
        );
    }

    private static string? ComputeHash(string? text)
    {
        if (text == null) return null;
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
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
