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
using Foundatio.Lock;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Storage;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public sealed class EventIngestionSideEffectsWorkItemHandler(
    IOrganizationRepository organizationRepository,
    IProjectRepository projectRepository,
    IEventRepository eventRepository,
    IStackRepository stackRepository,
    IIngestionStackUsageStore ingestionStackUsageStore,
    RequestInfoPlugin requestInfoPlugin,
    EnvironmentInfoPlugin environmentInfoPlugin,
    GeoPlugin geoPlugin,
    QueueNotificationAction queueNotificationAction,
    IngestionSideEffectExecutor sideEffectExecutor,
    IQueue<WorkItemData> workItemQueue,
    IFileStorage storage,
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
        {
            return;
        }

        var events = await eventRepository.GetByIdsAsync(workItem.EventIds);
        if (events.Count == 0)
        {
            return;
        }

        if (options.EnableArchive)
        {
            DateTime archiveDate = events.Min(ev => ev.CreatedUtc);
            string archivePath = GetArchivePath(workItem, archiveDate);
            bool archived = await storage.SaveObjectAsync(archivePath, events.OrderBy(ev => ev.Id).ToArray(), context.CancellationToken);
            if (!archived)
            {
                throw new InvalidOperationException($"Unable to archive V3 ingestion side-effect batch '{workItem.UniqueIdentifier}'.");
            }
        }

        var stacks = await stackRepository.GetByIdsAsync(events.Select(e => e.StackId).Distinct().ToArray());
        var stacksById = stacks.ToDictionary(stack => stack.Id);
        var contexts = new List<EventContext>(events.Count);
        foreach (var ev in events)
        {
            if (!stacksById.TryGetValue(ev.StackId, out var stack))
            {
                continue;
            }

            contexts.Add(new EventContext(ev, organization, project)
            {
                Stack = stack,
                IsNew = ev.IsFirstOccurrence,
                IsRegression = ev.IsRegression,
                IsIngestionV3 = true,
                IsProcessed = true
            });
        }

        if (contexts.Count == 0)
        {
            return;
        }

        if (!project.IsConfigured.GetValueOrDefault())
        {
            await sideEffectExecutor.ExecuteAsync(IngestionSideEffectExecutor.ProjectConfiguredStage, project.Id, [project.Id], async _ =>
            {
                await workItemQueue.EnqueueAsync(new SetProjectIsConfiguredWorkItem
                {
                    ProjectId = project.Id,
                    IsConfigured = true
                });
            }, context.CancellationToken);
        }

        var enrichmentContexts = contexts
            .Where(eventContext => eventContext.Event.Data?.ContainsKey(Event.KnownDataKeys.RequestInfo) is true
                || eventContext.Event.Data?.ContainsKey(Event.KnownDataKeys.EnvironmentInfo) is true)
            .ToList();
        if (enrichmentContexts.Count > 0)
        {
            await requestInfoPlugin.EventBatchProcessingAsync(enrichmentContexts);
            foreach (var eventContext in enrichmentContexts)
            {
                await environmentInfoPlugin.EventProcessingAsync(eventContext);
            }

            await geoPlugin.EventBatchProcessingAsync(enrichmentContexts);
            await eventRepository.SaveAsync(enrichmentContexts.Select(eventContext => eventContext.Event), o => o.Notifications(false));
        }

        var settledStackUsages = await ingestionStackUsageStore.SettleAsync(
            contexts.Select(eventContext => new IngestionStackUsage(
                eventContext.Event.Id,
                organization.Id,
                project.Id,
                eventContext.Event.StackId,
                eventContext.Event.Date.UtcDateTime)).ToArray(),
            context.CancellationToken);
        foreach (var usage in settledStackUsages)
        {
            if (stacksById.TryGetValue(usage.StackId, out var stack))
            {
                stack.TotalOccurrences += usage.Count;
                if (stack.FirstOccurrence > usage.MinimumOccurrenceDateUtc)
                {
                    stack.FirstOccurrence = usage.MinimumOccurrenceDateUtc;
                }

                if (stack.LastOccurrence < usage.MaximumOccurrenceDateUtc)
                {
                    stack.LastOccurrence = usage.MaximumOccurrenceDateUtc;
                }
            }
        }

        var notificationContextsById = contexts.ToDictionary(eventContext => eventContext.Event.Id, StringComparer.Ordinal);
        await sideEffectExecutor.ExecuteAsync(IngestionSideEffectExecutor.TerminalStage, project.Id, notificationContextsById.Keys.ToArray(), async pendingIds =>
        {
            var pendingContexts = pendingIds.Select(id => notificationContextsById[id]).ToArray();
            await queueNotificationAction.ProcessIngestionV3BatchAsync(pendingContexts);
        }, context.CancellationToken);
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
