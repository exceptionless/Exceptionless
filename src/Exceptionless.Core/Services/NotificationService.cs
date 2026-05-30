using Exceptionless.Core.Messaging.Models;
using Foundatio.Caching;
using Foundatio.Messaging;

namespace Exceptionless.Core.Services;

public class NotificationService(ICacheClient cacheClient, IMessagePublisher messagePublisher, TimeProvider timeProvider)
{
    private const string SystemNotificationCacheKey = "system-notification";

    public async Task<SystemNotification?> GetSystemNotificationAsync()
    {
        var result = await cacheClient.GetAsync<SystemNotification>(SystemNotificationCacheKey);
        return result.HasValue ? result.Value : null;
    }

    public async Task<SystemNotification> SetSystemNotificationAsync(string message, bool publish = true)
    {
        var notification = new SystemNotification { Date = timeProvider.GetUtcNow().UtcDateTime, Message = message };
        await cacheClient.SetAsync(SystemNotificationCacheKey, notification);
        if (publish)
            await messagePublisher.PublishAsync(notification);
        return notification;
    }

    public async Task ClearSystemNotificationAsync(bool publish = true)
    {
        await cacheClient.RemoveAsync(SystemNotificationCacheKey);
        if (publish)
            await messagePublisher.PublishAsync(new SystemNotification { Date = timeProvider.GetUtcNow().UtcDateTime });
    }

    public async Task<ReleaseNotification> SendReleaseNotificationAsync(string? message, bool critical, bool publish = true)
    {
        var notification = new ReleaseNotification { Critical = critical, Date = timeProvider.GetUtcNow().UtcDateTime, Message = message };
        if (publish)
            await messagePublisher.PublishAsync(notification);
        return notification;
    }
}
