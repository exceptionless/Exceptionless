using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.DateTimeExtensions;
using Exceptionless.Tests.Utility;
using Foundatio.Metrics;
using Nest;
using Xunit;

namespace Exceptionless.Api.Tests.Stats {
    public class StatsTests {
        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();
        private readonly IStackRepository _stackRepository = IoC.GetInstance<IStackRepository>();
        private readonly EventStats _stats = IoC.GetInstance<EventStats>();
        private readonly InMemoryMetricsClient _metricsClient = IoC.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
        private readonly EventPipeline _eventPipeline = IoC.GetInstance<EventPipeline>();

        [Fact]
        public async Task CanGetEventStats() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            RemoveData();
            await CreateDataAsync(eventCount, false);

            _client.Refresh(d => d.Force());
            _metricsClient.DisplayStats();
            var result = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, null, userFilter: "project:" + TestConstants.ProjectId);
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Timeline.Sum(t => t.Total));
            Assert.Equal(_stackRepository.Count(), result.Unique);
            Assert.Equal(_stackRepository.Count(), result.Timeline.Sum(t => t.New));

            var stacks = _stackRepository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithLimit(100));
            foreach (var stack in stacks) {
                result = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, null, userFilter: "stack:" + stack.Id);
                Console.WriteLine("{0} - {1} : {2}", stack.Id, stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public async Task CanGetEventStatsWithoutDateRange() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            RemoveData();
            await CreateDataAsync(eventCount, false);

            _client.Refresh(d => d.Force());
            _metricsClient.DisplayStats();
            var result = _stats.GetOccurrenceStats(DateTime.MinValue, DateTime.MaxValue, null, userFilter: "project:" + TestConstants.ProjectId);
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Timeline.Sum(t => t.Total));
            Assert.Equal(_stackRepository.Count(), result.Unique);
            Assert.Equal(_stackRepository.Count(), result.Timeline.Sum(t => t.New));

            var stacks = _stackRepository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithLimit(100));
            foreach (var stack in stacks) {
                result = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, null, userFilter: "stack:" + stack.Id);
                Console.WriteLine("{0} - {1} : {2}", stack.Id, stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public async Task CanGetEventStatsForTimeZone() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 1;
            RemoveData();
            await CreateDataAsync(eventCount);

            _client.Refresh(d => d.Force());
            _metricsClient.DisplayStats();
            var resultUtc = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, null);
            Assert.Equal(eventCount, resultUtc.Total);
            Assert.Equal(eventCount, resultUtc.Timeline.Sum(t => t.Total));

            var resultLocal = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, null, displayTimeOffset: TimeSpan.FromHours(-4));
            Assert.Equal(eventCount, resultLocal.Total);
            Assert.Equal(eventCount, resultLocal.Timeline.Sum(t => t.Total));
        }

        [Fact]
        public async Task CanGetEventTermStatsByTag() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            RemoveData();
            await CreateDataAsync(eventCount, false);

            _client.Refresh(d => d.Force());
            _metricsClient.DisplayStats();
            var result = _stats.GetTermsStats(startDate, DateTime.UtcNow, "tags", null, userFilter: "project:" + TestConstants.ProjectId);
            Assert.Equal(eventCount, result.Total);
            // each event can be in multiple tag buckets since an event can have up to 3 sample tags
            Assert.InRange(result.Terms.Sum(t => t.Total), eventCount, eventCount * 3);
            Assert.InRange(result.Terms.Sum(t => t.New), 1, 25 * TestConstants.EventTags.Count);
            Assert.InRange(result.Terms.Count, 1, TestConstants.EventTags.Count);
            foreach (var term in result.Terms) {
                Assert.InRange(term.New, 1, 25);
                //Assert.InRange(term.Unique, 1, 25);
                Assert.Equal(term.Total, term.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public async Task CanGetEventTermStatsByStack() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            RemoveData();
            await CreateDataAsync(eventCount, false);

            _client.Refresh(d => d.Force());
            _metricsClient.DisplayStats();
            var result = _stats.GetTermsStats(startDate, DateTime.UtcNow, "stack_id", null, userFilter: "project:" + TestConstants.ProjectId);
            Assert.Equal(eventCount, result.Total);
            Assert.InRange(result.Terms.Count, 1, 25);
            // TODO: Figure out why this is less than eventCount
            //Assert.Equal(eventCount, result.Terms.Sum(t => t.Total));
            Assert.InRange(result.Terms.Sum(t => t.New), 1, 25);
            foreach (var term in result.Terms) {
                Assert.Equal(1, term.New);
                Assert.Equal(1, term.Unique);
                Assert.Equal(term.Total, term.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public async Task CanGetEventTermStatsByProject() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            RemoveData();
            await CreateDataAsync(eventCount);

            _client.Refresh(d => d.Force());
            _metricsClient.DisplayStats();
            var result = _stats.GetTermsStats(startDate, DateTime.UtcNow, "project_id", null);
            Assert.Equal(eventCount, result.Total);
            Assert.InRange(result.Terms.Count, 1, 3); // 3 sample projects
            Assert.InRange(result.Terms.Sum(t => t.New), 1, 25 * 3);
            Assert.Equal(eventCount, result.Terms.Sum(t => t.Total));
            foreach (var term in result.Terms) {
                Assert.Equal(term.Total, term.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public async Task CanSetGauges() {
            await _metricsClient.GaugeAsync("mygauge", 12d);
            Assert.Equal(12d, _metricsClient.GetGaugeValue("mygauge"));
            await _metricsClient.GaugeAsync("mygauge", 10d);
            await _metricsClient.GaugeAsync("mygauge", 5d);
            await _metricsClient.GaugeAsync("mygauge", 4d);
            await _metricsClient.GaugeAsync("mygauge", 12d);
            await _metricsClient.GaugeAsync("mygauge", 20d);
            Assert.Equal(20d, _metricsClient.GetGaugeValue("mygauge"));
            _metricsClient.DisplayStats();
        }

        private async Task CreateDataAsync(int eventCount = 100, bool multipleProjects = true) {
            var orgs = OrganizationData.GenerateSampleOrganizations();
            _organizationRepository.Add(orgs);

            var projects = ProjectData.GenerateSampleProjects();
            _projectRepository.Add(projects);

            var events = EventData.GenerateEvents(eventCount, projectIds: multipleProjects ? projects.Select(p => p.Id).ToArray() : new[] { TestConstants.ProjectId }, startDate: DateTimeOffset.Now.SubtractDays(60), endDate: DateTimeOffset.Now);
            foreach (var eventGroup in events.GroupBy(ev => ev.ProjectId))
                await _eventPipeline.RunAsync(eventGroup);
        }

        private void RemoveData() {
            _organizationRepository.RemoveAll();
            _projectRepository.RemoveAll();
            _eventRepository.RemoveAll();
            _stackRepository.RemoveAll();
        }
    }
}
