namespace Exceptionless.Core.Models;

public record MailMessageData
{
    public required string Subject { get; set; }
    public required Dictionary<string, object?> Data { get; set; }
}
