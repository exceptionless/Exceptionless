using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.WebHook;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Foundatio.Queues;
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
            return;

        if (ctx.Stack is null || !ctx.Stack.AllowNotifications)
            return;

        if (ShouldQueueNotification(ctx))
            await _notificationQueue.EnqueueAsync(new EventNotification
            {
                EventId = ctx.Event.Id,
                IsNew = ctx.IsNew,
                IsRegression = ctx.IsRegression,
                TotalOccurrences = ctx.Stack.TotalOccurrences
            });

        var webHooks = await _webHookRepository.GetByOrganizationIdOrProjectIdAsync(ctx.Event.OrganizationId, ctx.Event.ProjectId);
        foreach (var hook in webHooks.Documents)
        {
            if (!ShouldCallWebHook(hook, ctx))
                continue;

            var context = new WebHookDataContext(hook, ctx.Organization, ctx.Project, ctx.Stack, ctx.Event, ctx.IsNew, ctx.IsRegression);
            var notification = new WebHookNotification
            {
                OrganizationId = ctx.Event.OrganizationId,
                ProjectId = ctx.Event.ProjectId,
                WebHookId = hook.Id,
                Url = hook.Url,
                Type = WebHookType.General,
                Data = await _webHookDataPluginManager.CreateFromEventAsync(context)
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
            return false;

        if (!String.IsNullOrEmpty(hook.ProjectId) && !String.Equals(ctx.Project.Id, hook.ProjectId))
            return false;

        if (ctx.IsNew && ctx.Event.IsError() && hook.EventTypes.Contains(WebHook.KnownEventTypes.NewError))
            return true;

        if (ctx.Event.IsCritical() && ctx.Event.IsError() && hook.EventTypes.Contains(WebHook.KnownEventTypes.CriticalError))
            return true;

        if (ctx.IsRegression && hook.EventTypes.Contains(WebHook.KnownEventTypes.StackRegression))
            return true;

        if (ctx.IsNew && hook.EventTypes.Contains(WebHook.KnownEventTypes.NewEvent))
            return true;

        if (ctx.Event.IsCritical() && hook.EventTypes.Contains(WebHook.KnownEventTypes.CriticalEvent))
            return true;

        return false;
    }

    private bool ShouldQueueNotification(EventContext ctx)
    {
        if (ctx.Project.NotificationSettings.Count == 0)
            return false;

        if (ctx.IsNew && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewErrors))
            return true;

        if (ctx.Event.IsCritical() && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalErrors))
            return true;

        if (ctx.IsRegression && ctx.Project.NotificationSettings.Any(n => n.Value.ReportEventRegressions))
            return true;

        if (ctx.IsNew && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewEvents))
            return true;

        if (ctx.Event.IsCritical() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalEvents))
            return true;

        return false;
    }
}
