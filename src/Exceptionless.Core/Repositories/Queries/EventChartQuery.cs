using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Exceptionless.Core.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Options;

namespace Exceptionless.Core.Repositories.Queries;

public static class EventChartQueryExtensions
{
    internal const string ChartKey = "@EventChart";
    internal const string CatalogKey = "@EventMeasurementCatalog";
    public const int MaxSeries = 20;
    public const int MaxPoints = 1000;

    public static T EventChart<T>(this T query, EventChart chart, DateTime start, DateTime end) where T : IRepositoryQuery
        => query.BuildOption(ChartKey, new ChartQuery(chart, GetInterval(start, end)));

    public static T MeasurementCatalog<T>(this T query) where T : IRepositoryQuery => query.BuildOption(CatalogKey, true);

    // UTC fixed intervals keep a strict upper bound on buckets, even for very long retention periods.
    public static long GetInterval(DateTime start, DateTime end) => Math.Max(1000, (long)Math.Ceiling((end - start).TotalMilliseconds / 200 / 1000) * 1000);

    internal sealed record ChartQuery(EventChart Chart, long IntervalMilliseconds);
}

public sealed class EventChartQueryBuilder : IElasticQueryBuilder
{
    public Task BuildAsync<T>(QueryBuilderContext<T> ctx) where T : class, new()
    {
        if (ctx.Source.SafeGetOption<bool>(EventChartQueryExtensions.CatalogKey))
        {
            ctx.Search.Aggregations(new Dictionary<string, Aggregation>
            {
                ["measurements"] = new()
                {
                    Nested = new NestedAggregation { Path = "measurements" },
                    Aggregations = new Dictionary<string, Aggregation>
                    {
                        ["names"] = new()
                        {
                            Terms = new TermsAggregation { Field = "measurements.name", Size = 100 },
                            Aggregations = new Dictionary<string, Aggregation>
                            {
                                ["units"] = new() { Terms = new TermsAggregation { Field = "measurements.unit", Size = 20 } }
                            }
                        }
                    }
                }
            });
        }

        var query = ctx.Source.SafeGetOption<EventChartQueryExtensions.ChartQuery?>(EventChartQueryExtensions.ChartKey);
        if (query is null)
            return Task.CompletedTask;

        var chart = query.Chart;
        Query? measurementFilter = null;
        if (chart.Measurement is not null)
        {
            measurementFilter = new TermQuery { Field = "measurements.name", Value = chart.Measurement };
            measurementFilter &= new TermQuery { Field = "measurements.unit", Value = chart.Unit! };
            ctx.Filter &= new NestedQuery { Path = "measurements", Query = measurementFilter, IgnoreUnmapped = true };
        }

        if (chart.Mode == "events")
            return Task.CompletedTask;

        var date = new Aggregation
        {
            DateHistogram = new DateHistogramAggregation
            {
                Field = "date",
                FixedInterval = $"{query.IntervalMilliseconds}ms",
                MinDocCount = 0
            }
        };

        if (measurementFilter is not null)
        {
            var metric = chart.Aggregation switch
            {
                "min" => new Aggregation { Min = new MinAggregation { Field = "measurements.value" } },
                "max" => new Aggregation { Max = new MaxAggregation { Field = "measurements.value" } },
                "sum" => new Aggregation { Sum = new SumAggregation { Field = "measurements.value" } },
                "count" => new Aggregation { ValueCount = new ValueCountAggregation { Field = "measurements.value" } },
                "p50" or "p95" or "p99" => new Aggregation { Percentiles = new PercentilesAggregation { Field = "measurements.value", Percents = [Double.Parse(chart.Aggregation[1..], System.Globalization.CultureInfo.InvariantCulture)] } },
                _ => new Aggregation { Avg = new AverageAggregation { Field = "measurements.value" } }
            };
            date.Aggregations = new Dictionary<string, Aggregation>
            {
                ["measurements"] = new()
                {
                    Nested = new NestedAggregation { Path = "measurements" },
                    Aggregations = new Dictionary<string, Aggregation>
                    {
                        ["selected"] = new()
                        {
                            Filter = measurementFilter,
                            Aggregations = new Dictionary<string, Aggregation> { ["value"] = metric }
                        }
                    }
                }
            };
        }

        var aggregations = new Dictionary<string, Aggregation> { ["dates"] = date };
        if (chart.GroupBy is not null)
        {
            string field = chart.GroupBy switch { "source" => "source.keyword", "stack" => "stack_id", _ => chart.GroupBy };
            aggregations = new Dictionary<string, Aggregation>
            {
                ["series"] = new() { Terms = new TermsAggregation { Field = field, Size = EventChartQueryExtensions.MaxSeries }, Aggregations = aggregations }
            };
        }
        ctx.Search.Aggregations(aggregations);
        return Task.CompletedTask;
    }
}
