using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.Billing;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Utility;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using Run = Exceptionless.Tests.Utility.Run;

namespace Exceptionless.Tests.Controllers {
    public class EventControllerTests : IntegrationTestsBase {
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;

        public EventControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) {
            _eventRepository = GetService<IEventRepository>();
            _eventQueue = GetService<IQueue<EventPost>>();
            _eventUserDescriptionQueue = GetService<IQueue<EventUserDescription>>();
        }

        protected override async Task ResetDataAsync() {
            await base.ResetDataAsync();
            await _eventQueue.DeleteQueueAsync();
            await CreateOrganizationAndProjectsAsync();
        }

        [Fact]
        public async Task CanPostUserDescriptionAsync() {
            await SendRequestAsync(r => r
               .Post()
               .AsClientUser()
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
        public async Task CanPostStringAsync() {
            const string message = "simple string";
            await SendRequestAsync(r => r
                .Post()
                .AsClientUser()
                .AppendPath("events")
                .Content(message, "text/plain")
                .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var ev = (await _eventRepository.GetAllAsync()).Documents.Single();
            Assert.Equal(message, ev.Message);
        }

        [Fact]
        public async Task CanPostCompressedStringAsync() {
            const string message = "simple string";

            byte[] data = Encoding.UTF8.GetBytes(message);
            var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionMode.Compress, true))
                gzip.Write(data, 0, data.Length);
            ms.Position = 0;

            var content = new StreamContent(ms);
            content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            content.Headers.ContentEncoding.Add("gzip");
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + TestConstants.ApiKey);
            var response = await _httpClient.PostAsync("events", content);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.True(response.Headers.Contains(Headers.ConfigurationVersion));

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var ev = (await _eventRepository.GetAllAsync()).Documents.Single();
            Assert.Equal(message, ev.Message);
        }

        [Fact]
        public async Task CanPostEventAsync() {
            var ev = new RandomEventGenerator().GeneratePersistent(false);
            if (String.IsNullOrEmpty(ev.Message))
                ev.Message = "Generated message.";

            await SendRequestAsync(r => r
                .Post()
                .AsClientUser()
                .AppendPath("events")
                .Content(ev)
                .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await RefreshDataAsync();

            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Completed);

            var actual = await _eventRepository.GetAllAsync();
            Assert.Single(actual.Documents);
            Assert.Equal(ev.Message, actual.Documents.Single().Message);
        }

        [Fact]
        public async Task CanPostManyEventsAsync() {
            const int batchSize = 50;
            const int batchCount = 10;

            await Run.InParallelAsync(batchCount, async i => {
                var events = new RandomEventGenerator().Generate(batchSize, false);
                await SendRequestAsync(r => r
                   .Post()
                   .AsClientUser()
                   .AppendPath("events")
                   .Content(events)
                   .StatusCodeShouldBeAccepted()
                );
            });

            await RefreshDataAsync();
            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            var sw = Stopwatch.StartNew();
            await processEventsJob.RunUntilEmptyAsync();
            sw.Stop();
            _logger.LogInformation("{Duration:g}", sw.Elapsed);

            await RefreshDataAsync();
            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Completed);
            Assert.Equal(batchSize * batchCount, await _eventRepository.CountAsync());
        }

        private Task CreateOrganizationAndProjectsAsync() {
            var organizationRepository = GetService<IOrganizationRepository>();
            var projectRepository = GetService<IProjectRepository>();
            var tokenRepository = GetService<ITokenRepository>();

            return Task.WhenAll(
                organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations(GetService<BillingManager>(), GetService<BillingPlans>()), o => o.ImmediateConsistency()),
                projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.ImmediateConsistency()),
                tokenRepository.AddAsync(TokenData.GenerateSampleApiKeyToken(), o => o.ImmediateConsistency())
            );
        }
    }
}
