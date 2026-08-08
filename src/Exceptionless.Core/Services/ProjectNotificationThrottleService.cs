using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;

namespace Exceptionless.Core.Services;

/// <summary>Shares the project-wide email budget across event and rate notifications.</summary>
public sealed class ProjectNotificationThrottleService
{
    public const int NotificationLimit = 10;
    public static readonly TimeSpan TimeWindow = TimeSpan.FromMinutes(30);

    private readonly ICacheClient _cache;
    private readonly TimeProvider _timeProvider;

    public ProjectNotificationThrottleService(ICacheClient cache, TimeProvider timeProvider)
    {
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public Task<long> IncrementAsync(string projectId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        cancellationToken.ThrowIfCancellationRequested();

        string cacheKey = $"notify:project-throttle:{projectId}-{_timeProvider.GetUtcNow().UtcDateTime.Floor(TimeWindow).Ticks}";
        return _cache.IncrementAsync(cacheKey, 1, TimeWindow);
    }
}
