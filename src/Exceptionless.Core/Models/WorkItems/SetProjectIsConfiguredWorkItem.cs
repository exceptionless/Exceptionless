namespace Exceptionless.Core.Models.WorkItems;

public record SetProjectIsConfiguredWorkItem
{
    public required string ProjectId { get; init; }
    public bool IsConfigured { get; init; }
}
