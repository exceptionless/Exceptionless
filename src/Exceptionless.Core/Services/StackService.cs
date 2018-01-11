using System;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Foundatio.Caching;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services {
    public class StackService {
        private readonly ILogger<UsageService> _logger;
        private readonly IStackRepository _stackRepository;
        private readonly ICacheClient _cache;
        private readonly TimeSpan _expireTimeout = TimeSpan.FromHours(12);

        public StackService(IStackRepository stackRepository, ICacheClient cache, ILoggerFactory loggerFactory = null) {
            _stackRepository = stackRepository;
            _cache = cache;
            _logger = loggerFactory.CreateLogger<UsageService>();
        }

        public async Task IncrementStackUsageAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count) {
            if (String.IsNullOrEmpty(organizationId))
                throw new ArgumentNullException(nameof(organizationId));
            if (String.IsNullOrEmpty(projectId))
                throw new ArgumentNullException(nameof(projectId));
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException(nameof(stackId));
            if (count == 0)
                return;

            await Task.WhenAll(
                _cache.SetAddAsync(GetStackOccurrenceSetCacheKey(), Tuple.Create(organizationId, projectId, stackId)),
                _cache.IncrementAsync(GetStackOccurrenceCountCacheKey(organizationId, projectId, stackId), count, _expireTimeout),
                _cache.SetIfLowerAsync(GetStackOccurrenceMinDateCacheKey(organizationId, projectId, stackId), minOccurrenceDateUtc.Ticks, _expireTimeout),
                _cache.SetIfHigherAsync(GetStackOccurrenceMaxDateCacheKey(organizationId, projectId, stackId), maxOccurrenceDateUtc.Ticks, _expireTimeout)
            ).AnyContext();
        }

        public async Task SaveStackUsagesAsync(bool sendNotifications = true, CancellationToken cancellationToken = default) {
            var occurrenceSetCacheKey = GetStackOccurrenceSetCacheKey();
            var stackUsageSet = await _cache.GetSetAsync<Tuple<string, string, string>>(occurrenceSetCacheKey).AnyContext();
            if (!stackUsageSet.HasValue || stackUsageSet.IsNull) return;
            foreach (var tuple in stackUsageSet.Value) {
                if (cancellationToken.IsCancellationRequested) break;

                string organizationId = tuple.Item1, projectId = tuple.Item2, stackId = tuple.Item3;
                string occurrenceCountCacheKey = GetStackOccurrenceCountCacheKey(organizationId, projectId, stackId),
                    occurrenceMinDateCacheKey = GetStackOccurrenceMinDateCacheKey(organizationId, projectId, stackId),
                    occurrenceMaxDateCacheKey = GetStackOccurrenceMaxDateCacheKey(organizationId, projectId, stackId);
                var occurrenceCountTask = _cache.GetAsync<long>(occurrenceCountCacheKey, 0);
                var occurrenceMinDateTask = _cache.GetAsync<double>(occurrenceMinDateCacheKey, SystemClock.UtcNow.Ticks);
                var occurrenceMaxDateTask = _cache.GetAsync<double>(occurrenceMaxDateCacheKey, SystemClock.UtcNow.Ticks);

                await Task.WhenAll(occurrenceCountTask, occurrenceMinDateTask, occurrenceMaxDateTask).AnyContext();
                var occurrenceCount = (int)occurrenceCountTask.Result;
                if (occurrenceCount == 0) continue;
                var occurrenceMinDate = new DateTime((long)occurrenceMinDateTask.Result);
                var occurrenceMaxDate = new DateTime((long)occurrenceMaxDateTask.Result);

                await _stackRepository.IncrementEventCounterAsync(organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate, occurrenceCount, sendNotifications).AnyContext();
                await _cache.DecrementAsync(occurrenceCountCacheKey, occurrenceCount, _expireTimeout).AnyContext();
                _logger.LogTrace("Increment event count {occurrenceCount} for organization:{organizationId} project:{projectId} stack:{stackId} with occurrenceMinDate:{occurrenceMinDate} occurrenceMaxDate:{occurrenceMaxDate}", occurrenceCount, organizationId, projectId, stackId, occurrenceMinDate, occurrenceMaxDate);
            }
        }

        private string GetStackOccurrenceSetCacheKey() {
            return "usage:occurrences";
        }

        private string GetStackOccurrenceCountCacheKey(string organizationId, string projectId, string stackId) {
            return $"usage:occurrences:count:{organizationId}:{projectId}:{stackId}";
        }

        private string GetStackOccurrenceMinDateCacheKey(string organizationId, string projectId, string stackId) {
            return $"usage:occurrences:mindate:{organizationId}:{projectId}:{stackId}";
        }

        private string GetStackOccurrenceMaxDateCacheKey(string organizationId, string projectId, string stackId) {
            return $"usage:occurrences:maxdate:{organizationId}:{projectId}:{stackId}";
        }
    }
}
