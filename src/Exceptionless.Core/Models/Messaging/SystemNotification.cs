using System.Text.Json.Serialization;

namespace Exceptionless.Core.Messaging.Models;

public record SystemNotification
{
    public required DateTime Date { get; set; }
    public string? Message { get; set; }
    public SystemNotificationLevel Level { get; set; } = SystemNotificationLevel.Info;
    public SystemNotificationTarget Target { get; set; } = SystemNotificationTarget.Both;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SystemNotificationLevel
{
    Info,
    Warning,
    Error
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SystemNotificationTarget
{
    Both,
    Legacy,
    Modern
}
