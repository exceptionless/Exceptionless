namespace Exceptionless.Core.Queues.Models;

public record SummaryNotification
{
    public required string Id { get; set; }
    public required DateTime UtcStartTime { get; set; }
    public required DateTime UtcEndTime { get; set; }
}
