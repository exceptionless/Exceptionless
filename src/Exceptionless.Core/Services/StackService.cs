using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services {
    public class StackService {
        private readonly ILogger<UsageService> _logger;
        private readonly IStackRepository _stackRepository;
        private readonly ICacheClient _cache;

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

            var tasks = new List<Task>(4);

            string occurenceCountCacheKey = GetStackOccurrenceCountCacheKey(organizationId, projectId, stackId),
                occurrenceMinDateCacheKey = GetStackOccurrenceMinDateCacheKey(organizationId, projectId, stackId),
                occurrenceMaxDateCacheKey = GetStackOccurrenceMaxDateCacheKey(organizationId, projectId, stackId),
                occurrenceSetCacheKey = GetStackOccurrenceSetCacheKey();

            var cachedOccurrenceMinDateUtc = await _cache.GetAsync<DateTime>(occurrenceMinDateCacheKey).AnyContext();
            if (!cachedOccurrenceMinDateUtc.HasValue || cachedOccurrenceMinDateUtc.Value.IsAfter(minOccurrenceDateUtc))
                tasks.Add(_cache.SetAsync(occurrenceMinDateCacheKey, minOccurrenceDateUtc));

            var cachedOccurrenceMaxDateUtc = await _cache.GetAsync<DateTime>(occurrenceMaxDateCacheKey).AnyContext();
            if (!cachedOccurrenceMaxDateUtc.HasValue || cachedOccurrenceMaxDateUtc.Value.IsBefore(maxOccurrenceDateUtc))
                tasks.Add(_cache.SetAsync(occurrenceMaxDateCacheKey, maxOccurrenceDateUtc));

            tasks.Add(_cache.IncrementAsync(occurenceCountCacheKey, count));
            tasks.Add(_cache.SetAddAsync(occurrenceSetCacheKey, Tuple.Create(organizationId, projectId, stackId)));

            await Task.WhenAll(tasks).AnyContext();
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
                var occurrenceCount = _cache.GetAsync<long>(occurrenceCountCacheKey, 0);
                var occurrenceMinDate = _cache.GetAsync(occurrenceMinDateCacheKey, SystemClock.UtcNow);
                var occurrenceMaxDate = _cache.GetAsync(occurrenceMaxDateCacheKey, SystemClock.UtcNow);

                await Task.WhenAll(occurrenceCount, occurrenceMinDate, occurrenceMaxDate).AnyContext();
                if (occurrenceCount.Result == 0) continue;
                await _cache.RemoveAllAsync(new[] { occurrenceCountCacheKey, occurrenceMinDateCacheKey, occurrenceMaxDateCacheKey }).AnyContext();

                try {
                    await _stackRepository.IncrementEventCounterAsync(organizationId, projectId, stackId, occurrenceMinDate.Result, occurrenceMaxDate.Result, (int)occurrenceCount.Result, sendNotifications).AnyContext();
                    _logger.LogTrace("Increment event count {occurrenceCount} for organization:{organizationId} project:{projectId} stack:{stackId} with occurrenceMinDate:{occurrenceMinDate} occurrenceMaxDate:{occurrenceMaxDate}", occurrenceCount.Result, organizationId, projectId, stackId, occurrenceMinDate.Result, occurrenceMaxDate.Result);
                }
                catch {
                    await IncrementStackUsageAsync(organizationId, projectId, stackId, occurrenceMinDate.Result, occurrenceMaxDate.Result, (int)occurrenceCount.Result).AnyContext();
                    throw;
                }
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
