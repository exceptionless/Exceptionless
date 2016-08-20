using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;
using Exceptionless.Api.Controllers;
using Exceptionless.Api.Tests.Utility;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using Microsoft.Owin;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Api.Tests.Controllers {
    public class EventControllerTests : ElasticTestBase {
        private readonly EventController _eventController;
        private readonly IEventRepository _eventRepository;
        private readonly IQueue<EventPost> _eventQueue;
        private readonly IQueue<EventUserDescription> _eventUserDescriptionQueue;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;

        public EventControllerTests(ITestOutputHelper output) : base(output) {
            _organizationRepository = GetService<IOrganizationRepository>();
            _projectRepository = GetService<IProjectRepository>();
            _eventController = GetService<EventController>();
            _eventRepository = GetService<IEventRepository>();
            _eventQueue = GetService<IQueue<EventPost>>();
            _eventUserDescriptionQueue = GetService<IQueue<EventUserDescription>>();

            CreateOrganizationAndProjectsAsync().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanPostStringAsync() {
            _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(new User { EmailAddress = TestConstants.UserEmail, Id = TestConstants.UserId, OrganizationIds = new[] { TestConstants.OrganizationId }, Roles = new[] { AuthorizationRoles.Client }}.ToIdentity(TestConstants.ProjectId)), false, false);

            var metricsClient = GetService<IMetricsClient>() as InMemoryMetricsClient;
            Assert.NotNull(metricsClient);

            Assert.True(await metricsClient.WaitForCounterAsync("eventpost.enqueued", work: async () => {
                var actionResult = await _eventController.PostAsync(Encoding.UTF8.GetBytes("simple string"));
                Assert.IsType<StatusCodeResult>(actionResult);
            }));

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
            Assert.Equal(1, await EventCountAsync());
        }

        [Fact]
        public async Task CanPostCompressedStringAsync() {
            _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(GetClientToken().ToIdentity()), true, false);
            var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync());
            Assert.IsType<StatusCodeResult>(actionResult);
            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
            Assert.Equal(1, await EventCountAsync());
        }

        [Fact]
        public async Task CanPostSingleEventAsync() {
            _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(GetClientToken().ToIdentity()), true, false);
            var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync());
            Assert.IsType<StatusCodeResult>(actionResult);
            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            await processEventsJob.RunAsync();

            Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync()).Completed);
            Assert.Equal(1, await EventCountAsync());
        }
        
        [Fact]
        public async Task CanPostUserDescriptionAsync() {
            _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(GetClientToken().ToIdentity()), true, false);
            var actionResult = await _eventController.SetUserDescriptionAsync("TestReferenceId", new EventUserDescription { Description = "Test Description", EmailAddress = TestConstants.UserEmail });
            Assert.IsType<StatusCodeResult>(actionResult);

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
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(GetClientToken().ToIdentity()), true, false);
                var events = new RandomEventGenerator().Generate(batchSize);
                var compressedEvents = await Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events)).CompressAsync();
                var actionResult = await _eventController.PostAsync(compressedEvents, version: 2, userAgent: "exceptionless/2.0.0.0");
                Assert.IsType<StatusCodeResult>(actionResult);
            });

            await _client.RefreshAsync();
            Assert.Equal(batchCount, (await _eventQueue.GetQueueStatsAsync()).Enqueued);
            Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync()).Completed);

            var processEventsJob = GetService<EventPostsJob>();
            var sw = Stopwatch.StartNew();
            await processEventsJob.RunUntilEmptyAsync();
            sw.Stop();
            Trace.WriteLine(sw.Elapsed);

            await _client.RefreshAsync();
            var stats = await _eventQueue.GetQueueStatsAsync();
            Assert.Equal(batchCount, stats.Completed);
            var minimum = batchSize * batchCount;
            Assert.InRange(await EventCountAsync(), minimum, minimum * 2);
        }

        private HttpRequestMessage CreateRequestMessage(ClaimsPrincipal user, bool isCompressed, bool isJson, string charset = "utf-8") {
            var request = new HttpRequestMessage();

            var context = new OwinContext();
            context.Request.User = Thread.CurrentPrincipal = user;
            request.SetOwinContext(context);
            request.SetConfiguration(new HttpConfiguration());
            request.Content = new HttpMessageContent(new HttpRequestMessage(HttpMethod.Post, "/api/v2/events"));
            if (isCompressed)
                request.Content.Headers.ContentEncoding.Add("gzip");
            request.Content.Headers.ContentType.MediaType = isJson ? "application/json" : "text/plain";
            request.Content.Headers.ContentType.CharSet = charset;

            return request;
        }

        private Core.Models.Token GetClientToken() {
            var token = new Core.Models.Token();
            token.Id = StringExtensions.GetNewToken();
            token.CreatedUtc = token.ModifiedUtc = DateTime.UtcNow;
            token.Type = TokenType.Access;
            token.CreatedBy = TestConstants.UserId;
            token.OrganizationId = TestConstants.OrganizationId;
            token.ProjectId = TestConstants.ProjectId;
            return token;
        }
        
        public async Task<long> EventCountAsync() {
            await _client.RefreshAsync();
            return await _eventRepository.CountAsync();
        }

        public async Task CreateOrganizationAndProjectsAsync() {
            await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations());
            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects());
            await _client.RefreshAsync();
        }
    }
}
