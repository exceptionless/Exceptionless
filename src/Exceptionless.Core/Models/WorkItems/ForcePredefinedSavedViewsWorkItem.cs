using Foundatio.Queues;

namespace Exceptionless.Core.Models.WorkItems;

public record ForcePredefinedSavedViewsWorkItem : IHaveUniqueIdentifier
{
    public required string UserId { get; init; }

    public string UniqueIdentifier => nameof(ForcePredefinedSavedViewsWorkItem);
}
