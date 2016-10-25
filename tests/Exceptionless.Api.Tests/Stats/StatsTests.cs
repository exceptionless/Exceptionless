using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Processors;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Logging;
using Foundatio.Repositories.Models;
using Foundatio.Utility;
using Nest;
using Xunit;
using Xunit.Abstractions;
using LogLevel = Foundatio.Logging.LogLevel;

namespace Exceptionless.Api.Tests.Stats {
    public sealed class StatsTests : ElasticTestBase {
        private readonly EventStats _stats;
        private readonly EventPipeline _pipeline;
        private readonly IEventRepository _eventRepository;
        private readonly IStackRepository _stackRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;

        public StatsTests(ITestOutputHelper output) : base(output) {
            _stats = GetService<EventStats>();
            _pipeline = GetService<EventPipeline>();
            _eventRepository = GetService<IEventRepository>();
            _stackRepository = GetService<IStackRepository>();
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();

            Log.SetLogLevel<EventStats>(LogLevel.Trace);
        }

        [Fact]
        public async Task CanGetNumbersAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);

            var fields = FieldAggregationProcessor.Process("distinct:stack_id,term:is_first_occurrence:-F", false);
            Assert.True(fields.IsValid);
            Assert.Equal(2, fields.Aggregations.Count);

            var result = await _stats.GetNumbersTimelineStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, userFilter: $"project:{TestConstants.ProjectId}");
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Timeline.Sum(t => t.Total));
            Assert.Equal(2, result.Numbers.Length);
            Assert.Equal(await _stackRepository.CountAsync(), result.Numbers[0]);
            Assert.Equal(await _stackRepository.CountAsync(), result.Timeline.Sum(t => t.Numbers[1]));

            var stacks = await _stackRepository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithLimit(100));
            foreach (var stack in stacks.Documents) {
                var nsr = await _stats.GetNumbersStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, userFilter: "stack:" + stack.Id);
                Assert.Equal(stack.TotalOccurrences, nsr.Total);
            }
        }

        [Fact]
        public async Task CanGetEventStatsAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            await CreateDataAsync(0, false);

            var  values = new decimal?[] { null, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
            foreach (var value in values)
                await CreateEventsAsync(1, null, value);

            var fields = FieldAggregationProcessor.Process("avg:value:0,distinct:value:0,sum:value,min:value:0,max:value", false);
            Assert.True(fields.IsValid);
            Assert.Equal(5, fields.Aggregations.Count);

            var ntsr = await _stats.GetNumbersTimelineStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, userFilter: $"project:{TestConstants.ProjectId}");
            Assert.Equal(values.Length, ntsr.Total);
            Assert.Equal(values.Length, ntsr.Timeline.Sum(t => t.Total));
            Assert.Equal(5, ntsr.Numbers.Length);
            Assert.Equal(50, ntsr.Numbers[0]); // average
            Assert.Equal(11, ntsr.Numbers[1]); // distinct
            Assert.Equal(550, ntsr.Numbers[2]); // sum
            Assert.Equal(0, ntsr.Numbers[3]); // min
            Assert.Equal(100, ntsr.Numbers[4]); // max

            var nsr = await _stats.GetNumbersStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, userFilter: $"project:{TestConstants.ProjectId}");
            Assert.Equal(values.Length, nsr.Total);
            Assert.Equal(5, nsr.Numbers.Length);
            Assert.Equal(50, nsr.Numbers[0]); // average
            Assert.Equal(11, nsr.Numbers[1]); // distinct
            Assert.Equal(550, nsr.Numbers[2]); // sum
            Assert.Equal(0, nsr.Numbers[3]); // min
            Assert.Equal(100, nsr.Numbers[4]); // max
        }

        [Fact]
        public async Task CanGetEventStatsWithoutDateRangeAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);

            var fields = FieldAggregationProcessor.Process("distinct:stack_id,term:is_first_occurrence:-F", false);
            Assert.True(fields.IsValid);
            Assert.Equal(2, fields.Aggregations.Count);

            var result = await _stats.GetNumbersTimelineStatsAsync(fields.Aggregations, DateTime.MinValue, DateTime.MaxValue, null, userFilter: $"project:{TestConstants.ProjectId}");
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Timeline.Sum(t => t.Total));
            Assert.Equal(await _stackRepository.CountAsync(), result.Numbers[0]);
            Assert.Equal(await _stackRepository.CountAsync(), result.Timeline.Sum(t => t.Numbers[1]));

            var stacks = await _stackRepository.GetByOrganizationIdAsync(TestConstants.OrganizationId, new PagingOptions().WithLimit(100));
            foreach (var stack in stacks.Documents) {
                var ns = await _stats.GetNumbersStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, userFilter: "stack:" + stack.Id);
                Console.WriteLine("{0} - {1} : {2}", stack.Id, stack.TotalOccurrences, ns.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public async Task CanGetEventStatsForTimeZoneAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            const int eventCount = 1;
            await CreateDataAsync(eventCount);

            var fields = FieldAggregationProcessor.Process("distinct:stack_id", false);
            Assert.True(fields.IsValid);
            Assert.Equal(1, fields.Aggregations.Count);

            var resultUtc = await _stats.GetNumbersTimelineStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null);
            Assert.Equal(eventCount, resultUtc.Total);
            Assert.Equal(eventCount, resultUtc.Timeline.Sum(t => t.Total));

            var resultLocal = await _stats.GetNumbersTimelineStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, displayTimeOffset: TimeSpan.FromHours(-4));
            Assert.Equal(eventCount, resultLocal.Total);
            Assert.Equal(eventCount, resultLocal.Timeline.Sum(t => t.Total));
        }

        [Fact]
        public async Task CanGetEventTermStatsByTagAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);

            Log.MinimumLevel = LogLevel.Trace;
            var fields = FieldAggregationProcessor.Process("term:is_first_occurrence:-F", false);
            Assert.True(fields.IsValid);

            var sf = new ExceptionlessSystemFilterQuery(ProjectData.GenerateSampleProject(), OrganizationData.GenerateSampleOrganization());
            var result = await _stats.GetNumbersTermsStatsAsync("tags", fields.Aggregations, startDate, SystemClock.UtcNow, sf, "fixed:false");
            Assert.Equal(eventCount, result.Total);
            // each event can be in multiple tag buckets since an event can have up to 3 sample tags
            Assert.InRange(result.Terms.Sum(t => t.Total), eventCount, eventCount * 3);
            Assert.InRange(result.Terms.Sum(t => t.Numbers[0]), 1, 25 * TestConstants.EventTags.Count); // new
            Assert.InRange(result.Terms.Count, 1, TestConstants.EventTags.Count);
            foreach (var term in result.Terms)
                Assert.InRange(term.Numbers[0], 1, 25); // new
        }

        [Fact]
        public async Task CanGetEventTermStatsByStackAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            await CreateDataAsync(eventCount, false);

            var fields = FieldAggregationProcessor.Process("distinct:stack_id,term:is_first_occurrence:-F", false);
            Assert.True(fields.IsValid);

            var sf = new ExceptionlessSystemFilterQuery(ProjectData.GenerateSampleProject(), OrganizationData.GenerateSampleOrganization());
            var result = await _stats.GetNumbersTermsStatsAsync("stack_id", fields.Aggregations, startDate, SystemClock.UtcNow, sf);
            Assert.Equal(eventCount, result.Total);
            Assert.InRange(result.Terms.Count, 1, 25);
            // TODO: Figure out why this is less than eventCount
            Assert.Equal(eventCount, result.Terms.Sum(t => t.Total));
            Assert.InRange(result.Terms.Sum(t => t.Numbers[1]), 1, 25); // new
            foreach (var term in result.Terms) {
                Assert.Equal(1, term.Numbers[0]); //unique
                Assert.Equal(1, term.Numbers[1]); // new
            }
        }

        [Fact]
        public async Task CanGetEventTermStatsByProjectAsync() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = SystemClock.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            await CreateDataAsync(eventCount);

            var fields = FieldAggregationProcessor.Process("term:is_first_occurrence:-F", false);
            Assert.True(fields.IsValid);

            var result = await _stats.GetNumbersTermsStatsAsync("project_id", fields.Aggregations, startDate, SystemClock.UtcNow, null);
            Assert.Equal(eventCount, result.Total);
            Assert.InRange(result.Terms.Count, 1, 3); // 3 sample projects
            Assert.InRange(result.Terms.Sum(t => t.Numbers[0]), 1, 25 * 3); // new
            Assert.Equal(eventCount, result.Terms.Sum(t => t.Total));
        }

        [Fact]
        public async Task CanGetSessionStatsAsync() {
            await CreateDataAsync();

            var startDate = SystemClock.UtcNow.SubtractHours(1);
            await CreateSessionEventsAsync();

            var fields = FieldAggregationProcessor.Process("avg:value:0,distinct:user.keyword", false);
            Assert.True(fields.IsValid);
            Assert.Equal(2, fields.Aggregations.Count);

            var result = await _stats.GetNumbersTimelineStatsAsync(fields.Aggregations, startDate, SystemClock.UtcNow, null, "type:session");
            Assert.Equal(3, result.Total);
            Assert.Equal(3, result.Timeline.Sum(t => t.Total));
            Assert.Equal(3, result.Numbers[1]);
            Assert.Equal(3, result.Timeline.Sum(t => t.Numbers[1]));
            Assert.Equal(3600.0 / result.Total, result.Numbers[0]);
            Assert.Equal(3600, result.Timeline.Sum(t => t.Numbers[0]));
        }

        private async Task CreateDataAsync(int eventCount = 0, bool multipleProjects = true) {
            var orgs = OrganizationData.GenerateSampleOrganizations();
            await _organizationRepository.AddAsync(orgs, true);

            var projects = ProjectData.GenerateSampleProjects();
            await _projectRepository.AddAsync(projects, true);
            await _configuration.Client.RefreshAsync(Indices.All);

            if (eventCount > 0)
                await CreateEventsAsync(eventCount, multipleProjects ? projects.Select(p => p.Id).ToArray() : new[] { TestConstants.ProjectId });
        }

        private async Task CreateEventsAsync(int eventCount, string[] projectIds, decimal? value = -1) {
            var events = EventData.GenerateEvents(eventCount, projectIds: projectIds, startDate: SystemClock.OffsetUtcNow.SubtractDays(60), endDate: SystemClock.OffsetUtcNow, value: value);
            foreach (var eventGroup in events.GroupBy(ev => ev.ProjectId))
                await _pipeline.RunAsync(eventGroup);

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

            await _pipeline.RunAsync(events);

            await _configuration.Client.RefreshAsync(Indices.All);
            var results = await _eventRepository.GetAllAsync(new SortingOptions().WithField(EventIndexType.Alias.Date));
            Assert.Equal(6, results.Total);
            Assert.Equal(3, results.Documents.Where(e => !String.IsNullOrEmpty(e.GetSessionId())).Select(e => e.GetSessionId()).Distinct().Count());

            var sessionStarts = results.Documents.Where(e => e.IsSessionStart()).ToList();
            Assert.Equal(TimeSpan.FromMinutes(20).TotalSeconds, (int)(sessionStarts.Sum(e => e.Value.GetValueOrDefault()) / sessionStarts.Count));

            return events;
        }
    }
}
