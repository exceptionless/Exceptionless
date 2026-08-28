namespace Exceptionless.Web.Models.Admin;

public sealed record AdminProductTourUsageResponse(
    DateTime Month,
    IReadOnlyCollection<AdminProductTourSummary> Tours,
    IReadOnlyCollection<AdminProductTourActivity> RecentActivity);

public sealed record AdminProductTourSummary(
    string Name,
    long Shown,
    long Started,
    long Completed,
    long Dismissed,
    long UniqueUsers,
    DateTime? LastRunUtc,
    decimal? CompletionRate,
    decimal? DismissalRate);

public sealed record AdminProductTourActivity(
    DateTime DateUtc,
    string Event,
    string LaunchSource,
    string TourName,
    string? UserIdentity,
    string? UserName,
    int Version,
    long Count);
