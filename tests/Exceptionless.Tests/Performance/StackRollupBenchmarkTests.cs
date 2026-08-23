using System.Diagnostics;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Performance;

/// <summary>
/// Opt-in comparison of the former growing terms-aggregation query and the ES|QL lookup-join query.
/// This is deliberately excluded from normal CI because it seeds a representative high-cardinality data set.
/// </summary>
public sealed class StackRollupBenchmarkTests : IntegrationTestsBase
{
    private const int DefaultStackCount = 5_000;
    private const int DefaultEventsPerStack = 3;
    private const int PageSize = 25;
    private const int DeepPage = 100;
    private readonly ITestOutputHelper _output;

    public StackRollupBenchmarkTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _output = output;
    }

    public static bool BenchmarksEnabled
        => String.Equals(Environment.GetEnvironmentVariable("RUN_STACK_ROLLUP_BENCHMARKS"), "true", StringComparison.OrdinalIgnoreCase);

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        await GetService<SampleDataService>().CreateDataAsync();
    }

    [Fact(Skip = "Set RUN_STACK_ROLLUP_BENCHMARKS=true to run the Elasticsearch stack-rollup benchmark.", SkipUnless = nameof(BenchmarksEnabled))]
    [Trait("Category", "Performance")]
    public async Task LookupJoin_ComparedToFormerTermsAggregation()
    {
        int stackCount = GetPositiveEnvironmentValue("STACK_ROLLUP_BENCHMARK_STACKS", DefaultStackCount);
        int eventsPerStack = GetPositiveEnvironmentValue("STACK_ROLLUP_BENCHMARK_EVENTS_PER_STACK", DefaultEventsPerStack);
        int iterations = GetPositiveEnvironmentValue("STACK_ROLLUP_BENCHMARK_ITERATIONS", 7);
        const int warmups = 2;

        await SeedAsync(stackCount, eventsPerStack);

        var organizationRepository = GetService<IOrganizationRepository>();
        var projectRepository = GetService<IProjectRepository>();
        var organization = await organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        var project = await projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(organization);
        Assert.NotNull(project);

        var appFilter = new AppFilter(project, organization);
        DateTime utcEnd = TimeProvider.GetUtcNow().UtcDateTime.AddMinutes(1);
        DateTime utcStart = utcEnd.AddDays(-30);
        var lookupService = GetService<IStackRollupSearchService>();

        string? deepCursor = await GetCursorForPageAsync(lookupService, appFilter, utcStart, utcEnd, DeepPage);
        Assert.NotNull(deepCursor);

        var scenarios = new[]
        {
            new Scenario("first page", null, 1, null),
            new Scenario("deep page 100", null, DeepPage, deepCursor),
            new Scenario("stack filter", "status:open", 1, null)
        };

        var report = new List<string>
        {
            $"Data set: {stackCount:N0} stacks, {stackCount * eventsPerStack:N0} events, page size {PageSize}",
            "",
            "Scenario | Former terms median | Former terms p95 | Lookup join median | Lookup join p95 | Median change",
            "--- | ---: | ---: | ---: | ---: | ---:"
        };

        foreach (var scenario in scenarios)
        {
            string? legacyFailure = null;
            for (int i = 0; i < warmups; i++)
            {
                if (legacyFailure is null)
                {
                    try
                    {
                        await ExecuteLegacyAsync(appFilter, utcStart, utcEnd, scenario.Filter, scenario.Page);
                    }
                    catch (DocumentLimitExceededException ex)
                    {
                        legacyFailure = ex.Message;
                    }
                }

                await ExecuteLookupAsync(lookupService, appFilter, utcStart, utcEnd, scenario.Filter, scenario.After);
            }

            var legacy = new List<double>(iterations);
            var lookup = new List<double>(iterations);
            for (int i = 0; i < iterations; i++)
            {
                if (legacyFailure is not null)
                {
                    lookup.Add(await MeasureAsync(() => ExecuteLookupAsync(lookupService, appFilter, utcStart, utcEnd, scenario.Filter, scenario.After)));
                }
                else if (i % 2 == 0)
                {
                    legacy.Add(await MeasureAsync(() => ExecuteLegacyAsync(appFilter, utcStart, utcEnd, scenario.Filter, scenario.Page)));
                    lookup.Add(await MeasureAsync(() => ExecuteLookupAsync(lookupService, appFilter, utcStart, utcEnd, scenario.Filter, scenario.After)));
                }
                else
                {
                    lookup.Add(await MeasureAsync(() => ExecuteLookupAsync(lookupService, appFilter, utcStart, utcEnd, scenario.Filter, scenario.After)));
                    legacy.Add(await MeasureAsync(() => ExecuteLegacyAsync(appFilter, utcStart, utcEnd, scenario.Filter, scenario.Page)));
                }
            }

            double lookupMedian = Percentile(lookup, 0.50);
            if (legacyFailure is not null)
            {
                report.Add($"{scenario.Name} | failed: 20,000 stack limit | failed | {lookupMedian:N1} ms | {Percentile(lookup, 0.95):N1} ms | n/a");
            }
            else
            {
                double legacyMedian = Percentile(legacy, 0.50);
                double change = (lookupMedian - legacyMedian) / legacyMedian * 100;
                report.Add($"{scenario.Name} | {legacyMedian:N1} ms | {Percentile(legacy, 0.95):N1} ms | {lookupMedian:N1} ms | {Percentile(lookup, 0.95):N1} ms | {change:+0.0;-0.0;0.0}%");
            }
        }

        for (int i = 0; i < warmups; i++)
        {
            await ExecuteLookupApiListAsync();
            await ExecuteLookupApiStatsAsync(stackCount, eventsPerStack);
        }

        var apiList = new List<double>(iterations);
        var apiStats = new List<double>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            apiList.Add(await MeasureAsync(ExecuteLookupApiListAsync));
            apiStats.Add(await MeasureAsync(() => ExecuteLookupApiStatsAsync(stackCount, eventsPerStack)));
        }

        report.Add($"full API list | n/a | n/a | {Percentile(apiList, 0.50):N1} ms | {Percentile(apiList, 0.95):N1} ms | n/a");
        report.Add($"full API stats | n/a | n/a | {Percentile(apiStats, 0.50):N1} ms | {Percentile(apiStats, 0.95):N1} ms | n/a");

        foreach (string line in report)
            _output.WriteLine(line);

        string? outputPath = Environment.GetEnvironmentVariable("STACK_ROLLUP_BENCHMARK_OUTPUT");
        if (!String.IsNullOrWhiteSpace(outputPath))
            await File.WriteAllLinesAsync(outputPath, report, TestContext.Current.CancellationToken);
    }

    private async Task SeedAsync(int stackCount, int eventsPerStack)
    {
        var stackRepository = GetService<IStackRepository>();
        var eventRepository = GetService<IEventRepository>();
        DateTime now = TimeProvider.GetUtcNow().UtcDateTime;
        var stacks = new List<Stack>(stackCount);
        var events = new List<PersistentEvent>(stackCount * eventsPerStack);

        for (int stackIndex = 0; stackIndex < stackCount; stackIndex++)
        {
            string stackId = ObjectId.GenerateNewId().ToString();
            DateTime first = now.AddMinutes(-(stackIndex % 720) - eventsPerStack).AddSeconds(-(stackIndex % 60));
            DateTime last = first.AddMinutes(eventsPerStack - 1);
            string signature = $"benchmark-{stackIndex}";
            stacks.Add(new Stack
            {
                Id = stackId,
                OrganizationId = SampleDataService.TEST_ORG_ID,
                ProjectId = SampleDataService.TEST_PROJECT_ID,
                Type = Event.KnownTypes.Error,
                Status = stackIndex % 10 == 0 ? StackStatus.Fixed : StackStatus.Open,
                SignatureHash = signature,
                DuplicateSignature = $"{SampleDataService.TEST_PROJECT_ID}:{signature}",
                Title = $"Benchmark stack {stackIndex}",
                TotalOccurrences = eventsPerStack,
                FirstOccurrence = first,
                LastOccurrence = last,
                CreatedUtc = first,
                UpdatedUtc = last
            });

            for (int eventIndex = 0; eventIndex < eventsPerStack; eventIndex++)
            {
                DateTime date = first.AddMinutes(eventIndex);
                var persistentEvent = new PersistentEvent
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    OrganizationId = SampleDataService.TEST_ORG_ID,
                    ProjectId = SampleDataService.TEST_PROJECT_ID,
                    StackId = stackId,
                    Type = Event.KnownTypes.Error,
                    Source = "StackRollupBenchmark",
                    Message = $"Benchmark stack {stackIndex}",
                    Date = new DateTimeOffset(date, TimeSpan.Zero),
                    CreatedUtc = date,
                    Count = 1,
                    IsFirstOccurrence = eventIndex == 0
                };
                persistentEvent.SetUserIdentity($"user-{(stackIndex + eventIndex) % 1_000}");
                persistentEvent.CopyDataToIndex();
                events.Add(persistentEvent);
            }
        }

        await stackRepository.AddAsync(stacks, options => options.ImmediateConsistency());
        await eventRepository.AddAsync(events, options => options.ImmediateConsistency());
    }

    private async Task ExecuteLegacyAsync(AppFilter appFilter, DateTime utcStart, DateTime utcEnd, string? filter, int page)
    {
        int skip = (page - 1) * PageSize;
        var systemFilter = new RepositoryQuery<PersistentEvent>()
            .AppFilter(appFilter)
            .EnforceEventStackFilter()
            .DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date)
            .Index(utcStart, utcEnd);
        string aggregation = $"terms:(stack_id~{skip + PageSize + 1} cardinality:user -sum:count~1 min:date max:date)";

        var response = await GetService<IEventRepository>().CountAsync(query => query
            .SystemFilter(systemFilter)
            .FilterExpression(filter)
            .EnforceEventStackFilter()
            .AggregationsExpression(aggregation), options => options.TrackTotalHits(false));
        var terms = response.Aggregations.Terms<string>("terms_stack_id");
        Assert.NotNull(terms);
        Assert.NotEmpty(terms.Buckets.Skip(skip).Take(PageSize));
    }

    private static async Task ExecuteLookupAsync(
        IStackRollupSearchService service,
        AppFilter appFilter,
        DateTime utcStart,
        DateTime utcEnd,
        string? filter,
        string? after)
    {
        var result = await service.SearchAsync(new StackRollupSearchRequest(
            appFilter,
            utcStart,
            utcEnd,
            TimeSpan.Zero,
            null,
            filter,
            "-total",
            PageSize,
            null,
            after,
            IncludeTotal: false));
        Assert.NotEmpty(result.Rows);
    }

    private static async Task<string?> GetCursorForPageAsync(
        IStackRollupSearchService service,
        AppFilter appFilter,
        DateTime utcStart,
        DateTime utcEnd,
        int page)
    {
        string? after = null;
        for (int currentPage = 1; currentPage < page; currentPage++)
        {
            var result = await service.SearchAsync(new StackRollupSearchRequest(
                appFilter,
                utcStart,
                utcEnd,
                TimeSpan.Zero,
                null,
                null,
                "-total",
                PageSize,
                null,
                after,
                IncludeTotal: false));
            after = result.After;
            if (after is null)
                throw new InvalidOperationException($"Lookup cursor ended after page {currentPage} with {result.Rows.Count} rows (has more: {result.HasMore}).");
        }

        return after;
    }

    private async Task ExecuteLookupApiListAsync()
    {
        var results = await SendRequestAsAsync<IReadOnlyCollection<StackSummaryModel>>(request => request
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "stack-rollups")
            .QueryString("filter", "source:StackRollupBenchmark")
            .QueryString("sort", "-total")
            .QueryString("time", "last 24 hours")
            .QueryString("limit", PageSize)
            .StatusCodeShouldBeOk());
        Assert.NotNull(results);
        Assert.Equal(PageSize, results.Count);
    }

    private async Task ExecuteLookupApiStatsAsync(int stackCount, int eventsPerStack)
    {
        var result = await SendRequestAsAsync<StackRollupStatsResult>(request => request
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "stack-rollups", "stats")
            .QueryString("filter", "source:StackRollupBenchmark")
            .QueryString("time", "last 24 hours")
            .StatusCodeShouldBeOk());
        Assert.NotNull(result);
        Assert.InRange(Math.Abs(result.TotalStacks - stackCount), 0, Math.Max(1, stackCount / 100));
        Assert.Equal((long)stackCount * eventsPerStack, result.TotalEvents);
    }

    private static async Task<double> MeasureAsync(Func<Task> action)
    {
        var stopwatch = Stopwatch.StartNew();
        await action();
        return stopwatch.Elapsed.TotalMilliseconds;
    }

    private static double Percentile(IEnumerable<double> values, double percentile)
    {
        double[] ordered = values.Order().ToArray();
        int index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static int GetPositiveEnvironmentValue(string name, int defaultValue)
    {
        return Int32.TryParse(Environment.GetEnvironmentVariable(name), out int value) && value > 0 ? value : defaultValue;
    }

    private sealed record Scenario(string Name, string? Filter, int Page, string? After);
}
