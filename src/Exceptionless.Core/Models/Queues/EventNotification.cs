using Foundatio.Queues;

namespace Exceptionless.Core.Queues.Models;

public record EventNotification : IHaveUniqueIdentifier
{
    public required string EventId { get; set; }
    public required bool IsNew { get; set; }
    public required bool IsRegression { get; set; }
    public required int TotalOccurrences { get; set; }
    public string DeduplicationId { get; set; } = Guid.NewGuid().ToString("N");
    public string UniqueIdentifier => DeduplicationId;
}
