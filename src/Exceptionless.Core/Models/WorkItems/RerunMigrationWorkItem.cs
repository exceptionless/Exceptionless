using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record RerunMigrationWorkItem : IHaveUniqueIdentifier
{
    public required string OperationId { get; init; }

    public string UniqueIdentifier => $"{nameof(RerunMigrationWorkItem)}:{OperationId}";
}
