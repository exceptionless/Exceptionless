using Exceptionless.Core.Models;

namespace Exceptionless.Core.Models.Ingestion;

public sealed record StackRoute(
    string StackId,
    StackStatus Status,
    long Version,
    string? FixedInVersion = null,
    DateTime? DateFixed = null,
    bool OccurrencesAreCritical = false,
    string? RegressionEventId = null,
    string? IngestionFirstEventId = null)
{
    public bool IsDiscarded => Status is StackStatus.Discarded;
}

public sealed record StackRouteCacheEntry(
    bool Exists,
    long Version,
    string? StackId = null,
    StackStatus Status = StackStatus.Open,
    string? FixedInVersion = null,
    DateTime? DateFixed = null,
    bool OccurrencesAreCritical = false,
    string? RegressionEventId = null,
    string? IngestionFirstEventId = null)
{
    public StackRoute? ToRoute() => Exists && StackId is not null
        ? new StackRoute(StackId, Status, Version, FixedInVersion, DateFixed, OccurrencesAreCritical, RegressionEventId, IngestionFirstEventId)
        : null;

    public static StackRouteCacheEntry FromRoute(StackRoute route) => new(
        true,
        route.Version,
        route.StackId,
        route.Status,
        route.FixedInVersion,
        route.DateFixed,
        route.OccurrencesAreCritical,
        route.RegressionEventId,
        route.IngestionFirstEventId);
}
