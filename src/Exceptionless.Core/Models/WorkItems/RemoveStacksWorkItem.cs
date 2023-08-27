namespace Exceptionless.Core.Models.WorkItems;

public record RemoveStacksWorkItem
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
}
