using System;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Metrics;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(0)]
    public class ThrottleBotsPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private readonly IMetricsClient _metricsClient;
        private readonly IEventRepository _eventRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        public ThrottleBotsPlugin(ICacheClient cacheClient, IEventRepository eventRepository, IProjectRepository projectRepository, IMetricsClient metricsClient) {
            _cacheClient = cacheClient;
            _metricsClient = metricsClient;
            _eventRepository = eventRepository;
            _projectRepository = projectRepository;
        }

        public override async Task EventProcessingAsync(EventContext context) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Dev)
                return;

            var project = _projectRepository.GetById(context.Event.ProjectId);
            if (project == null || !project.DeleteBotDataEnabled)
                return;

            // Throttle errors by client ip address to no more than X every 5 minutes.
            var ri = context.Event.GetRequestInfo();
            if (ri == null || String.IsNullOrEmpty(ri.ClientIpAddress))
                return;

            if (ri.ClientIpAddress.IsPrivateNetwork())
                return;

            string throttleCacheKey = String.Concat("bot:", ri.ClientIpAddress, ":", DateTime.Now.Floor(_throttlingPeriod).Ticks);
            var requestCount = _cacheClient.Get<int?>(throttleCacheKey);
            if (requestCount != null) {
                _cacheClient.Increment(throttleCacheKey, 1);
                requestCount++;
            } else {
                _cacheClient.Set(throttleCacheKey, 1, _throttlingPeriod);
                requestCount = 1;
            }

            if (requestCount < Settings.Current.BotThrottleLimit)
                return;

            await _metricsClient.CounterAsync(MetricNames.EventsBotThrottleTriggered);
            Log.Info().Message("Bot throttle triggered. IP: {0} Time: {1} Project: {2}", ri.ClientIpAddress, DateTime.Now.Floor(_throttlingPeriod), context.Event.ProjectId).Project(context.Event.ProjectId).Write();
            
            // TODO: We should kick this off into a long running task.
            // the throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
            _eventRepository.HideAllByClientIpAndDateAsync(context.Event.OrganizationId, ri.ClientIpAddress, DateTime.Now.Floor(_throttlingPeriod), DateTime.Now.Ceiling(_throttlingPeriod));
            context.IsCancelled = true;
        }
    }
}