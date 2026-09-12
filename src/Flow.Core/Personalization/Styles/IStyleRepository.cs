using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.Personalization.Styles;

/// <summary>
/// Repository interface for style profiles and application-to-style mappings.
/// </summary>
public interface IStyleRepository
{
    Task<IReadOnlyList<StyleProfile>> GetAllProfilesAsync(CancellationToken ct = default);
    Task<StyleProfile?> GetProfileByIdAsync(string id, CancellationToken ct = default);
    Task<StyleProfile?> GetProfileByNameAsync(string name, CancellationToken ct = default);
    Task SaveProfileAsync(StyleProfile profile, CancellationToken ct = default);
    Task DeleteProfileAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAppMappingsAsync(CancellationToken ct = default);
    Task<string?> GetStyleIdForAppAsync(string processName, CancellationToken ct = default);
    Task SetStyleForAppAsync(string processName, string styleProfileId, CancellationToken ct = default);
    Task RemoveStyleForAppAsync(string processName, CancellationToken ct = default);
}
