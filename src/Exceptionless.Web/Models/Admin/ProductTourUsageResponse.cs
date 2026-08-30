using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Models.Admin;

public sealed record ProductTourUsageResponse(
    DateTime? UtcStart,
    DateTime UtcEnd,
    IReadOnlyCollection<ProductTourSummary> Tours);

public sealed record ProductTourSummary(
    string Name,
    int Version,
    ProductTourKind Kind,
    long Shown,
    long Started,
    long Completed,
    long Dismissed,
    DateTime? LastRunUtc,
    IReadOnlyCollection<ProductTourStartSource> StartSources);

public sealed record ProductTourStartSource(ProductTourLaunchSource Source, long Count);
