namespace Exceptionless.Core.Messaging.Models;

public record ReleaseNotification
{
    public required bool Critical { get; set; }
    public required DateTime Date { get; set; }
    public string? Message { get; set; }
}
