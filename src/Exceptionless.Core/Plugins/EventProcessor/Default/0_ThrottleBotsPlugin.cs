using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Plugins.EventProcessor;

[Priority(0)]
public sealed class ThrottleBotsPlugin : EventProcessorPluginBase
{
    private readonly ICacheClient _cache;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly TimeProvider _timeProvider;
    private readonly ITextSerializer _serializer;
    private readonly TimeSpan _throttlingPeriod = TimeSpan.FromMinutes(5);

    public ThrottleBotsPlugin(ICacheClient cacheClient, IQueue<WorkItemData> workItemQueue,
        ITextSerializer serializer, TimeProvider timeProvider, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _cache = cacheClient;
        _workItemQueue = workItemQueue;
        _serializer = serializer;
        _timeProvider = timeProvider;
    }

    private static string CacheKey(string organizationId, string projectId, string clientIpAddress, long period) =>
        String.Concat("Organization:", organizationId, ":Project:", projectId, ":bot:", period, ":", clientIpAddress);

    public override async Task EventBatchProcessingAsync(ICollection<EventContext> contexts)
    {
        if (_options.AppMode == AppMode.Development)
            return;

        // Keep each project's client IP counters and cleanup tasks isolated.
        var clientIpAddressGroups = contexts
            .Where(c => c.Project.DeleteBotDataEnabled && c.IncludePrivateInformation)
            .GroupBy(c => new
            {
                c.Event.OrganizationId,
                c.Event.ProjectId,
                ClientIpAddress = c.Event.GetRequestInfo(_serializer, _logger)?.ClientIpAddress
            });
        foreach (var clientIpAddressGroup in clientIpAddressGroups)
        {
            var scope = clientIpAddressGroup.Key;
            if (String.IsNullOrEmpty(scope.ClientIpAddress) || scope.ClientIpAddress.IsPrivateNetwork())
            {
                continue;
            }

            var clientIpContexts = clientIpAddressGroup.ToList();
            string throttleCacheKey = CacheKey(scope.OrganizationId, scope.ProjectId, scope.ClientIpAddress, _timeProvider.GetUtcNow().UtcDateTime.Floor(_throttlingPeriod).Ticks);
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
            _logger.LogInformation("Bot throttle triggered. IP: {IP} Time: {ThrottlingPeriod} Organization: {OrganizationId} Project: {ProjectId}", scope.ClientIpAddress, utcNow.Floor(_throttlingPeriod), scope.OrganizationId, scope.ProjectId);

            // The throttle was triggered, go and delete all the errors that triggered the throttle to reduce bot noise in the system
            await _workItemQueue.EnqueueAsync(new RemoveBotEventsWorkItem
            {
                OrganizationId = scope.OrganizationId,
                ProjectId = scope.ProjectId,
                ClientIpAddress = scope.ClientIpAddress,
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
