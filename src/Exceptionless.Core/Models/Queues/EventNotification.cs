using Exceptionless.Core.Queues;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Queues.Models;

public record EventNotification : IHaveDurableUniqueIdentifier
{
    public required string EventId { get; set; }
    public required bool IsNew { get; set; }
    public required bool IsRegression { get; set; }
    public required int TotalOccurrences { get; set; }
    public string DeduplicationId { get; set; } = Guid.NewGuid().ToString("N");
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UseDurableDeduplication { get; set; }
    public string UniqueIdentifier => DeduplicationId;
}
