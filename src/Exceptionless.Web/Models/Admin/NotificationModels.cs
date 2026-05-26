using Exceptionless.Core.Messaging.Models;

namespace Exceptionless.Web.Models.Admin;

public record NotificationSettingsResponse
{
    public string? ConfiguredSystemNotificationMessage { get; init; }
    public SystemNotification? SystemNotification { get; init; }
}

public record SetSystemNotificationRequest
{
    public required string Message { get; init; }
    public bool Publish { get; init; } = true;
}

public record SendReleaseNotificationRequest
{
    public string? Message { get; init; }
    public bool Critical { get; init; }
}

public record ForceRefreshRequest
{
    public string? Message { get; init; }
}
