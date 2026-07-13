using Exceptionless.Core.Queues;
using System.Text.Json.Serialization;

namespace Exceptionless.Core.Queues.Models;

public record WebHookNotification : IHaveDurableUniqueIdentifier
{
    public required string OrganizationId { get; set; }
    public required string ProjectId { get; set; }
    public string? WebHookId { get; set; }
    public required WebHookType Type { get; set; } = WebHookType.General;
    public required string Url { get; set; }
    public required object? Data { get; set; }
    public string DeduplicationId { get; set; } = Guid.NewGuid().ToString("N");
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UseDurableDeduplication { get; set; }
    public string UniqueIdentifier => DeduplicationId;
}

public enum WebHookType
{
    General = 0,
    Slack = 1
}
