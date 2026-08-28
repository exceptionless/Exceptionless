using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Repositories;

public sealed record ProductTourUsageResult(
    IReadOnlyCollection<ProductTourUsageTour> Tours,
    IReadOnlyCollection<ProductTourUsageEvent> RecentEvents);

public sealed record ProductTourUsageTour(string Name, long UniqueUsers, IReadOnlyCollection<ProductTourUsageBucket> Buckets);

public sealed record ProductTourUsageBucket(ProductTourUsageSource Source, long Count, DateTime? LastUtc);

public sealed record ProductTourUsageEvent(PersistentEvent Event, ProductTourUsageSource Source);

public sealed record ProductTourUsageSource(
    string Raw,
    ProductTourTelemetryEvent Event,
    string TourName,
    int Version,
    ProductTourLaunchSource LaunchSource);
