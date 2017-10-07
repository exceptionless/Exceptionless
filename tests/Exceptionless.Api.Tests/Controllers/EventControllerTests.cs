using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Api.Tests.Extensions;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;
using Run = Exceptionless.Api.Tests.Utility.Run;

namespace Exceptionless.Api.Tests.Controllers {
    public class EventControllerTests : IntegrationTestsBase {
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;

        public EventControllerTests(ITestOutputHelper output) : base(output) {
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _eventRepository = GetService<IEventRepository>();
            _eventQueue = GetService<IQueue<EventPost>>();
            _eventUserDescriptionQueue = GetService<IQueue<EventUserDescription>>();

            CreateOrganizationAndProjectsAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanPostStringAsync() {
            await SendRequest(r => r
                .Post()
                .AppendPath("events")
                .Content("simple string")
                .StatusCodeShouldBeAccepted()
            );

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
            Assert.Equal(1, await EventCountAsync());
        }

        [Fact]
        public async Task CanPostCompressedStringAsync() {
            await SendRequest(r => r
               .Post()
               .AppendPath("events")
               .Content(Encoding.UTF8.GetBytes("simple string").Compress())
               .StatusCodeShouldBeAccepted()
            );

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
            Assert.Equal(1, await EventCountAsync());
        }

        [Fact]
        public async Task CanPostUserDescriptionAsync() {
            await SendRequest(r => r
               .Post()
               .AppendPath("events/by-ref/TestReferenceId/user-description")
               .Content(new EventUserDescription { Description = "Test Description", EmailAddress = TestConstants.UserEmail })
               .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventUserDescriptionQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var userDescriptionJob = GetService<EventUserDescriptionsJob>();
            await userDescriptionJob.RunAsync();

            stats = await _eventUserDescriptionQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Dequeued);
            Assert.Equal(1, stats.Abandoned); // Event doesn't exist
        }

        [Fact]
        public async Task CanPostManyEventsAsync() {
            const int batchSize = 250;
            const int batchCount = 10;

            await Run.InParallelAsync(batchCount, async i => {
                var events = new RandomEventGenerator().Generate(batchSize);
                var compressedEvents = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events)).Compress();

                await SendRequest(r => r
                   .Post()
                   .AppendPath("events")
                   .Content(compressedEvents)
                   .StatusCodeShouldBeAccepted()
                );
            });

            await _configuration.Client.RefreshAsync(Indices.All);
            Assert.Equal(batchCount, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            var sw = Stopwatch.StartNew();
            processEventsJob.RunUntilEmpty();
            sw.Stop();
            _logger.LogInformation(sw.Elapsed.ToString());

            await _configuration.Client.RefreshAsync(Indices.All);
            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Completed);
            int minimum = batchSize * batchCount;
            Assert.InRange(await EventCountAsync(), minimum, minimum * 2);
        }

        private async Task<long> EventCountAsync() {
            await _configuration.Client.RefreshAsync(Indices.All);
            return await _eventRepository.CountAsync();
        }

        private Task CreateOrganizationAndProjectsAsync() {
            return Task.WhenAll(
                _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations(), o => o.ImmediateConsistency()),
                _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.ImmediateConsistency())
            );
        }
    }
}
