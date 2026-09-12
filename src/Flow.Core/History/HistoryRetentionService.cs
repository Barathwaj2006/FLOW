using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flow.Core.History;

/// <summary>
/// Enforces storage limits and retention schedules (WF-040).
/// INVIOLABLE: Starred / Favorite entries and active sessions are NEVER automatically deleted.
/// </summary>
public sealed class HistoryRetentionService : IHistoryRetentionService
{
    private readonly IHistoryRepository _repository;

    public HistoryRetentionService(IHistoryRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<int> EnforceRetentionAsync(CancellationToken ct = default)
    {
        var settings = await _repository.GetSettingsAsync(ct);
        if (!settings.HistoryEnabled)
        {
            return 0;
        }

        TimeSpan? maxAge = settings.Retention switch
        {
            RetentionPolicy.ThirtyDays => TimeSpan.FromDays(30),
            RetentionPolicy.NinetyDays => TimeSpan.FromDays(90),
            RetentionPolicy.OneHundredEightyDays => TimeSpan.FromDays(180),
            RetentionPolicy.OneYear => TimeSpan.FromDays(365),
            RetentionPolicy.Unlimited => null,
            _ => null
        };

        int maxCount = settings.MaxHistoryEntries > 0 ? settings.MaxHistoryEntries : 10000;

        return await _repository.ApplyRetentionAsync(maxAge, maxCount, ct);
    }
}
