using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services {
    public class StackService {
        private readonly ILogger<UsageService> _logger;
        private readonly IStackRepository _stackRepository;
        private readonly ICacheClient _cache;
        private readonly TimeSpan _expireTimeout = TimeSpan.FromHours(12);

        public StackService(IStackRepository stackRepository, ICacheClient cache, ILoggerFactory loggerFactory = null) {
            _stackRepository = stackRepository;
            _cache = cache;
            _logger = loggerFactory?.CreateLogger<UsageService>() ?? NullLogger<UsageService>.Instance;
        }

        public async Task IncrementStackUsageAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));
            if (count <= 0)
                return;

            await Task.WhenAll(
                _cache.SetAddAsync(GetStackOccurrenceSetCacheKey(), (organizationId, projectId, stackId)),
                _cache.IncrementAsync(GetStackOccurrenceCountCacheKey(stackId), count, _expireTimeout),
                _cache.SetIfLowerAsync(GetStackOccurrenceMinDateCacheKey(stackId), minOccurrenceDateUtc, _expireTimeout),
                _cache.SetIfHigherAsync(GetStackOccurrenceMaxDateCacheKey(stackId), maxOccurrenceDateUtc, _expireTimeout)
            ).AnyContext();
        }

        public async Task SaveStackUsagesAsync(bool sendNotifications = true, CancellationToken cancellationToken = default) {
            string occurrenceSetCacheKey = GetStackOccurrenceSetCacheKey();
            var stackUsageSet = await _cache.GetSetAsync<(string OrganizationId, string ProjectId, string StackId)>(occurrenceSetCacheKey).AnyContext();
            if (!stackUsageSet.HasValue) 
                return;

            foreach (var (organizationId, projectId, stackId) in stackUsageSet.Value) {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var removeFromSetTask = _cache.SetRemoveAsync(occurrenceSetCacheKey, (organizationId, projectId, stackId));
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
                ).AnyContext();

                int occurrenceCount = (int)countTask.Result;
                if (occurrenceCount <= 0) {
                    await _cache.RemoveAllAsync(new[] { minDateCacheKey, maxDateCacheKey }).AnyContext();
                    continue;
                }

                await Task.WhenAll(
                    _cache.RemoveAllAsync(new []{ minDateCacheKey, maxDateCacheKey }),
                    _cache.DecrementAsync(countCacheKey, occurrenceCount, _expireTimeout)
                ).AnyContext();

                var occurrenceMinDate = minDateTask.Result;
                var occurrenceMaxDate = maxDateTask.Result;
                bool shouldRetry = false;
                try {
                    if (!await _stackRepository.IncrementEventCounterAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount, sendNotifications).AnyContext()) {
                        shouldRetry = true;
                        await IncrementStackUsageAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount).AnyContext();
                    } else if (_logger.IsEnabled(LogLevel.Trace)) {
                        _logger.LogTrace("Increment event count {OccurrenceCount} for organization:{OrganizationId} project:{ProjectId} stack:{StackId} with Min Date:{OccurrenceMinDate} Max Date:{OccurrenceMaxDate}", occurrenceCount, organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate);
                    }
                } catch(Exception ex) {
                    _logger.LogError(ex, "Error incrementing event count for organization: {OrganizationId} project:{ProjectId} stack:{StackId}", organizationId, projectId, stackId);
                    if (!shouldRetry)
                        await IncrementStackUsageAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount).AnyContext();
                }
            }
        }

        internal string GetStackOccurrenceSetCacheKey() {
            return "usage:occurrences";
        }

        internal string GetStackOccurrenceCountCacheKey(string stackId) {
            return String.Concat("usage:occurrences:count:", stackId);
        }

        internal string GetStackOccurrenceMinDateCacheKey( string stackId) {
            return String.Concat("usage:occurrences:min:", stackId);
        }

        internal string GetStackOccurrenceMaxDateCacheKey(string stackId) {
            return String.Concat("usage:occurrences:max:", stackId);
        }
    }
}