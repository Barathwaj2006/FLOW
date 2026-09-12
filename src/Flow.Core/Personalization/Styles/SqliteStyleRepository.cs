using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Flow.Core.Storage;
using Microsoft.Data.Sqlite;

namespace Flow.Core.Personalization.Styles;

/// <summary>
/// SQLite implementation of IStyleRepository.
/// </summary>
public sealed class SqliteStyleRepository : IStyleRepository
{
    private readonly SqlitePersonalizationDatabase _database;

    public SqliteStyleRepository(SqlitePersonalizationDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<IReadOnlyList<StyleProfile>> GetAllProfilesAsync(CancellationToken ct = default)
    {
        var list = new List<StyleProfile>();
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Name, Description, ContractionPolicy, FormalityLevel, UseBulletPoints, CreatedAt, UpdatedAt, IsEnabled, LanguageScope
            FROM StyleProfiles
            ORDER BY Name ASC;
        ";

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(ReadProfile(reader));
        }

        return list;
    }

    public async Task<StyleProfile?> GetProfileByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Name, Description, ContractionPolicy, FormalityLevel, UseBulletPoints, CreatedAt, UpdatedAt, IsEnabled, LanguageScope
            FROM StyleProfiles
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadProfile(reader);
        }

        return null;
    }

    public async Task<StyleProfile?> GetProfileByNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            SELECT Id, Name, Description, ContractionPolicy, FormalityLevel, UseBulletPoints, CreatedAt, UpdatedAt, IsEnabled, LanguageScope
            FROM StyleProfiles
            WHERE Name = @name COLLATE NOCASE;
        ";
        cmd.Parameters.AddWithValue("@name", name.Trim());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return ReadProfile(reader);
        }

        return null;
    }

    public Task AddProfileAsync(StyleProfile profile, CancellationToken ct = default) => SaveProfileAsync(profile, ct);
    public Task UpdateProfileAsync(StyleProfile profile, CancellationToken ct = default) => SaveProfileAsync(profile, ct);

    public async Task SaveProfileAsync(StyleProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profile));
        }

        profile.UpdatedAt = DateTime.UtcNow;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO StyleProfiles (Id, Name, Description, ContractionPolicy, FormalityLevel, UseBulletPoints, IsEnabled, LanguageScope, CreatedAt, UpdatedAt)
            VALUES (@id, @name, @desc, @contractions, @formality, @bullets, @isEnabled, @langScope, @createdAt, @updatedAt)
            ON CONFLICT(Id) DO UPDATE SET
                Name = excluded.Name,
                Description = excluded.Description,
                ContractionPolicy = excluded.ContractionPolicy,
                FormalityLevel = excluded.FormalityLevel,
                UseBulletPoints = excluded.UseBulletPoints,
                IsEnabled = excluded.IsEnabled,
                LanguageScope = excluded.LanguageScope,
                UpdatedAt = excluded.UpdatedAt;
        ";
        cmd.Parameters.AddWithValue("@id", profile.Id);
        cmd.Parameters.AddWithValue("@name", profile.Name.Trim());
        cmd.Parameters.AddWithValue("@desc", (object?)profile.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@contractions", (int)profile.ContractionPolicy);
        cmd.Parameters.AddWithValue("@formality", (int)profile.FormalityLevel);
        cmd.Parameters.AddWithValue("@bullets", profile.UseBulletPoints ? 1 : 0);
        cmd.Parameters.AddWithValue("@isEnabled", profile.IsEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@langScope", (object?)profile.LanguageScope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", profile.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@updatedAt", profile.UpdatedAt.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteProfileAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM StyleProfiles WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAppMappingsAsync(CancellationToken ct = default)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT ProcessName, StyleProfileId FROM AppStyleMappings;";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }

        return dict;
    }

    public async Task<string?> GetStyleIdForAppAsync(string processName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(processName)) return null;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT StyleProfileId FROM AppStyleMappings WHERE ProcessName = @proc COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@proc", processName.Trim());

        object? result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task SetStyleForAppAsync(string processName, string styleProfileId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(processName);
        ArgumentException.ThrowIfNullOrWhiteSpace(styleProfileId);

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO AppStyleMappings (ProcessName, StyleProfileId, CreatedAt)
            VALUES (@proc, @styleId, @createdAt)
            ON CONFLICT(ProcessName) DO UPDATE SET
                StyleProfileId = excluded.StyleProfileId;
        ";
        cmd.Parameters.AddWithValue("@proc", processName.Trim());
        cmd.Parameters.AddWithValue("@styleId", styleProfileId.Trim());
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveStyleForAppAsync(string processName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(processName)) return;

        await using var conn = _database.CreateConnection();
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = "DELETE FROM AppStyleMappings WHERE ProcessName = @proc COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@proc", processName.Trim());

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static StyleProfile ReadProfile(SqliteDataReader reader)
    {
        var profile = new StyleProfile
        {
            Id = reader.GetString(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? null : reader.GetString(2),
            ContractionPolicy = (ContractionPolicy)reader.GetInt32(3),
            FormalityLevel = (FormalityLevel)reader.GetInt32(4),
            UseBulletPoints = reader.GetInt32(5) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(6)),
            UpdatedAt = DateTime.Parse(reader.GetString(7))
        };

        if (reader.FieldCount > 8 && !reader.IsDBNull(8)) profile.IsEnabled = reader.GetInt32(8) == 1;
        if (reader.FieldCount > 9 && !reader.IsDBNull(9)) profile.LanguageScope = reader.GetString(9);

        return profile;
    }
}
