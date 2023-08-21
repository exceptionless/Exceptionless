namespace Exceptionless.Core.Queues.Models;

public record MailMessage
{
    public required string To { get; set; }
    public string? From { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
}
