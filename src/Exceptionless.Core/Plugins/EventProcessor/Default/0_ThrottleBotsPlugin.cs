using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(0)]
public sealed class ThrottleBotsPlugin : EventProcessorPluginBase
{
    private readonly ICacheClient _cache;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

    public ThrottleBotsPlugin(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue,
        TimeProvider timeProvider, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _cache = cacheClient;
        _workItemQueue = workItemQueue;
        _timeProvider = timeProvider;
    }

    public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts)
    {
        if (_options.AppMode == AppMode.Development)
            return;

        var firstContext = contexts.First();
        if (!firstContext.Project.DeleteBotDataEnabled || !firstContext.IncludePrivateInformation)
            return;

        // Throttle errors by client ip address to no more than X every 5 minutes.
        var clientIpAddressGroups = contexts.GroupBy(c => c.Event.GetRequestInfo()?.ClientIpAddress);
        foreach (var clientIpAddressGroup in clientIpAddressGroups)
        {
            if (String.IsNullOrEmpty(clientIpAddressGroup.Key) || clientIpAddressGroup.Key.IsPrivateNetwork())
                continue;

            var clientIpContexts = clientIpAddressGroup.ToList();
            string throttleCacheKey = String.Concat("bot:", clientIpAddressGroup.Key, ":", _timeProvider.GetUtcNow().UtcDateTime.Floor(_throttlingPeriod).Ticks);
            int? requestCount = await _cache.GetAsync<int?>(throttleCacheKey, null);
            if (requestCount.HasValue)
            {
                await _cache.IncrementAsync(throttleCacheKey, clientIpContexts.Count);
                requestCount += clientIpContexts.Count;
            }
            else
            {
                await _cache.SetAsync(throttleCacheKey, clientIpContexts.Count, _timeProvider.GetUtcNow().UtcDateTime.Ceiling(_throttlingPeriod));
                requestCount = clientIpContexts.Count;
            }

            if (requestCount < _options.BotThrottleLimit)
                continue;

            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            _logger.LogInformation("Bot throttle triggered. IP: {IP} Time: {ThrottlingPeriod} Project: {ProjectId}", clientIpAddressGroup.Key, utcNow.Floor(_throttlingPeriod), firstContext.Event.ProjectId);

            // The throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
            await _workItemQueue.EnqueueAsync(new RemoveBotEventsWorkItem
            {
                OrganizationId = firstContext.Event.OrganizationId,
                ProjectId = firstContext.Event.ProjectId,
                ClientIpAddress = clientIpAddressGroup.Key,
                UtcStartDate = utcNow.Floor(_throttlingPeriod),
                UtcEndDate = utcNow.Ceiling(_throttlingPeriod)
            });

            clientIpContexts.ForEach(c =>
            {
                c.IsDiscarded = true;
                c.IsCancelled = true;
            });
        }
    }
}
