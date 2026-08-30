using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Repositories;

public sealed record ProductTourUsageResult(
    IReadOnlyCollection<ProductTourUsageBucket> Buckets);

public sealed record ProductTourUsageBucket(ProductTourUsageSource Source, long Count, DateTime? LastUtc);

public sealed record ProductTourUsageSource(
    string Raw,
    ProductTourTelemetryEvent Event,
    string TourName,
    int Version,
    ProductTourLaunchSource LaunchSource);
