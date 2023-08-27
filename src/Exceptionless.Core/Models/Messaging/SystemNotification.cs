namespace Exceptionless.Core.Messaging.Models;

public record SystemNotification
{
    public required DateTime Date { get; set; }
    public string? Message { get; set; }
}
