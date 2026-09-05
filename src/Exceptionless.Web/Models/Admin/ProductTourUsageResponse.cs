using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Models.Admin;

public sealed record ProductTourUsageResponse(
    DateTime? UtcStart,
    DateTime UtcEnd,
    IReadOnlyCollection<ProductTourSummary> Tours)
{
    public bool CollectionAvailable { get; init; }
}

public sealed record ProductTourSummary(
    string Name,
    int Version,
    ProductTourKind Kind,
    long Shown,
    long Started,
    long Completed,
    long Dismissed,
    DateTime? LastRunUtc,
    IReadOnlyCollection<ProductTourStartSource> StartSources,
    IReadOnlyCollection<ProductTourActivity> Activity);

public sealed record ProductTourStartSource(ProductTourLaunchSource Source, long Count);

public sealed record ProductTourActivity(DateTime DateUtc, long Shown, long Started, long Completed, long Dismissed);
