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
using Exceptionless.Api.Tests.Extensions;
using Exceptionless.Api.Utility;
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
        private readonly ITokenRepository _tokenRepository;

        public EventControllerTests(ITestOutputHelper output) : base(output) {
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _tokenRepository = GetService<ITokenRepository>();
            _eventRepository = GetService<IEventRepository>();
            _eventQueue = GetService<IQueue<EventPost>>();
            _eventUserDescriptionQueue = GetService<IQueue<EventUserDescription>>();

            CreateOrganizationAndProjectsAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanPostUserDescriptionAsync() {
            await SendTokenRequest(TestConstants.ApiKey, r => r
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
        public async Task CanPostStringAsync() {
            const string message = "simple string";
            await SendTokenRequest(TestConstants.ApiKey, r => r
                .Post()
                .AppendPath("events")
                .Content(message, "text/plain")
                .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await _configuration.Client.RefreshAsync(Indices.All);

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

            //var response = await SendTokenRequest(TestConstants.ApiKey, r => r
            //   .Post()
            //   .AppendPath("events")
            //   .Content(CompressString(message))
            //   .StatusCodeShouldBeAccepted()
            //);

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await _configuration.Client.RefreshAsync(Indices.All);

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

            await SendTokenRequest(TestConstants.ApiKey, r => r
                .Post()
                .AppendPath("events")
                .Content(ev)
                .StatusCodeShouldBeAccepted()
            );

            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(1, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();
            await _configuration.Client.RefreshAsync(Indices.All);

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
                await SendTokenRequest(TestConstants.ApiKey, r => r
                   .Post()
                   .AppendPath("events")
                   .Content(events)
                   .StatusCodeShouldBeAccepted()
                );
            });

            await _configuration.Client.RefreshAsync(Indices.All);
            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Enqueued);
            Assert.Equal(0, stats.Completed);

            var processEventsJob = GetService<EventPostsJob>();
            var sw = Stopwatch.StartNew();
            await processEventsJob.RunUntilEmptyAsync();
            sw.Stop();
            _logger.LogInformation("{Duration:g}", sw.Elapsed);

            await _configuration.Client.RefreshAsync(Indices.All);
            stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Completed);
            Assert.Equal(batchSize * batchCount, await _eventRepository.CountAsync());
        }

        //private ByteArrayContent CompressString(string data) {
        //    byte[] bytes = Encoding.UTF8.GetBytes(data);
        //    using (var stream = new MemoryStream()) {
        //        using (var zipper = new GZipStream(stream, CompressionMode.Compress, true))
        //            zipper.Write(bytes, 0, bytes.Length);

        //        var content = new ByteArrayContent(stream.ToArray());
        //        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") {
        //            CharSet = "utf-8"
        //        };
        //        content.Headers.ContentEncoding.Add("gzip");
        //        return content;
        //    }
        //}

        //private ByteArrayContent JsonCompress(object data) {
        //    byte[] bytes = Encoding.UTF8.GetBytes(_serializer.SerializeToString(data));
        //    using (var stream = new MemoryStream()) {
        //        using (var zipper = new GZipStream(stream, CompressionMode.Compress, true))
        //            zipper.Write(bytes, 0, bytes.Length);

        //        var content = new ByteArrayContent(stream.ToArray());
        //        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") {
        //            CharSet = "utf-8"
        //        };
        //        content.Headers.ContentEncoding.Add("gzip");
        //        return content;
        //    }
        //}

        private Task CreateOrganizationAndProjectsAsync() {
            return Task.WhenAll(
                _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations(), o => o.ImmediateConsistency()),
                _projectRepository.AddAsync(ProjectData.GenerateSampleProjects(), o => o.ImmediateConsistency()),
                _tokenRepository.AddAsync(TokenData.GenerateSampleApiKeyToken(), o => o.ImmediateConsistency())
            );
        }
    }
}
