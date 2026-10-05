using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Repositories;
using Foundatio.Repositories.Extensions;
using Foundatio.Repositories.Models;

namespace Exceptionless.Core.Services;

/// <summary>Shared, bounded telemetry queries for the API and MCP. Callers authorize and validate inputs.</summary>
public sealed class EventTelemetryService(IEventRepository eventRepository)
{
    public async Task<EventChartResult> GetChartAsync(AppFilter systemFilter, EventChart chart, string? filter, DateTime start, DateTime end)
    {
        RepositoryQueryDescriptor<PersistentEvent> query = q => q
            .SortExpression("-date")
            .AppFilter(systemFilter)
            .FilterExpression(filter)
            .EnforceEventStackFilter()
            .DateRange(start, end, "date")
            .Index(start, end)
            .EventChart(chart, start, end);

        if (chart.Mode == "events")
        {
            var events = await eventRepository.FindAsync(query, o => o.PageLimit(EventChartQueryExtensions.MaxPoints));
            var groups = events.Documents.GroupBy(ev => GetSeriesName(ev, chart.GroupBy)).ToList();
            return new EventChartResult
            {
                Total = events.Total,
                Truncated = events.HasMore || events.Total > events.Documents.Count || groups.Count > EventChartQueryExtensions.MaxSeries,
                Series = groups.Take(EventChartQueryExtensions.MaxSeries).Select(group => new EventChartSeries(group.Key,
                    group.OrderBy(ev => ev.Date).Select(ev => new EventChartPoint(ev.Date.UtcDateTime,
                        ev.Measurements?.FirstOrDefault(m => m.Name == chart.Measurement && m.Unit == chart.Unit)?.Value, 1, ev.Id)).ToList())).ToList()
            };
        }

        var result = await eventRepository.CountAsync(query);
        var response = new EventChartResult
        {
            Total = result.Total,
            IntervalMilliseconds = EventChartQueryExtensions.GetInterval(start, end)
        };
        if (chart.GroupBy is null)
            response.Series.Add(ReadSeries("All events", result.Aggregations, chart));
        else
        {
            var buckets = result.Aggregations.Terms<string>("series")?.Buckets ?? [];
            response.Truncated = buckets.Sum(b => b.Total ?? 0) < result.Total;
            foreach (var bucket in buckets)
                response.Series.Add(ReadSeries(bucket.Key, bucket.Aggregations, chart));
        }
        return response;
    }

    public async Task<EventMeasurementCatalog> GetMeasurementsAsync(AppFilter systemFilter, string? filter, DateTime start, DateTime end)
    {
        var result = await eventRepository.CountAsync(q => q.AppFilter(systemFilter).FilterExpression(filter).EnforceEventStackFilter()
            .DateRange(start, end, "date").Index(start, end).MeasurementCatalog());
        var measurements = GetBucket(result.Aggregations, "measurements");
        var names = measurements?.Aggregations.Terms<string>("names")?.Buckets ?? [];
        var values = new List<EventMeasurementDescriptor>();
        bool truncated = names.Sum(n => n.Total ?? 0) < (measurements?.Total ?? 0);
        foreach (var name in names)
        {
            var units = name.Aggregations.Terms<string>("units")?.Buckets ?? [];
            truncated |= units.Sum(u => u.Total ?? 0) < (name.Total ?? 0);
            values.AddRange(units.Select(u => new EventMeasurementDescriptor(name.Key, u.Key)));
        }
        return new EventMeasurementCatalog(values.OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.Unit, StringComparer.Ordinal).ToList(), truncated);
    }

    private static string GetSeriesName(PersistentEvent ev, string? groupBy) => groupBy switch
    {
        null => "All events",
        "source" => ev.Source ?? "(missing)",
        "stack" => ev.StackId,
        "outcome" => ev.Outcome ?? "(missing)",
        "result" => ev.Result ?? "(missing)",
        _ => ev.Labels?.GetValueOrDefault(groupBy["labels.".Length..]) ?? "(missing)"
    };

    private static SingleBucketAggregate? GetBucket(IReadOnlyDictionary<string, IAggregate> aggregations, string name)
        => aggregations.GetValueOrDefault(name) as SingleBucketAggregate;

    private static EventChartSeries ReadSeries(string name, IReadOnlyDictionary<string, IAggregate> aggregations, EventChart chart)
    {
        var points = new List<EventChartPoint>();
        foreach (var bucket in aggregations.DateHistogram("dates")?.Buckets ?? [])
        {
            var selected = GetBucket(bucket.Aggregations, "measurements") is { } nested ? GetBucket(nested.Aggregations, "selected") : null;
            double? value = chart.Measurement is null ? bucket.Total ?? 0 : null;
            if (selected is { Total: > 0 } && selected.Aggregations.TryGetValue("value", out var metric))
                value = metric switch
                {
                    ValueAggregate numeric => numeric.Value,
                    PercentilesAggregate percentiles => percentiles.Items.FirstOrDefault()?.Value,
                    _ => null
                };
            points.Add(new EventChartPoint(bucket.Date, value, bucket.Total ?? 0));
        }
        return new EventChartSeries(name, points);
    }
}
