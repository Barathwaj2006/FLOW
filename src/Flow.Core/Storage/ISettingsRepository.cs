using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Storage;

/// <summary>
/// Persisted user application settings for the FLOW desktop client.
/// </summary>
public sealed record FlowAppSettings(
    string Language = "en",
    float VadThreshold = 0.015f,
    int HotkeyVk = 0xA5, // VK_RMENU (Right Alt)
    string Theme = "Dark",
    string? AudioDeviceId = null,
    string RetentionPolicy = "Unlimited",
    bool LaunchOnStartup = false,
    bool EnableAnonymousTelemetry = false
);

/// <summary>
/// Repository contract for reading and writing application settings 100% locally.
/// </summary>
public interface ISettingsRepository
{
    Task<string?> GetSettingAsync(string key, CancellationToken ct = default);
    Task SetSettingAsync(string key, string value, CancellationToken ct = default);
    Task<FlowAppSettings> LoadSettingsAsync(CancellationToken ct = default);
    Task SaveSettingsAsync(FlowAppSettings settings, CancellationToken ct = default);
}
