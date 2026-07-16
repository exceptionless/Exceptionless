using Exceptionless.Core.Messaging.Models;

namespace Exceptionless.Web.Api.Messages;

public record GetAboutInfo;
public record GetQueueStats;
public record PostReleaseNotification(string Message, bool Critical);
public record GetSystemNotification;
public record PostSystemNotification(string Message, SystemNotificationLevel Level = SystemNotificationLevel.Info, SystemNotificationTarget Target = SystemNotificationTarget.Both, bool Publish = true);
public record RemoveSystemNotification(bool Publish = true);
public record ValidateSearchQuery(string Query);

public record SetSystemNotificationRequest
{
    public string? Message { get; set; }
    public SystemNotificationLevel Level { get; set; } = SystemNotificationLevel.Info;
    public SystemNotificationTarget Target { get; set; } = SystemNotificationTarget.Both;
}
