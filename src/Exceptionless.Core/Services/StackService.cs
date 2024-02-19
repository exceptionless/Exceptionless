using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services;

public class StackService
{
    private readonly ILogger<UsageService> _logger;
    private readonly IStackRepository _stackRepository;
    private readonly ICacheClient _cache;
    private readonly TimeSpan _expireTimeout = TimeSpan.FromHours(12);

    public StackService(IStackRepository stackRepository, ICacheClient cache, ILoggerFactory loggerFactory)
    {
        _stackRepository = stackRepository;
        _cache = cache;
        _logger = loggerFactory.CreateLogger<UsageService>() ?? NullLogger<UsageService>.Instance;
    }

    public async Task IncrementStackUsageAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        ArgumentException.ThrowIfNullOrEmpty(stackId);
        if (count <= 0)
            return;

        await Task.WhenAll(
            _cache.ListAddAsync(GetStackOccurrenceSetCacheKey(), (organizationId, projectId, stackId)),
            _cache.IncrementAsync(GetStackOccurrenceCountCacheKey(stackId), count, _expireTimeout),
            _cache.SetIfLowerAsync(GetStackOccurrenceMinDateCacheKey(stackId), minOccurrenceDateUtc, _expireTimeout),
            _cache.SetIfHigherAsync(GetStackOccurrenceMaxDateCacheKey(stackId), maxOccurrenceDateUtc, _expireTimeout)
        );
    }

    public async Task SaveStackUsagesAsync(bool sendNotifications = true, CancellationToken cancellationToken = default)
    {
        string occurrenceSetCacheKey = GetStackOccurrenceSetCacheKey();
        var stackUsageSet = await _cache.GetListAsync<(string OrganizationId, string ProjectId, string StackId)>(occurrenceSetCacheKey);
        if (!stackUsageSet.HasValue)
            return;

        foreach ((string? organizationId, string? projectId, string? stackId) in stackUsageSet.Value)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var removeFromSetTask = _cache.ListRemoveAsync(occurrenceSetCacheKey, (organizationId, projectId, stackId));
            string countCacheKey = GetStackOccurrenceCountCacheKey(stackId);
            var countTask = _cache.GetAsync<long>(countCacheKey, 0);
            string minDateCacheKey = GetStackOccurrenceMinDateCacheKey(stackId);
            var minDateTask = _cache.GetUnixTimeMillisecondsAsync(minDateCacheKey, SystemClock.UtcNow);
            string maxDateCacheKey = GetStackOccurrenceMaxDateCacheKey(stackId);
            var maxDateTask = _cache.GetUnixTimeMillisecondsAsync(maxDateCacheKey, SystemClock.UtcNow);

            await Task.WhenAll(
                removeFromSetTask,
                countTask,
                minDateTask,
                maxDateTask
            );

            int occurrenceCount = (int)countTask.Result;
            if (occurrenceCount <= 0)
            {
                await _cache.RemoveAllAsync(new[] { minDateCacheKey, maxDateCacheKey });
                continue;
            }

            await Task.WhenAll(
                _cache.RemoveAllAsync(new[] { minDateCacheKey, maxDateCacheKey }),
                _cache.DecrementAsync(countCacheKey, occurrenceCount, _expireTimeout)
            );

            var occurrenceMinDate = minDateTask.Result;
            var occurrenceMaxDate = maxDateTask.Result;
            bool shouldRetry = false;
            try
            {
                if (!await _stackRepository.IncrementEventCounterAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount, sendNotifications))
                {
                    shouldRetry = true;
                    await IncrementStackUsageAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount);
                }
                else
                {
                    _logger.LogTrace("Increment event count {OccurrenceCount} for organization:{OrganizationId} project:{ProjectId} stack:{StackId} with Min Date:{OccurrenceMinDate} Max Date:{OccurrenceMaxDate}", occurrenceCount, organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing event count for organization: {OrganizationId} project:{ProjectId} stack:{StackId}", organizationId, projectId, stackId);
                if (!shouldRetry)
                {
                    await IncrementStackUsageAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount);
                }
            }
        }
    }

    internal static string GetStackOccurrenceSetCacheKey()
    {
        return "usage:occurrences";
    }

    internal static string GetStackOccurrenceCountCacheKey(string stackId)
    {
        return String.Concat("usage:occurrences:count:", stackId);
    }

    internal static string GetStackOccurrenceMinDateCacheKey(string stackId)
    {
        return String.Concat("usage:occurrences:min:", stackId);
    }

    internal static string GetStackOccurrenceMaxDateCacheKey(string stackId)
    {
        return String.Concat("usage:occurrences:max:", stackId);
    }
}
