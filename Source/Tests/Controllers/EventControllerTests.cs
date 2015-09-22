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
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Authorization;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Microsoft.Owin;
using Nest;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Api.Tests.Controllers {
    public class EventControllerTests {
        private static bool _databaseReset;
        private static bool _sampleOrganizationsAdded;
        private static bool _sampleProjectsAdded;

        private readonly IElasticClient _client = IoC.GetInstance<IElasticClient>();
        private readonly EventController _eventController = IoC.GetInstance<EventController>();
        private readonly IEventRepository _eventRepository = IoC.GetInstance<IEventRepository>();
        private readonly IQueue<EventPost> _eventQueue = IoC.GetInstance<IQueue<EventPost>>();
        private readonly IOrganizationRepository _organizationRepository = IoC.GetInstance<IOrganizationRepository>();
        private readonly IProjectRepository _projectRepository = IoC.GetInstance<IProjectRepository>();

        public EventControllerTests() {
            ResetDatabaseAsync().AnyContext().GetAwaiter().GetResult();
            AddSamplesAsync().AnyContext().GetAwaiter().GetResult();
        }

        [Fact]
        public async Task CanPostString() {
            await _eventQueue.DeleteQueueAsync().AnyContext();
            await RemoveAllEventsAsync().AnyContext();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), false, false);

                var metricsClient = IoC.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
                Assert.NotNull(metricsClient);
                
                Assert.True(await metricsClient.WaitForCounterAsync(MetricNames.PostsQueued, work: async () => {
                    var actionResult = await _eventController.PostAsync(Encoding.UTF8.GetBytes("simple string")).AnyContext();
                    Assert.IsType<StatusCodeResult>(actionResult);
                }).AnyContext());

                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                await processEventsJob.RunAsync().AnyContext();

                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);
                Assert.Equal(1, await EventCountAsync().AnyContext());
            } finally {
                await RemoveAllEventsAsync().AnyContext();
            }
        }

        [Fact]
        public async Task CanPostCompressedString() {
            await _eventQueue.DeleteQueueAsync().AnyContext();
            await RemoveAllEventsAsync().AnyContext();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync().AnyContext()).AnyContext();
                Assert.IsType<StatusCodeResult>(actionResult);
                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                await processEventsJob.RunAsync().AnyContext();

                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);
                Assert.Equal(1, await EventCountAsync().AnyContext());
            } finally {
                await RemoveAllEventsAsync().AnyContext();
            }
        }

        [Fact]
        public async Task CanPostSingleEvent() {
            await _eventQueue.DeleteQueueAsync().AnyContext();
            await RemoveAllEventsAsync().AnyContext();
            
            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync().AnyContext()).AnyContext();
                Assert.IsType<StatusCodeResult>(actionResult);
                Assert.Equal(1, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                await processEventsJob.RunAsync().AnyContext();

                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);
                Assert.Equal(1, await EventCountAsync().AnyContext());
            } finally {
                await RemoveAllEventsAsync().AnyContext();
            }
        }

        [Fact]
        public async Task CanPostManyEvents() {
            await _eventQueue.DeleteQueueAsync().AnyContext();
            await RemoveAllEventsAsync().AnyContext();

            const int batchSize = 250;
            const int batchCount = 10;

            try {
                var countdown = new CountDownLatch(10);
                var messageSubscriber = IoC.GetInstance<IMessageSubscriber>();
                messageSubscriber.Subscribe<EntityChanged>(ch => {
                    if (ch.ChangeType != ChangeType.Added || ch.Type != typeof(PersistentEvent).Name)
                        return;

                    if (countdown.Remaining <= 0)
                        throw new ApplicationException("Too many change notifications.");

                    countdown.Signal();
                });

                await Run.InParallel(batchCount, async i => {
                    _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                    var events = new RandomEventGenerator().Generate(batchSize);
                    var compressedEvents = await Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events)).CompressAsync().AnyContext();
                    var actionResult = await _eventController.PostAsync(compressedEvents, version: 2, userAgent: "exceptionless/2.0.0.0").AnyContext();
                    Assert.IsType<StatusCodeResult>(actionResult);
                }).AnyContext();

                Assert.Equal(batchCount, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);
                
                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                var sw = Stopwatch.StartNew();
                await processEventsJob.RunUntilEmptyAsync().AnyContext();
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);

                Assert.Equal(0, (await _eventQueue.GetQueueStatsAsync().AnyContext()).Enqueued);
                Assert.Equal(batchSize * batchCount, await EventCountAsync().AnyContext());

                bool success = countdown.Wait(5000);
                Assert.True(success);
            } finally {
                await _eventQueue.DeleteQueueAsync().AnyContext();
            }
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

        private async Task ResetDatabaseAsync(bool force = false) {
            if (_databaseReset && !force)
                return;
            
            await RemoveAllEventsAsync().AnyContext();
            await RemoveAllProjectsAsync().AnyContext();
            await RemoveAllOrganizationsAsync().AnyContext();

            _databaseReset = true;
        }

        public async Task RemoveAllOrganizationsAsync() {
            await _organizationRepository.RemoveAllAsync().AnyContext();
            _client.Refresh(r => r.Force());
            _sampleOrganizationsAdded = false;
        }

        public async Task RemoveAllProjectsAsync() {
            await _projectRepository.RemoveAllAsync().AnyContext();
            _client.Refresh(r => r.Force());
            _sampleProjectsAdded = false;
        }

        public async Task RemoveAllEventsAsync() {
            await _eventRepository.RemoveAllAsync().AnyContext();
            _client.Refresh(r => r.Force());
        }

        public Task<long> EventCountAsync() {
            _client.Refresh(r => r.Force());
            return _eventRepository.CountAsync();
        }
        
        public async Task AddSampleProjectsAsync() {
            if (_sampleProjectsAdded)
                return;

            await _projectRepository.AddAsync(ProjectData.GenerateSampleProjects()).AnyContext();
            _client.Refresh(r => r.Force());
            _sampleProjectsAdded = true;
        }
        
        public async Task AddSampleOrganizationsAsync() {
            if (_sampleOrganizationsAdded)
                return;

            await _organizationRepository.AddAsync(OrganizationData.GenerateSampleOrganizations()).AnyContext();
            _client.Refresh(r => r.Force());
            _sampleOrganizationsAdded = true;
        }

        public async Task AddSamplesAsync() {
            await AddSampleProjectsAsync().AnyContext();
            await AddSampleOrganizationsAsync().AnyContext();
        }
    }
}