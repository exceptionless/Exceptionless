using Exceptionless.Core;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Services;
using Exceptionless.Web.Api.Messages;
using Foundatio.Queues;

namespace Exceptionless.Web.Api.Handlers;

public class StatusHandler(
    NotificationService notificationService,
    IQueue<EventPost> eventQueue,
    IQueue<MailMessage> mailQueue,
    IQueue<EventNotification> notificationQueue,
    IQueue<WebHookNotification> webHooksQueue,
    IQueue<EventUserDescription> userDescriptionQueue,
    AppOptions appOptions)
{
    public object Handle(GetAboutInfo message)
    {
        return new
        {
            appOptions.InformationalVersion,
            AppMode = appOptions.AppMode.ToString(),
            Environment.MachineName
        };
    }

    public async Task<object> Handle(GetQueueStats message)
    {
        var eventQueueStats = await eventQueue.GetQueueStatsAsync();
        var mailQueueStats = await mailQueue.GetQueueStatsAsync();
        var userDescriptionQueueStats = await userDescriptionQueue.GetQueueStatsAsync();
        var notificationQueueStats = await notificationQueue.GetQueueStatsAsync();
        var webHooksQueueStats = await webHooksQueue.GetQueueStatsAsync();

        return new
        {
            EventPosts = new
            {
                Active = eventQueueStats.Enqueued,
                eventQueueStats.Deadletter,
                eventQueueStats.Working
            },
            MailMessages = new
            {
                Active = mailQueueStats.Enqueued,
                mailQueueStats.Deadletter,
                mailQueueStats.Working
            },
            UserDescriptions = new
            {
                Active = userDescriptionQueueStats.Enqueued,
                userDescriptionQueueStats.Deadletter,
                userDescriptionQueueStats.Working
            },
            Notifications = new
            {
                Active = notificationQueueStats.Enqueued,
                notificationQueueStats.Deadletter,
                notificationQueueStats.Working
            },
            WebHooks = new
            {
                Active = webHooksQueueStats.Enqueued,
                webHooksQueueStats.Deadletter,
                webHooksQueueStats.Working
            }
        };
    }

    public Task<ReleaseNotification> Handle(PostReleaseNotification message)
    {
        return notificationService.SendReleaseNotificationAsync(message.Message, message.Critical);
    }

    public async Task<SystemNotification> Handle(GetSystemNotification message)
    {
        return await notificationService.GetSystemNotificationAsync() ?? new SystemNotification { Date = DateTime.MinValue };
    }

    public async Task<SystemNotification> Handle(PostSystemNotification message)
    {
        if (String.IsNullOrWhiteSpace(message.Message))
            return new SystemNotification { Date = DateTime.MinValue };

        return await notificationService.SetSystemNotificationAsync(message.Message, message.Publish);
    }

    public Task Handle(RemoveSystemNotification message)
    {
        return notificationService.ClearSystemNotificationAsync(message.Publish);
    }
}
