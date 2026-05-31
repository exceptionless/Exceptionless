namespace Exceptionless.Core.Messaging.Models;

public record SystemNotification
{
    public required DateTime Date { get; set; }
    public string? Message { get; set; }
    public SystemNotificationLevel Level { get; set; } = SystemNotificationLevel.Info;
}

public enum SystemNotificationLevel
{
    Info,
    Warning,
    Error
}
