using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Queues;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Pipeline;

[Priority(70)]
public class QueueNotificationAction : EventPipelineActionBase
{
    private readonly IQueue<EventNotification> _notificationQueue;
    private readonly IQueue<WebHookNotification> _webHookNotificationQueue;
    private readonly IWebHookRepository _webHookRepository;
    private readonly WebHookDataPluginManager _webHookDataPluginManager;

    public QueueNotificationAction(IQueue<EventNotification> notificationQueue, IQueue<WebHookNotification> webHookNotificationQueue, IWebHookRepository webHookRepository, WebHookDataPluginManager webHookDataPluginManager, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _notificationQueue = notificationQueue;
        _webHookNotificationQueue = webHookNotificationQueue;
        _webHookRepository = webHookRepository;
        _webHookDataPluginManager = webHookDataPluginManager;
        ContinueOnError = true;
    }

    public override async Task ProcessAsync(EventContext ctx)
    {
        // if they don't have premium features, then we don't need to queue notifications
        if (!ctx.Organization.HasPremiumFeatures)
        {
            return;
        }

        if (ctx.Stack is null || !ctx.Stack.AllowNotifications)
        {
            return;
        }

        if (ShouldQueueNotification(ctx))
        {
            await QueueEventNotificationAsync(ctx);
        }

        var webHooks = await GetWebHooksAsync(ctx.Event.OrganizationId, ctx.Event.ProjectId);
        await QueueWebHooksAsync(ctx, webHooks.Documents);
    }

    /// <summary>
    /// Queues V3 notifications as a bounded, sequential batch. Webhooks are loaded once for each
    /// organization/project pair and queue failures are allowed to escape so the side-effect worker
    /// can retry the batch. Deterministic queue identifiers suppress already-enqueued work on retry.
    /// </summary>
    public async Task ProcessIngestionV3BatchAsync(IReadOnlyCollection<EventContext> contexts)
    {
        ArgumentNullException.ThrowIfNull(contexts);

        var eligibleContexts = contexts
            .Where(ctx => ctx.Organization.HasPremiumFeatures && ctx.Stack?.AllowNotifications is true)
            .ToArray();

        foreach (var group in eligibleContexts.GroupBy(ctx => (ctx.Event.OrganizationId, ctx.Event.ProjectId)))
        {
            var webHooks = await GetWebHooksAsync(group.Key.OrganizationId, group.Key.ProjectId);
            foreach (var ctx in group)
            {
                if (ShouldQueueNotification(ctx))
                {
                    await QueueEventNotificationAsync(ctx);
                }

                await QueueWebHooksAsync(ctx, webHooks.Documents);
            }
        }
    }

    private Task<FindResults<WebHook>> GetWebHooksAsync(string organizationId, string projectId)
    {
        return _webHookRepository.GetByOrganizationIdOrProjectIdAsync(organizationId, projectId);
    }

    private Task QueueEventNotificationAsync(EventContext ctx)
    {
        return _notificationQueue.EnqueueAsync(new EventNotification
        {
            EventId = ctx.Event.Id,
            IsNew = ctx.IsNew,
            IsRegression = ctx.IsRegression,
            TotalOccurrences = ctx.Stack!.TotalOccurrences,
            DeduplicationId = String.Concat("event-notification:", ctx.Event.Id),
            UseDurableDeduplication = ctx.IsIngestionV3
        });
    }

    private async Task QueueWebHooksAsync(EventContext ctx, IReadOnlyCollection<WebHook> webHooks)
    {
        foreach (var hook in webHooks)
        {
            if (!ShouldCallWebHook(hook, ctx))
            {
                continue;
            }

            var context = new WebHookDataContext(hook, ctx.Organization, ctx.Project, ctx.Stack!, ctx.Event, ctx.IsNew, ctx.IsRegression);
            var notification = new WebHookNotification
            {
                OrganizationId = ctx.Event.OrganizationId,
                ProjectId = ctx.Event.ProjectId,
                WebHookId = hook.Id,
                Url = hook.Url,
                Type = WebHookType.General,
                Data = await _webHookDataPluginManager.CreateFromEventAsync(context),
                DeduplicationId = String.Concat("event-webhook:", ctx.Event.Id, ":", hook.Id, ":", WebHookType.General),
                UseDurableDeduplication = ctx.IsIngestionV3
            };

            if (notification.Data is null)
            {
                _logger.LogTrace("Skipping Web hook: invalid data payload: project={ProjectId} url={Url}", ctx.Event.ProjectId, hook.Url);
                continue;
            }

            await _webHookNotificationQueue.EnqueueAsync(notification);
            _logger.LogTrace("Web hook queued: project={ProjectId} url={Url}", ctx.Event.ProjectId, hook.Url);
        }
    }

    private bool ShouldCallWebHook(WebHook hook, EventContext ctx)
    {
        if (!hook.IsEnabled)
        {
            return false;
        }

        if (!String.IsNullOrEmpty(hook.ProjectId) && !String.Equals(ctx.Project.Id, hook.ProjectId))
        {
            return false;
        }

        if (ctx.IsNew && ctx.Event.IsError() && hook.EventTypes.Contains(WebHook.KnownEventTypes.NewError))
        {
            return true;
        }

        if (ctx.Event.IsCritical() && ctx.Event.IsError() && hook.EventTypes.Contains(WebHook.KnownEventTypes.CriticalError))
        {
            return true;
        }

        if (ctx.IsRegression && hook.EventTypes.Contains(WebHook.KnownEventTypes.StackRegression))
        {
            return true;
        }

        if (ctx.IsNew && hook.EventTypes.Contains(WebHook.KnownEventTypes.NewEvent))
        {
            return true;
        }

        if (ctx.Event.IsCritical() && hook.EventTypes.Contains(WebHook.KnownEventTypes.CriticalEvent))
        {
            return true;
        }

        return false;
    }

    private bool ShouldQueueNotification(EventContext ctx)
    {
        if (ctx.Project.NotificationSettings.Count == 0)
        {
            return false;
        }

        if (ctx.IsNew && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewErrors))
        {
            return true;
        }

        if (ctx.Event.IsCritical() && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalErrors))
        {
            return true;
        }

        if (ctx.IsRegression && ctx.Project.NotificationSettings.Any(n => n.Value.ReportEventRegressions))
        {
            return true;
        }

        if (ctx.IsNew && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewEvents))
        {
            return true;
        }

        if (ctx.Event.IsCritical() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalEvents))
        {
            return true;
        }

        return false;
    }
}
