namespace Exceptionless.Core.Models.WorkItems;

public record RemoveBotEventsWorkItem
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public required string ClientIpAddress { get; init; }
    public required DateTime UtcStartDate { get; init; }
    public required DateTime UtcEndDate { get; init; }
}
