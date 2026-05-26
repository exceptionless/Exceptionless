using Exceptionless.Core;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Web.Api.Messages;
using Foundatio.Caching;
using Foundatio.Messaging;
using Foundatio.Queues;

namespace Exceptionless.Web.Api.Handlers;

public class StatusHandler(
    ICacheClient cacheClient,
    IMessagePublisher messagePublisher,
    IQueue<EventPost> eventQueue,
    IQueue<MailMessage> mailQueue,
    IQueue<EventNotification> notificationQueue,
    IQueue<WebHookNotification> webHooksQueue,
    IQueue<EventUserDescription> userDescriptionQueue,
    AppOptions appOptions,
    TimeProvider timeProvider)
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

    public async Task<ReleaseNotification> Handle(PostReleaseNotification message)
    {
        var notification = new ReleaseNotification
        {
            Critical = message.Critical,
            Date = timeProvider.GetUtcNow().UtcDateTime,
            Message = message.Message
        };
        await messagePublisher.PublishAsync(notification);
        return notification;
    }

    public Task<CacheValue<SystemNotification>> Handle(GetSystemNotification message)
    {
        return cacheClient.GetAsync<SystemNotification>("system-notification");
    }

    public async Task<SystemNotification> Handle(PostSystemNotification message)
    {
        if (String.IsNullOrWhiteSpace(message.Message))
            return new SystemNotification { Date = DateTime.MinValue };

        var notification = new SystemNotification
        {
            Date = timeProvider.GetUtcNow().UtcDateTime,
            Message = message.Message
        };
        await cacheClient.SetAsync("system-notification", notification);
        await messagePublisher.PublishAsync(notification);
        return notification;
    }

    public async Task Handle(RemoveSystemNotification message)
    {
        await cacheClient.RemoveAsync("system-notification");
        await messagePublisher.PublishAsync(new SystemNotification { Date = timeProvider.GetUtcNow().UtcDateTime });
    }
}
