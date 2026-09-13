using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Flow.Core.Storage;

/// <summary>
/// SQLite-backed persistent settings repository storing user preferences in the local personalization database.
/// Operates 100% offline with zero cloud transmission.
/// </summary>
public sealed class SqliteSettingsRepository : ISettingsRepository
{
    private readonly SqlitePersonalizationDatabase _db;

    public SqliteSettingsRepository(SqlitePersonalizationDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var conn = _db.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @Key LIMIT 1;";
        cmd.Parameters.AddWithValue("@Key", key);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result?.ToString();
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await using var conn = _db.CreateConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO AppSettings (Key, Value, UpdatedAt)
            VALUES (@Key, @Value, @UpdatedAt)
            ON CONFLICT(Key) DO UPDATE SET
                Value = excluded.Value,
                UpdatedAt = excluded.UpdatedAt;";

        cmd.Parameters.AddWithValue("@Key", key);
        cmd.Parameters.AddWithValue("@Value", value);
        cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("o"));

        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<FlowAppSettings> LoadSettingsAsync(CancellationToken ct = default)
    {
        string language = await GetSettingAsync("Language", ct) ?? "en";
        string? vadStr = await GetSettingAsync("VadThreshold", ct);
        float vadThreshold = float.TryParse(vadStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedVad)
            ? parsedVad
            : 0.015f;

        string? hotkeyStr = await GetSettingAsync("HotkeyVk", ct);
        int hotkeyVk = int.TryParse(hotkeyStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVk)
            ? parsedVk
            : 0xA5;

        string theme = await GetSettingAsync("Theme", ct) ?? "Dark";
        string? audioDeviceId = await GetSettingAsync("AudioDeviceId", ct);

        return new FlowAppSettings(
            Language: language,
            VadThreshold: vadThreshold,
            HotkeyVk: hotkeyVk,
            Theme: theme,
            AudioDeviceId: audioDeviceId
        );
    }

    public async Task SaveSettingsAsync(FlowAppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await SetSettingAsync("Language", settings.Language, ct);
        await SetSettingAsync("VadThreshold", settings.VadThreshold.ToString(CultureInfo.InvariantCulture), ct);
        await SetSettingAsync("HotkeyVk", settings.HotkeyVk.ToString(CultureInfo.InvariantCulture), ct);
        await SetSettingAsync("Theme", settings.Theme, ct);
        if (settings.AudioDeviceId != null)
        {
            await SetSettingAsync("AudioDeviceId", settings.AudioDeviceId, ct);
        }
    }
}
