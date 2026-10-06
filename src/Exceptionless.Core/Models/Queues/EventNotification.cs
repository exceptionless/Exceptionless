namespace Exceptionless.Core.Queues.Models;

public record EventNotification
{
    public required string EventId { get; set; }
    public required bool IsNew { get; set; }
    public required bool IsRegression { get; set; }
    public required int TotalOccurrences { get; set; }
}
