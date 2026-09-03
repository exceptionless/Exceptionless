using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Web.Models.Admin;

public sealed record ProductTourUsageResponse(
    DateTime? UtcStart,
    DateTime UtcEnd,
    IReadOnlyCollection<ProductTourSummary> Tours,
    ProductTourUsageInterval Interval)
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
    IReadOnlyCollection<ProductTourActivity> Activity)
{
    public IReadOnlyCollection<ProductTourStepActivity> Steps { get; init; } = [];
}

public sealed record ProductTourStepActivity(string Step, long Reached, long Dismissed);

public sealed record ProductTourStartSource(ProductTourLaunchSource Source, long Count);

public sealed record ProductTourActivity(DateTime DateUtc, long Shown, long Started, long Completed, long Dismissed);
