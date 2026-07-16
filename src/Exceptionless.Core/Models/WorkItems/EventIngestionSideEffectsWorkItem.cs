using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public sealed record EventIngestionSideEffectsWorkItem : IHaveUniqueIdentifier
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public required string BatchId { get; init; }
    public required string[] EventIds { get; init; }
    public string UniqueIdentifier => String.Concat("event-ingestion-v3:", ProjectId, ":", BatchId);
}
