namespace Exceptionless.Core.Queues.Models;

public record WebHookNotification
{
    public required string OrganizationId { get; set; }
    public required string ProjectId { get; set; }
    public string? WebHookId { get; set; }
    public required WebHookType Type { get; set; } = WebHookType.General;
    public required string Url { get; set; }
    public required object? Data { get; set; }
}

public enum WebHookType
{
    General = 0,
    Slack = 1
}
