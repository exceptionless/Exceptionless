using System.ComponentModel.DataAnnotations;
using Exceptionless.Core.Models;

namespace Exceptionless.Web.Models;

public sealed record EventChartRequest
{
    [Required]
    public EventChart Chart { get; set; } = null!;
    [MaxLength(2000)]
    public string? Filter { get; set; }
    [MaxLength(100)]
    public string? Time { get; set; }
    [MaxLength(20)]
    public string? Offset { get; set; }
}

public sealed record EventChartResult
{
    public List<EventChartSeries> Series { get; set; } = [];
    public long Total { get; set; }
    public bool Truncated { get; set; }
    public long? IntervalMilliseconds { get; set; }
}

public sealed record EventChartSeries(string Name, List<EventChartPoint> Points);
public sealed record EventChartPoint(DateTime Date, double? Value, long Count, string? EventId = null);
public sealed record EventMeasurementDescriptor(string Name, string Unit);
public sealed record EventMeasurementCatalog(List<EventMeasurementDescriptor> Measurements, bool Truncated);
