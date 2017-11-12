using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(0)]
    public sealed class ThrottleBotsPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cache;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        public ThrottleBotsPlugin(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue, ILoggerFactory loggerFactory = null) : base(loggerFactory) {
            _cache = cacheClient;
            _workItemQueue = workItemQueue;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            if (Settings.Current.AppMode == AppMode.Development)
                return;

            var firstContext = contexts.First();
            if (!firstContext.Project.DeleteBotDataEnabled)
                return;

            // Throttle errors by client ip address to no more than X every 5 minutes.
            var clientIpAddressGroups = contexts.Where(c => !String.IsNullOrEmpty(c.Event.GetRequestInfo()?.ClientIpAddress)).GroupBy(c => c.Event.GetRequestInfo().ClientIpAddress);
            foreach (var clientIpAddressGroup in clientIpAddressGroups) {
                if (clientIpAddressGroup.Key.IsPrivateNetwork())
                    return;

                var clientIpContexts = clientIpAddressGroup.ToList();

                string throttleCacheKey = String.Concat("bot:", clientIpAddressGroup.Key, ":", SystemClock.UtcNow.Floor(_throttlingPeriod).Ticks);
                var requestCount = await _cache.GetAsync<int?>(throttleCacheKey, null).AnyContext();
                if (requestCount.HasValue) {
                    await _cache.IncrementAsync(throttleCacheKey, clientIpContexts.Count).AnyContext();
                    requestCount += clientIpContexts.Count;
                } else {
                    await _cache.SetAsync(throttleCacheKey, clientIpContexts.Count, SystemClock.UtcNow.Ceiling(_throttlingPeriod)).AnyContext();
                    requestCount = clientIpContexts.Count;
                }

                if (requestCount < Settings.Current.BotThrottleLimit)
                    return;

                _logger.LogInformation("Bot throttle triggered. IP: {IP} Time: {ThrottlingPeriod} Project: {project}", clientIpAddressGroup.Key, SystemClock.UtcNow.Floor(_throttlingPeriod), firstContext.Event.ProjectId);

                // The throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
                await _workItemQueue.EnqueueAsync(new ThrottleBotsWorkItem {
                    OrganizationId = firstContext.Event.OrganizationId,
                    ClientIpAddress = clientIpAddressGroup.Key,
                    UtcStartDate = SystemClock.UtcNow.Floor(_throttlingPeriod),
                    UtcEndDate = SystemClock.UtcNow.Ceiling(_throttlingPeriod)
                }).AnyContext();

                clientIpContexts.ForEach(c => c.Event.IsHidden = true);
            }
        }
    }
}