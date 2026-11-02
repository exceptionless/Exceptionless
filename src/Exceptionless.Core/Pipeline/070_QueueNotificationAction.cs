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
    private readonly IStackRepository _stackRepository;
    private readonly WebHookDataPluginManager _webHookDataPluginManager;

    public QueueNotificationAction(IQueue<EventNotification> notificationQueue, IQueue<WebHookNotification> webHookNotificationQueue, IWebHookRepository webHookRepository, IStackRepository stackRepository, WebHookDataPluginManager webHookDataPluginManager, AppOptions options, ILoggerFactory loggerFactory) : base(options, loggerFactory)
    {
        _notificationQueue = notificationQueue;
        _webHookNotificationQueue = webHookNotificationQueue;
        _webHookRepository = webHookRepository;
        _stackRepository = stackRepository;
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

        // Every notification for every channel (email, Slack, web hooks) requires a notifying environment.
        if (!ctx.Project.ShouldNotifyForEnvironment(ctx.Event.Environment))
            return;

        bool isNew = await IsNewForNotificationAsync(ctx);
        bool isRegression = ctx.IsRegression || await ClaimPendingAsync(ctx, PendingStackNotification.Regression);
        if (ShouldQueueNotification(ctx, isNew, isRegression))
            await _notificationQueue.EnqueueAsync(new EventNotification
            {
                EventId = ctx.Event.Id,
                IsNew = isNew,
                IsRegression = isRegression,
                TotalOccurrences = ctx.Stack.TotalOccurrences
            });

        var webHooks = await _webHookRepository.GetByOrganizationIdOrProjectIdAsync(ctx.Event.OrganizationId, ctx.Event.ProjectId);
        foreach (var hook in webHooks.Documents)
        {
            if (!ShouldCallWebHook(hook, ctx, isNew, isRegression))
                continue;

            var context = new WebHookDataContext(hook, ctx.Organization, ctx.Project, ctx.Stack, ctx.Event, isNew, isRegression);
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

    /// <summary>
    /// A stack is new for notifications on its first occurrence in a notifying environment. That is the occurrence that created it,
    /// or the first one after it was created in an environment that does not notify. Stacks created before environments were managed
    /// are never pending, so they are never reported as new again.
    /// </summary>
    private async Task<bool> IsNewForNotificationAsync(EventContext ctx)
    {
        if (ctx.IsNew)
            return true;

        return await ClaimPendingAsync(ctx, PendingStackNotification.New);
    }

    /// <summary>
    /// The repository clears the flag atomically, so concurrent events can only produce one notification.
    /// </summary>
    private async Task<bool> ClaimPendingAsync(EventContext ctx, PendingStackNotification kind)
    {
        bool isPending = kind == PendingStackNotification.New ? ctx.Stack!.IsNewNotificationPending : ctx.Stack!.IsRegressionNotificationPending;
        if (!isPending)
            return false;

        bool claimed = await _stackRepository.TryClaimNotificationAsync(ctx.Stack, kind);
        if (kind == PendingStackNotification.New)
            ctx.Stack.IsNewNotificationPending = false;
        else
            ctx.Stack.IsRegressionNotificationPending = false;

        return claimed;
    }

    private bool ShouldCallWebHook(WebHook hook, EventContext ctx, bool isNew, bool isRegression)
    {
        if (!hook.IsEnabled)
            return false;

        if (!String.IsNullOrEmpty(hook.ProjectId) && !String.Equals(ctx.Project.Id, hook.ProjectId))
            return false;

        if (isNew && ctx.Event.IsError() && hook.EventTypes.Contains(WebHook.KnownEventTypes.NewError))
            return true;

        if (ctx.Event.IsCritical() && ctx.Event.IsError() && hook.EventTypes.Contains(WebHook.KnownEventTypes.CriticalError))
            return true;

        if (isRegression && hook.EventTypes.Contains(WebHook.KnownEventTypes.StackRegression))
            return true;

        if (isNew && hook.EventTypes.Contains(WebHook.KnownEventTypes.NewEvent))
            return true;

        if (ctx.Event.IsCritical() && hook.EventTypes.Contains(WebHook.KnownEventTypes.CriticalEvent))
            return true;

        return false;
    }

    private bool ShouldQueueNotification(EventContext ctx, bool isNew, bool isRegression)
    {
        if (ctx.Project.NotificationSettings.Count == 0)
            return false;

        if (isNew && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewErrors))
            return true;

        if (ctx.Event.IsCritical() && ctx.Event.IsError() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalErrors))
            return true;

        if (isRegression && ctx.Project.NotificationSettings.Any(n => n.Value.ReportEventRegressions))
            return true;

        if (isNew && ctx.Project.NotificationSettings.Any(n => n.Value.ReportNewEvents))
            return true;

        if (ctx.Event.IsCritical() && ctx.Project.NotificationSettings.Any(n => n.Value.ReportCriticalEvents))
            return true;

        return false;
    }
}
