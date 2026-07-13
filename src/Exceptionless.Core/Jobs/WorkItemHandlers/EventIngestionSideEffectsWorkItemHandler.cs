using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventProcessor.Default;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Foundatio.Jobs;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public sealed class EventIngestionSideEffectsWorkItemHandler(
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IEventRepository eventRepository,
    IStackRepository stackRepository,
    StackService stackService,
    RequestInfoPlugin requestInfoPlugin,
    EnvironmentInfoPlugin environmentInfoPlugin,
    GeoPlugin geoPlugin,
    QueueNotificationAction queueNotificationAction,
    RunEventProcessedPluginsAction runEventProcessedPluginsAction,
    IFileStorage storage,
    ICacheClient cache,
    AppOptions options,
    ILockProvider lockProvider,
    ILoggerFactory loggerFactory) : WorkItemHandlerBase(loggerFactory)
{
    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        var sideEffects = (EventIngestionSideEffectsWorkItem)workItem;
        return lockProvider.TryAcquireAsync(sideEffects.UniqueIdentifier, TimeSpan.FromMinutes(5), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<EventIngestionSideEffectsWorkItem>()!;
        var organization = await organizationRepository.GetByIdAsync(workItem.OrganizationId, o => o.Cache());
        var project = await projectRepository.GetByIdAsync(workItem.ProjectId, o => o.Cache());
        if (organization is null || project is null)
            return;

        var events = await eventRepository.GetByIdsAsync(workItem.EventIds);
        if (events.Count == 0)
            return;

        if (options.EnableArchive)
        {
            DateTime archiveDate = events.Min(ev => ev.CreatedUtc);
            string archivePath = GetArchivePath(workItem, archiveDate);
            bool archived = await storage.SaveObjectAsync(archivePath, events.OrderBy(ev => ev.Id).ToArray(), context.CancellationToken);
            if (!archived)
                throw new InvalidOperationException($"Unable to archive V3 ingestion side-effect batch '{workItem.UniqueIdentifier}'.");
        }

        var stacks = await stackRepository.GetByIdsAsync(events.Select(e => e.StackId).Distinct().ToArray());
        var stacksById = stacks.ToDictionary(stack => stack.Id);
        var contexts = new List<EventContext>(events.Count);
        foreach (var ev in events)
        {
            if (!stacksById.TryGetValue(ev.StackId, out var stack))
                continue;

            contexts.Add(new EventContext(ev, organization, project)
            {
                Stack = stack,
                IsNew = ev.IsFirstOccurrence,
                IsProcessed = true
            });
        }

        if (contexts.Count == 0)
            return;

        var enrichmentContexts = contexts
            .Where(eventContext => eventContext.Event.Data?.ContainsKey(Event.KnownDataKeys.RequestInfo) is true
                || eventContext.Event.Data?.ContainsKey(Event.KnownDataKeys.EnvironmentInfo) is true)
            .ToList();
        if (enrichmentContexts.Count > 0)
        {
            await requestInfoPlugin.EventBatchProcessingAsync(enrichmentContexts);
            foreach (var eventContext in enrichmentContexts)
                await environmentInfoPlugin.EventProcessingAsync(eventContext);
            await geoPlugin.EventBatchProcessingAsync(enrichmentContexts);
            await eventRepository.SaveAsync(enrichmentContexts.Select(eventContext => eventContext.Event), o => o.Notifications(false));
        }

        foreach (var stackGroup in contexts.GroupBy(c => c.Event.StackId))
        {
            var claimedContexts = new List<(EventContext Context, string Key)>();
            foreach (var eventContext in stackGroup)
            {
                string statisticsKey = String.Concat("ingest-v3:sideeffects:statistics:", eventContext.Event.Id);
                if (await cache.AddAsync(statisticsKey, true, options.EventIngestionV3.IdempotencyWindow))
                    claimedContexts.Add((eventContext, statisticsKey));
            }

            if (claimedContexts.Count == 0)
                continue;

            try
            {
                DateTime minimum = claimedContexts.Min(item => item.Context.Event.Date.UtcDateTime);
                DateTime maximum = claimedContexts.Max(item => item.Context.Event.Date.UtcDateTime);
                await stackService.IncrementStackUsageAsync(organization.Id, project.Id, stackGroup.Key, minimum, maximum, claimedContexts.Count);
                if (stacksById.TryGetValue(stackGroup.Key, out var stack))
                {
                    stack.TotalOccurrences += claimedContexts.Count;
                    if (stack.FirstOccurrence > minimum)
                        stack.FirstOccurrence = minimum;
                    if (stack.LastOccurrence < maximum)
                        stack.LastOccurrence = maximum;
                }
            }
            catch
            {
                await cache.RemoveAllAsync(claimedContexts.Select(item => item.Key));
                throw;
            }
        }

        foreach (var eventContext in contexts)
        {
            string notificationKey = String.Concat("ingest-v3:sideeffects:notifications:", eventContext.Event.Id);
            if (!await cache.AddAsync(notificationKey, true, options.EventIngestionV3.IdempotencyWindow))
                continue;

            try
            {
                await queueNotificationAction.ProcessAsync(eventContext);
            }
            catch
            {
                await cache.RemoveAsync(notificationKey);
                throw;
            }
        }

        var pluginContexts = new List<(EventContext Context, string Key)>();
        foreach (var eventContext in contexts)
        {
            string pluginKey = String.Concat("ingest-v3:sideeffects:plugins:", eventContext.Event.Id);
            if (await cache.AddAsync(pluginKey, true, options.EventIngestionV3.IdempotencyWindow))
                pluginContexts.Add((eventContext, pluginKey));
        }

        if (pluginContexts.Count == 0)
            return;

        try
        {
            await runEventProcessedPluginsAction.ProcessBatchAsync(pluginContexts.Select(item => item.Context).ToList());
        }
        catch
        {
            await cache.RemoveAllAsync(pluginContexts.Select(item => item.Key));
            throw;
        }
    }

    private static string GetArchivePath(EventIngestionSideEffectsWorkItem workItem, DateTime createdUtc)
    {
        return Path.Combine(
            "archive",
            "v3",
            createdUtc.ToString("yy"),
            createdUtc.ToString("MM"),
            createdUtc.ToString("dd"),
            createdUtc.ToString("HH"),
            createdUtc.ToString("mm"),
            workItem.ProjectId,
            String.Concat(GetWorkItemHash(workItem), ".json"));
    }

    private static string GetWorkItemHash(EventIngestionSideEffectsWorkItem workItem)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(workItem.UniqueIdentifier))).ToLowerInvariant();
    }
}
