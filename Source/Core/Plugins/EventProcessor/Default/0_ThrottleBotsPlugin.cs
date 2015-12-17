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
using Foundatio.Logging;
using Foundatio.Queues;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(0)]
    public class ThrottleBotsPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        public ThrottleBotsPlugin(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue) {
            _cacheClient = cacheClient;
            _workItemQueue = workItemQueue;
        }

        public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Dev)
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

                string throttleCacheKey = String.Concat("bot:", clientIpAddressGroup.Key, ":", DateTime.UtcNow.Floor(_throttlingPeriod).Ticks);
                var requestCount = await _cacheClient.GetAsync<int?>(throttleCacheKey, null).AnyContext();
                if (requestCount.HasValue) {
                    await _cacheClient.IncrementAsync(throttleCacheKey, clientIpContexts.Count).AnyContext();
                    requestCount += clientIpContexts.Count;
                } else {
                    await _cacheClient.SetAsync(throttleCacheKey, clientIpContexts.Count, DateTime.UtcNow.Ceiling(_throttlingPeriod)).AnyContext();
                    requestCount = clientIpContexts.Count;
                }

                if (requestCount < Settings.Current.BotThrottleLimit)
                    return;

                Logger.Info().Message("Bot throttle triggered. IP: {0} Time: {1} Project: {2}", clientIpAddressGroup.Key, DateTime.UtcNow.Floor(_throttlingPeriod), firstContext.Event.ProjectId).Project(firstContext.Event.ProjectId).Write();

                // The throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
                await _workItemQueue.EnqueueAsync(new ThrottleBotsWorkItem {
                    OrganizationId = firstContext.Event.OrganizationId,
                    ClientIpAddress = clientIpAddressGroup.Key,
                    UtcStartDate = DateTime.UtcNow.Floor(_throttlingPeriod),
                    UtcEndDate = DateTime.UtcNow.Ceiling(_throttlingPeriod)
                }).AnyContext();

                clientIpContexts.ForEach(c => c.IsCancelled = true);
            }
        }
    }
}