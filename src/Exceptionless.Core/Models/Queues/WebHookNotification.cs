using Foundatio.Queues;

namespace Exceptionless.Core.Queues.Models;

public record WebHookNotification : IHaveUniqueIdentifier
{
    public required string OrganizationId { get; set; }
    public required string ProjectId { get; set; }
    public string? WebHookId { get; set; }
    public required WebHookType Type { get; set; } = WebHookType.General;
    public required string Url { get; set; }
    public required object? Data { get; set; }
    public string DeduplicationId { get; set; } = Guid.NewGuid().ToString("N");
    public string UniqueIdentifier => DeduplicationId;
}

public enum WebHookType
{
    General = 0,
    Slack = 1
}
