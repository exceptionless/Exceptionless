namespace Exceptionless.Web.Api.Messages;

public record GetAboutInfo;
public record GetQueueStats;
public record PostReleaseNotification(string Message, bool Critical);
public record GetSystemNotification;
public record PostSystemNotification(string Message, bool Publish = true);
public record RemoveSystemNotification;
public record ValidateSearchQuery(string Query);
