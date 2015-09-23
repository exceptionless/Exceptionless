using System;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(0)]
    public class ThrottleBotsPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private readonly IMetricsClient _metricsClient;
        private readonly IEventRepository _eventRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IQueue<WorkItemData> _workItemQueue;
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        public ThrottleBotsPlugin(ICacheClient cacheClient, IEventRepository eventRepository, IProjectRepository projectRepository, IMetricsClient metricsClient, IQueue<WorkItemData> workItemQueue) {
            _cacheClient = cacheClient;
            _metricsClient = metricsClient;
            _eventRepository = eventRepository;
            _projectRepository = projectRepository;
            _workItemQueue = workItemQueue;
        }

        public override async Task EventProcessingAsync(EventContext context) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Dev)
                return;

            var project = await _projectRepository.GetByIdAsync(context.Event.ProjectId).AnyContext();
            if (project == null || !project.DeleteBotDataEnabled)
                return;

            // Throttle errors by client ip address to no more than X every 5 minutes.
            var ri = context.Event.GetRequestInfo();
            if (ri == null || String.IsNullOrEmpty(ri.ClientIpAddress))
                return;

            if (ri.ClientIpAddress.IsPrivateNetwork())
                return;

            string throttleCacheKey = String.Concat("bot:", ri.ClientIpAddress, ":", DateTime.Now.Floor(_throttlingPeriod).Ticks);
            var requestCount = await _cacheClient.GetAsync<int?>(throttleCacheKey).AnyContext();
            if (requestCount != null) {
                await _cacheClient.IncrementAsync(throttleCacheKey, 1).AnyContext();
                requestCount++;
            } else {
                await _cacheClient.SetAsync(throttleCacheKey, 1, _throttlingPeriod).AnyContext();
                requestCount = 1;
            }

            if (requestCount < Settings.Current.BotThrottleLimit)
                return;

            await _metricsClient.CounterAsync(MetricNames.EventsBotThrottleTriggered).AnyContext();
            Log.Info().Message("Bot throttle triggered. IP: {0} Time: {1} Project: {2}", ri.ClientIpAddress, DateTime.Now.Floor(_throttlingPeriod), context.Event.ProjectId).Project(context.Event.ProjectId).Write();
            
            // the throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
            await _workItemQueue.EnqueueAsync(new ThrottleBotsWorkItem {
                OrganizationId = context.Event.OrganizationId,
                ClientIpAddress = ri.ClientIpAddress,
                UtcStartDate = DateTime.UtcNow.Floor(_throttlingPeriod),
                UtcEndDate = DateTime.UtcNow.Ceiling(_throttlingPeriod)
            }).AnyContext();

            context.IsCancelled = true;
        }
    }
}