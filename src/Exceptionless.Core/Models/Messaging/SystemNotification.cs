namespace Exceptionless.Core.Messaging.Models;

public record SystemNotification
{
    public required DateTime Date { get; set; }
    public required string Message { get; set; }
}
