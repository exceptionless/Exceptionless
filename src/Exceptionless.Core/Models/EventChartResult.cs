namespace Exceptionless.Core.Models;

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
