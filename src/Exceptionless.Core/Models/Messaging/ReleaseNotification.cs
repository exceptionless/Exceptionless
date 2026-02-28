namespace Exceptionless.Core.Messaging.Models;

public record ReleaseNotification
{
    public bool Critical { get; set; }
    public DateTime Date { get; set; }
    public string? Message { get; set; }
}
