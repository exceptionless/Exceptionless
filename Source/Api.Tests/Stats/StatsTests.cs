using System;
using System.Diagnostics;
using System.Linq;
using CodeSmith.Core.Extensions;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Utility;
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
        private readonly EventPipeline _eventPipeline = IoC.GetInstance<EventPipeline>();

        [Fact]
        public void CanGetEventStats() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 100;
            RemoveData();
            CreateData(eventCount);

            _client.Refresh();
            var result = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow);
            Assert.Equal(eventCount, result.Total);
            Assert.Equal(eventCount, result.Timeline.Sum(t => t.Total));
            Assert.Equal(_stackRepository.Count(), result.Unique);
            Assert.Equal(_stackRepository.Count(), result.Timeline.Sum(t => t.New));

            var stacks = _stackRepository.GetByOrganizationId(TestConstants.OrganizationId, new PagingOptions().WithLimit(100));
            foreach (var stack in stacks) {
                result = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, "stack:" + stack.Id);
                Console.WriteLine("{0} - {1} : {2}", stack.Id, stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Total);
                //Assert.Equal(stack.TotalOccurrences, result.Timeline.Sum(t => t.Total));
            }
        }

        [Fact]
        public void CanGetEventStatsForTimeZone() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 1;
            RemoveData();
            CreateData(eventCount);

            _client.Refresh();
            var resultUtc = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow);
            Assert.Equal(eventCount, resultUtc.Total);
            Assert.Equal(eventCount, resultUtc.Timeline.Sum(t => t.Total));

            var resultLocal = _stats.GetOccurrenceStats(startDate, DateTime.UtcNow, utcOffset: TimeSpan.FromHours(-4));
            Assert.Equal(eventCount, resultLocal.Total);
            Assert.Equal(eventCount, resultLocal.Timeline.Sum(t => t.Total));
        }

        [Fact]
        public void CanGetEventTermStats() {
            // capture start date before generating data to make sure that our time range for stats includes all items
            var startDate = DateTime.UtcNow.SubtractDays(60);
            const int eventCount = 10;
            RemoveData();
            CreateData(eventCount);

            _client.Refresh();
            var result = _stats.GetTermsStats(startDate, DateTime.UtcNow, "tags");
            Assert.Equal(eventCount, result.Total);
        }

        protected void CreateData(int eventCount = 100) {
            var org = OrganizationData.GenerateSampleOrganization();
            _organizationRepository.Add(org);

            var projects = ProjectData.GenerateSampleProjects();
            _projectRepository.Add(projects);

            var events = EventData.GenerateEvents(eventCount, projectIds: projects.Select(p => p.Id).ToArray(), startDate: DateTime.Now.SubtractDays(60), endDate: DateTime.Now);
            
            foreach (var ev in events)
                _eventPipeline.Run(ev);
        }

        protected void RemoveData() {
            _organizationRepository.RemoveAll();
            _projectRepository.RemoveAll();
            _eventRepository.RemoveAll();
            _stackRepository.RemoveAll();
        }
    }
}
