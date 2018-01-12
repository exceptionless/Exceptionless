using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Services;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nest;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Exceptionless.Api.Tests.Stats {
    public sealed class AggregationTests : ElasticTestBase {
        private readonly EventPipeline _pipeline;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly StackService _stackService;

        public AggregationTests(ITestOutputHelper output) : base(output) {
            _pipeline = GetService<EventPipeline>();
            _eventRepository = GetService<IEventRepository>();
            _stackRepository = GetService<IStackRepository>();
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _stackService = GetService<StackService>();
        }

        [Fact]
        public async Task CanGetCardinalityAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await _eventRepository.CountBySearchAsync(null, $"project:{TestConstants.ProjectId}", "cardinality:stack_id cardinality:id");
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Aggregations.Cardinality("cardinality_id").Value.GetValueOrDefault());
            Assert.Equal(await _stackRepository.CountAsync(), result.Aggregations.Cardinality("cardinality_stack_id").Value.GetValueOrDefault());
        }

        [Fact]
        public async Task CanGetDateHistogramWithCardinalityAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await _eventRepository.CountBySearchAsync(null, $"project:{TestConstants.ProjectId}", "date:(date cardinality:id) cardinality:id");
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Aggregations.DateHistogram("date_date").Buckets.Sum(t => t.Total));
            Assert.Equal(1, result.Aggregations.DateHistogram("date_date").Buckets.First().Aggregations.Count);
            Assert.Equal(eventCount, result.Aggregations.Cardinality("cardinality_id").Value.GetValueOrDefault());
            Assert.Equal(eventCount, result.Aggregations.DateHistogram("date_date").Buckets.Sum(t => t.Aggregations.Cardinality("cardinality_id").Value.GetValueOrDefault()));

            var stacks = await _stackRepository.GetByOrganizationIdAsync(TestConstants.OrganizationId, o => o.PageLimit(100));
            foreach (var stack in stacks.Documents) {
                var stackResult = await _eventRepository.CountBySearchAsync(null, $"stack:{stack.Id}", "cardinality:id");
                Assert.Equal(stack.TotalOccurrences, stackResult.Total);
                Assert.Equal(stack.TotalOccurrences, stackResult.Aggregations.Cardinality("cardinality_id").Value.GetValueOrDefault());
            }
        }

        [Fact]
        public async Task CanGetExcludedTermsAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await _eventRepository.CountBySearchAsync(null, $"project:{TestConstants.ProjectId}", "terms:(is_first_occurrence @include:true)");
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(await _stackRepository.CountAsync(), result.Aggregations.Terms<string>("terms_is_first_occurrence").Buckets.First(b => b.KeyAsString == Boolean.TrueString.ToLower()).Total.GetValueOrDefault());
        }

        [Fact]
        public async Task CanGetNumericAggregationsAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(3);
            await CreateDataAsync(0, false);

            var values = new decimal?[] { null, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            foreach (var value in values)
                await CreateEventsAsync(1, null, value);

            Log.SetLogLevel<EventRepository>(LogLevel.Trace);
            var result = await _eventRepository.CountBySearchAsync(null, $"project:{TestConstants.ProjectId}", "avg:value~0 cardinality:value~0 sum:value~0 min:value~0 max:value~0");

            Assert.Equal(values.Length, result.Total);
            Assert.Equal(5, result.Aggregations.Count);
            Assert.Equal(50, result.Aggregations.Average("avg_value").Value.GetValueOrDefault());
            Assert.Equal(11, result.Aggregations.Cardinality("cardinality_value").Value.GetValueOrDefault());
            Assert.Equal(550, result.Aggregations.Sum("sum_value").Value.GetValueOrDefault());
            Assert.Equal(0, result.Aggregations.Min("min_value").Value.GetValueOrDefault());
            Assert.Equal(100, result.Aggregations.Max("max_value").Value.GetValueOrDefault());
        }

        [Fact]
        public async Task CanGetTagTermAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await _eventRepository.CountBySearchAsync(null, null, "terms:tags");
            Assert.Equal(eventCount, result.Total);
            // each event can be in multiple tag buckets since an event can have up to 3 sample tags
            Assert.InRange(result.Aggregations.Terms<string>("terms_tags").Buckets.Sum(t => t.Total.GetValueOrDefault()), eventCount, eventCount * 3);
            Assert.InRange(result.Aggregations.Terms<string>("terms_tags").Buckets.Count, 1, TestConstants.EventTags.Count);
            foreach (var term in result.Aggregations.Terms<string>("terms_tags").Buckets)
                Assert.InRange(term.Total.GetValueOrDefault(), 1, eventCount);
        }

        [Fact]
        public async Task CanGetVersionTermAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await _eventRepository.CountBySearchAsync(null, null, "terms:version");
            Assert.Equal(eventCount, result.Total);
            // NOTE: The events are created without a version.
            Assert.Equal(0, result.Aggregations.Terms<string>("terms_version").Buckets.Count);
        }

        [Fact]
        public async Task CanGetStackIdTermAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            long stackSize = await _stackRepository.CountAsync();
            var result = await _eventRepository.CountBySearchAsync(null, null, $"terms:(stack_id terms:(is_first_occurrence~{stackSize} @include:true))");
            Assert.Equal(eventCount, result.Total);

            var termsAggregation = result.Aggregations.Terms<string>("terms_stack_id");
            Assert.Equal(eventCount, termsAggregation.Buckets.Sum(b1 => b1.Total.GetValueOrDefault()) + (long)termsAggregation.Data["SumOtherDocCount"]);
            foreach (var term in termsAggregation.Buckets) {
                Assert.Equal(1, term.Aggregations.Terms<string>("terms_is_first_occurrence").Buckets.Sum(b => b.Total.GetValueOrDefault()));
            }
        }

        [Fact]
        public async Task CanGetStackIdTermMinMaxAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            long stackSize = await _stackRepository.CountAsync();
            var result = await _eventRepository.CountBySearchAsync(null, null, $"terms:(stack_id~{stackSize} min:date max:date)");
            Assert.Equal(eventCount, result.Total);

            var termsAggregation = result.Aggregations.Terms<string>("terms_stack_id");
            var largestStackBucket = termsAggregation.Buckets.First();

            var events = await _eventRepository.GetByFilterAsync(null, $"stack:{largestStackBucket.Key}", null, null, DateTime.MinValue, DateTime.MaxValue, o => o.PageLimit(eventCount));
            Assert.Equal(largestStackBucket.Total.GetValueOrDefault(), events.Total);

            var oldestEvent = events.Documents.OrderBy(e => e.Date).First();
            Assert.Equal(oldestEvent.Date.UtcDateTime.Floor(TimeSpan.FromMilliseconds(1)), largestStackBucket.Aggregations.Min<DateTime>("min_date").Value.Floor(TimeSpan.FromMilliseconds(1)));

            var newestEvent= events.Documents.OrderByDescending(e => e.Date).First();
            Assert.Equal(newestEvent.Date.UtcDateTime.Floor(TimeSpan.FromMilliseconds(1)), largestStackBucket.Aggregations.Min<DateTime>("max_date").Value.Floor(TimeSpan.FromMilliseconds(1)));
        }

        [Fact]
        public async Task CanGetProjectTermAggregationsAsync() {
            const int eventCount = 100;
            await CreateDataAsync(eventCount);
            Log.SetLogLevel<EventRepository>(LogLevel.Trace);

            var result = await _eventRepository.CountBySearchAsync(null, null, "terms:project_id");
            Assert.Equal(eventCount, result.Total);
            Assert.InRange(result.Aggregations.Terms<string>("terms_project_id").Buckets.Count, 1, 3); // 3 sample projects
            Assert.Equal(eventCount, result.Aggregations.Terms<string>("terms_project_id").Buckets.Sum(t => t.Total.GetValueOrDefault()));
        }

        [Fact]
        public async Task CanGetSessionAggregationsAsync() {
            await CreateDataAsync();
            await CreateSessionEventsAsync();

            var result = await _eventRepository.CountBySearchAsync(null, "type:session", "avg:value cardinality:user");
            Assert.Equal(3, result.Total);
            Assert.Equal(3, result.Aggregations.Cardinality("cardinality_user").Value.GetValueOrDefault());
            Assert.Equal(3600.0 / result.Total, result.Aggregations.Average("avg_value").Value.GetValueOrDefault());
        }

        private async Task CreateDataAsync(int eventCount = 0, bool multipleProjects = true) {
            var orgs = OrganizationData.GenerateSampleOrganizations();
            await _organizationRepository.AddAsync(orgs, o => o.Cache());

            var projects = ProjectData.GenerateSampleProjects();
            await _projectRepository.AddAsync(projects, o => o.Cache());
            await _configuration.Client.RefreshAsync(Indices.All);

            if (eventCount > 0)
                await CreateEventsAsync(eventCount, multipleProjects ? projects.Select(p => p.Id).ToArray() : new[] { TestConstants.ProjectId });
        }

        private async Task CreateEventsAsync(int eventCount, string[] projectIds, decimal? value = -1) {
            var events = EventData.GenerateEvents(eventCount, projectIds: projectIds, startDate: SystemClock.OffsetUtcNow.SubtractDays(3), endDate: SystemClock.OffsetUtcNow, value: value);
            foreach (var eventGroup in events.GroupBy(ev => ev.ProjectId))
                await _pipeline.RunAsync(eventGroup, OrganizationData.GenerateSampleOrganization(), ProjectData.GenerateSampleProject());
            await _stackService.SaveStackUsagesAsync();

            await _configuration.Client.RefreshAsync(Indices.All);
        }

        private async Task<List<PersistentEvent>> CreateSessionEventsAsync() {
            var startDate = SystemClock.OffsetUtcNow.SubtractHours(1);
            var events = new List<PersistentEvent> {
                EventData.GenerateSessionStartEvent(occurrenceDate: startDate, userIdentity: "1"),
                EventData.GenerateSessionEndEvent(occurrenceDate: startDate.AddMinutes(10), userIdentity: "1"),
                EventData.GenerateSessionStartEvent(occurrenceDate: startDate.AddMinutes(10), userIdentity: "2"),
                EventData.GenerateSessionEndEvent(occurrenceDate: startDate.AddMinutes(30), userIdentity: "2"),
                EventData.GenerateSessionStartEvent(occurrenceDate: startDate.AddMinutes(20), userIdentity: "3"),
                EventData.GenerateSessionEndEvent(occurrenceDate: startDate.AddMinutes(50), userIdentity: "3")
            };

            await _pipeline.RunAsync(events, OrganizationData.GenerateSampleOrganization(), ProjectData.GenerateSampleProject());
            await _configuration.Client.RefreshAsync(Indices.All);

            var results = await _eventRepository.GetByFilterAsync(null, null, EventIndexType.Alias.Date, null, DateTime.MinValue, DateTime.MaxValue, null);
            Assert.Equal(6, results.Total);
            Assert.Equal(3, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(TimeSpan.FromMinutes(20).TotalSeconds, (int)(sessionStarts.Sum(e => e.Value.GetValueOrDefault()) / sessionStarts.Count));

            return events;
        }
    }
}