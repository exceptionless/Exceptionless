using System;
using System.Threading.Tasks;
using CodeSmith.Core.Component;
using CodeSmith.Core.Extensions;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Repositories;
using NLog.Fluent;

namespace Exceptionless.Core.Plugins.EventProcessor {
    [Priority(0)]
    public class ThrottleBotsPlugin : EventProcessorPluginBase {
        private readonly ICacheClient _cacheClient;
        private readonly IEventRepository _eventRepository;
        private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

        public ThrottleBotsPlugin(ICacheClient cacheClient, IEventRepository eventRepository) {
            _cacheClient = cacheClient;
            _eventRepository = eventRepository;
        }

        public override void EventProcessing(EventContext context) {
            if (Settings.Current.WebsiteMode == WebsiteMode.Dev)
                return;

            // Throttle errors by client ip address to no more than X every 5 minutes.
            var ri = context.Event.GetRequestInfo();
            if (ri == null || String.IsNullOrEmpty(ri.ClientIpAddress))
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

            Log.Info().Message("Bot throttle triggered. IP: {0} Time: {1} Project: {2}", ri.ClientIpAddress, DateTime.Now.Floor(_throttlingPeriod), context.Event.ProjectId).Project(context.Event.ProjectId).Write();
            // the throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
            Task.Run(() => _eventRepository.RemoveAllByClientIpAndDateAsync(ri.ClientIpAddress, DateTime.Now.Floor(_throttlingPeriod), DateTime.Now.Ceiling(_throttlingPeriod)));
            context.IsCancelled = true;
        }
    }
}