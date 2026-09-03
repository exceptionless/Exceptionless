using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Exceptionless.Core.Models.Data;

namespace Exceptionless.Core.Repositories;

public sealed record ProductTourUsageResult(
    IReadOnlyCollection<ProductTourUsageBucket> Buckets,
    ProductTourUsageInterval Interval);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProductTourUsageInterval
{
    [JsonStringEnumMemberName("day")]
    [EnumMember(Value = "day")]
    Day,
    [JsonStringEnumMemberName("month")]
    [EnumMember(Value = "month")]
    Month
}

public sealed record ProductTourUsageBucket(ProductTourUsageSource Source, long Count, DateTime? LastUtc, IReadOnlyCollection<ProductTourUsagePeriod> Activity)
{
    public IReadOnlyCollection<ProductTourStepCount> Steps { get; init; } = [];
}

public sealed record ProductTourStepCount(string Step, long Count);

public sealed record ProductTourUsagePeriod(DateTime DateUtc, long Count);

public sealed record ProductTourUsageSource(
    string Raw,
    ProductTourTelemetryEvent Event,
    string TourName,
    int Version,
    ProductTourLaunchSource LaunchSource);
