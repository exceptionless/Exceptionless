namespace Exceptionless.Core.Messaging.Models;

public record NotificationSettingsResponse
{
    public string? ConfiguredSystemNotificationMessage { get; init; }
    public SystemNotification? SystemNotification { get; init; }
}
