using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record ForcePredefinedSavedViewsWorkItem : IHaveUniqueIdentifier
{
    public required string UserId { get; init; }

    public Guid RunId { get; init; } = Guid.NewGuid();

    public string UniqueIdentifier => $"{nameof(ForcePredefinedSavedViewsWorkItem)}:{RunId:N}";
}
