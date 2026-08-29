using Exceptionless.Core.Models.Data;

namespace Exceptionless.Web.Models.Admin;

public sealed record ProductTourUsageResponse(
    DateTime? Month,
    IReadOnlyCollection<ProductTourSummary> Tours,
    IReadOnlyCollection<ProductTourEvent> RecentEvents);

public sealed record ProductTourSummary(
    string Name,
    long Shown,
    long Started,
    long ManualStarted,
    long Completed,
    long Dismissed,
    DateTime? LastRunUtc,
    decimal? StartedRate,
    decimal? ManualStartedRate,
    decimal? CompletionRate,
    decimal? DismissalRate);

public sealed record ProductTourEvent(
    DateTime DateUtc,
    ProductTourTelemetryEvent Event,
    ProductTourLaunchSource LaunchSource,
    string TourName,
    string? UserIdentity,
    string? UserName,
    int Version,
    long Count);
