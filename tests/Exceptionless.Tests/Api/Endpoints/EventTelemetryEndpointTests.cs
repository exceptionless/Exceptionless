using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class EventTelemetryEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : IntegrationTestsBase(output, factory)
{
    protected override async Task ResetDataAsync()
    {
        // Keep observations in one daily partition when tests run near UTC midnight.
        TimeProvider.SetUtcNow(TimeProvider.GetUtcNow().UtcDateTime.Date.AddHours(12));
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact]
    public async Task Chart_MultipleMeasurementsAndUnits_IsolatesObservationsAndPreservesZero()
    {
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Source("import");
            d.Event().TestProject().Source("import");
            d.Event().TestProject().Source("import");
            d.Event().FreeProject().Source("import");
        });
        var observations = events.Where(e => e.OrganizationId == SampleDataService.TEST_ORG_ID).ToList();
        observations[0].Measurements = [new() { Name = "duration", Unit = "ms", Value = 0 }, new() { Name = "allocated", Unit = "By", Value = 999 }];
        observations[1].Measurements = [new() { Name = "duration", Unit = "ms", Value = 20 }, new() { Name = "allocated", Unit = "By", Value = 999 }];
        observations[2].Measurements = [new() { Name = "duration", Unit = "s", Value = 900 }];
        observations[0].Outcome = Event.KnownOutcomes.Success;
        observations[0].Result = "completed";
        observations[1].Outcome = Event.KnownOutcomes.Failure;
        observations[1].Result = "timed_out";
        foreach (var ev in events)
        {
            ev.Labels = new() { ["version"] = "4.90", ["size"] = "00123" };
            ev.Date = TimeProvider.GetUtcNow().AddMinutes(-1);
        }
        events.Single(e => e.OrganizationId != SampleDataService.TEST_ORG_ID).Measurements = [new() { Name = "duration", Unit = "ms", Value = 9999 }];
        await GetService<IEventRepository>().SaveAsync(events, o => o.ImmediateConsistency());
        var request = new EventChartRequest { Chart = new() { Measurement = "duration", Unit = "ms", GroupBy = "labels.version" }, Filter = "labels.size:00123" };

        var result = await ChartAsync(request);

        Assert.Equal(2, result.Total);
        Assert.False(result.Truncated);
        var series = Assert.Single(result.Series);
        Assert.Equal("4.90", series.Name);
        Assert.Equal(10, Assert.Single(series.Points).Value);

        request.Filter = "measurement.duration@ms:>0";
        result = await ChartAsync(request);
        Assert.Equal(1, result.Total);
        Assert.Equal(20, Assert.Single(Assert.Single(result.Series).Points).Value);
        request.Filter = "labels.size:00123";

        var sorted = await SendRequestAsAsync<List<PersistentEvent>>(r => r.AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events")
            .QueryString("filter", "_exists_:measurement.duration@ms")
            .QueryString("sort", "-measurement.duration@ms").StatusCodeShouldBeOk());
        Assert.Equal(observations[1].Id, sorted![0].Id);
        Assert.Equal(observations[0].Id, sorted[1].Id);

        var aggregates = await SendRequestAsAsync<CountResult>(r => r.AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "count")
            .QueryString("aggregations", "avg:measurement.duration@ms avg:measurement.allocated@By").StatusCodeShouldBeOk());
        Assert.Equal(new double?[] { 10, 999 }, NumericValues(aggregates!.Aggregations).Order().ToArray());

        request.Chart.Mode = "events";
        result = await ChartAsync(request);
        Assert.Equal(2, result.Total);
        Assert.Contains(Assert.Single(result.Series).Points, p => p.Value == 0 && p.EventId == observations[0].Id);

        var catalog = await SendRequestAsAsync<EventMeasurementCatalog>(r => r.AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "measurements").StatusCodeShouldBeOk());
        Assert.Contains(new EventMeasurementDescriptor("duration", "ms"), catalog!.Measurements);
        Assert.Contains(new EventMeasurementDescriptor("duration", "s"), catalog.Measurements);
        Assert.Contains(new EventMeasurementDescriptor("allocated", "By"), catalog.Measurements);

        request.Chart.GroupBy = "result";
        request.Filter = "outcome:failure AND result:timed_out";
        result = await ChartAsync(request);
        series = Assert.Single(result.Series);
        Assert.Equal("timed_out", series.Name);
        Assert.Equal(observations[1].Id, Assert.Single(series.Points).EventId);

        request.Chart.Mode = "buckets";
        result = await ChartAsync(request);
        series = Assert.Single(result.Series);
        Assert.Equal("timed_out", series.Name);
        Assert.Equal(20, Assert.Single(series.Points).Value);
    }

    [Theory]
    [InlineData("items/s @core")]
    [InlineData("{request}/s")]
    public async Task Chart_ComplexUnit_UsesTheSameScopeAsDrilldown(string unit)
    {
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Source("throughput");
            d.Event().TestProject().Source("throughput");
        });
        var ev = events[0];
        ev.Measurements = [new() { Name = "rate", Unit = unit, Value = 10 }];
        events[1].Measurements = [new() { Name = "rate", Unit = "1", Value = 20 }, new() { Name = "other", Unit = unit, Value = 30 }];
        await GetService<IEventRepository>().SaveAsync(events, o => o.ImmediateConsistency());
        var request = new EventChartRequest { Chart = new() { Measurement = "rate", Unit = unit } };

        var result = await ChartAsync(request);
        var drilled = await SendRequestAsAsync<List<PersistentEvent>>(r => r.AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events")
            .QueryString("filter", $"measurements:(measurements.name:\"rate\" AND measurements.unit:\"{unit}\")")
            .StatusCodeShouldBeOk());

        Assert.Equal(1, result.Total);
        Assert.Equal(ev.Id, Assert.Single(drilled!).Id);
    }

    [Fact]
    public async Task Chart_PercentilesAndEmptyBuckets_PreserveMissingObservations()
    {
        var (_, events) = await CreateDataAsync(d =>
        {
            d.Event().TestProject().Source("percentiles");
            d.Event().TestProject().Source("percentiles");
            d.Event().TestProject().Source("percentiles");
        });
        var now = TimeProvider.GetUtcNow();
        events[0].Date = now.AddMinutes(-30);
        events[1].Date = events[0].Date;
        events[2].Date = now.AddMinutes(-1);
        events[0].Measurements = [new() { Name = "duration", Unit = "ms", Value = 0 }];
        events[1].Measurements = [new() { Name = "duration", Unit = "ms", Value = 20 }];
        events[2].Measurements = [new() { Name = "duration", Unit = "ms", Value = 100 }];
        await GetService<IEventRepository>().SaveAsync(events, o => o.ImmediateConsistency());
        var request = new EventChartRequest { Chart = new() { Measurement = "duration", Unit = "ms", Aggregation = "p50" }, Filter = "source:percentiles", Time = "[now-1h TO now]" };

        var result = await ChartAsync(request);

        var points = Assert.Single(result.Series).Points;
        Assert.Equal(10, points[0].Value);
        Assert.Equal(100, points[^1].Value);
        Assert.Contains(points, p => p.Count == 0 && p.Value is null);
        Assert.True(points.Count <= 201);

        request.Chart.Aggregation = "sum";
        result = await ChartAsync(request);
        points = Assert.Single(result.Series).Points;
        Assert.Equal(20, points[0].Value);
        Assert.Contains(points, p => p.Count == 0 && p.Value is null);

        request.Chart.Aggregation = "count";
        result = await ChartAsync(request);
        Assert.Equal(2, Assert.Single(result.Series).Points[0].Value);
    }

    [Fact]
    public async Task Chart_MoreThanTwentySeries_ReportsTruncation()
    {
        var (_, events) = await CreateDataAsync(d =>
        {
            for (int i = 0; i < 21; i++)
                d.Event().TestProject().Source($"series-{i}");
        });
        foreach (var ev in events)
            ev.Measurements = [new() { Name = "duration", Unit = "ms", Value = 0 }];
        await GetService<IEventRepository>().SaveAsync(events, o => o.ImmediateConsistency());

        var result = await ChartAsync(new EventChartRequest { Chart = new() { Measurement = "duration", Unit = "ms", GroupBy = "source" } });

        Assert.Equal(21, result.Total);
        Assert.Equal(20, result.Series.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public Task Chart_MissingMeasurementUnit_ReturnsValidationError()
    {
        return SendRequestAsync(r => r.Post().AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "chart")
            .Content(new EventChartRequest { Chart = new() { Measurement = "duration" } })
            .StatusCodeShouldBeUnprocessableEntity());
    }

    [Fact]
    public Task Chart_OtherOrganization_ReturnsNotFound()
    {
        return SendRequestAsync(r => r.Post().AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "events", "chart")
            .Content(new EventChartRequest { Chart = new() { Aggregation = "count" } })
            .StatusCodeShouldBeNotFound());
    }

    private async Task<EventChartResult> ChartAsync(EventChartRequest request) => (await SendRequestAsAsync<EventChartResult>(r => r.Post()
        .AsTestOrganizationUser().AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "events", "chart")
        .Content(request).StatusCodeShouldBeOk()))!;

    private static IEnumerable<double?> NumericValues(IReadOnlyDictionary<string, IAggregate> values)
    {
        foreach (var aggregate in values.Values)
        {
            if (aggregate is ValueAggregate value)
                yield return value.Value;
            if (aggregate is SingleBucketAggregate bucket)
                foreach (var child in NumericValues(bucket.Aggregations))
                    yield return child;
        }
    }
}
