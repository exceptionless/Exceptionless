using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Lock;
using Foundatio.Messaging;

namespace Exceptionless.Core.Services;

public class NotificationService(ICacheClient cacheClient, IMessagePublisher messagePublisher, TimeProvider timeProvider, CacheLockProvider lockProvider)
{
    private const string SystemNotificationCacheKey = "system-notification";
    private static readonly TimeSpan OrganizationNotificationLockTimeout = TimeSpan.FromMinutes(90);

    public async Task<SystemNotification?> GetSystemNotificationAsync()
    {
        var result = await cacheClient.GetAsync<SystemNotification>(SystemNotificationCacheKey);
        return result.HasValue ? result.Value : null;
    }

    public async Task<SystemNotification> SetSystemNotificationAsync(string message, SystemNotificationLevel level = SystemNotificationLevel.Info, SystemNotificationTarget target = SystemNotificationTarget.Both, bool publish = true)
    {
        var notification = new SystemNotification { Date = timeProvider.GetUtcNow().UtcDateTime, Message = message, Level = level, Target = target };
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

    public async Task<bool> IsOrganizationNotificationSentAsync(string organizationId, bool isOverMonthlyLimit)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        var sent = await cacheClient.GetAsync<bool>(GetOrganizationNotificationSentKey(organizationId, isOverMonthlyLimit));
        return sent.HasValue && sent.Value;
    }

    public Task SetOrganizationNotificationSentAsync(string organizationId, bool isOverMonthlyLimit)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return cacheClient.SetAsync(GetOrganizationNotificationSentKey(organizationId, isOverMonthlyLimit), true, GetOrganizationNotificationSentExpiresAtUtc());
    }

    public Task RemoveOrganizationNotificationSentAsync(string organizationId, bool isOverMonthlyLimit)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return cacheClient.RemoveAsync(GetOrganizationNotificationSentKey(organizationId, isOverMonthlyLimit));
    }

    public Task<ILock?> TryAcquireOrganizationNotificationLockAsync(string organizationId, bool isOverMonthlyLimit)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return lockProvider.TryAcquireAsync(GetOrganizationNotificationLockKey(organizationId, isOverMonthlyLimit), OrganizationNotificationLockTimeout, TimeSpan.Zero);
    }

    public Task<bool> IsOrganizationNotificationLockedAsync(string organizationId, bool isOverMonthlyLimit)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);

        return lockProvider.IsLockedAsync(GetOrganizationNotificationLockKey(organizationId, isOverMonthlyLimit));
    }

    private static string GetOrganizationNotificationLockKey(string organizationId, bool isOverMonthlyLimit)
    {
        return $"{OrganizationNotificationWorkItem.GetNotificationKey(organizationId, isOverMonthlyLimit)}-lock";
    }

    private static string GetOrganizationNotificationSentKey(string organizationId, bool isOverMonthlyLimit)
    {
        return $"{OrganizationNotificationWorkItem.GetNotificationKey(organizationId, isOverMonthlyLimit)}-sent";
    }

    private DateTime GetOrganizationNotificationSentExpiresAtUtc()
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAtUtc = utcNow.StartOfMonth().AddMonths(1);

        // Foundatio treats absolute cache expirations less than 5ms in the future as already expired.
        // Keep the marker observable for sends that complete in the final milliseconds of the UTC month.
        if (expiresAtUtc - utcNow < CacheClientExtensions.MinimumExpiration)
            return utcNow.Add(CacheClientExtensions.MinimumExpiration);

        return expiresAtUtc;
    }
}
