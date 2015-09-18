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
            ResetDatabase();
            AddSamples();
        }

        [Fact]
        public void CanPostString() {
            _eventQueue.DeleteQueue();
            RemoveAllEvents();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), false, false);

                var metricsClient = IoC.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
                Assert.NotNull(metricsClient);
                
                Assert.True(metricsClient.WaitForCounter(MetricNames.PostsQueued, work: async () => {
                    var actionResult = await _eventController.PostAsync(Encoding.UTF8.GetBytes("simple string")).AnyContext();
                    Assert.IsType<StatusCodeResult>(actionResult);
                }));

                Assert.Equal(1, _eventQueue.GetQueueCount());

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                processEventsJob.Run();

                Assert.Equal(0, _eventQueue.GetQueueCount());
                Assert.Equal(1, EventCount());
            } finally {
                RemoveAllEvents();
            }
        }

        [Fact]
        public async Task CanPostCompressedString() {
            _eventQueue.DeleteQueue();
            RemoveAllEvents();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync().AnyContext()).AnyContext();
                Assert.IsType<StatusCodeResult>(actionResult);
                Assert.Equal(1, _eventQueue.GetQueueCount());

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                processEventsJob.Run();

                Assert.Equal(0, _eventQueue.GetQueueCount());
                Assert.Equal(1, EventCount());
            } finally {
                RemoveAllEvents();
            }
        }

        [Fact]
        public async Task CanPostSingleEvent() {
            _eventQueue.DeleteQueue();
            RemoveAllEvents();
            
            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                var actionResult = await _eventController.PostAsync(await Encoding.UTF8.GetBytes("simple string").CompressAsync().AnyContext()).AnyContext();
                Assert.IsType<StatusCodeResult>(actionResult);
                Assert.Equal(1, _eventQueue.GetQueueCount());

                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                processEventsJob.Run();

                Assert.Equal(0, _eventQueue.GetQueueCount());
                Assert.Equal(1, EventCount());
            } finally {
                RemoveAllEvents();
            }
        }

        [Fact]
        public void CanPostManyEvents() {
            _eventQueue.DeleteQueue();
            RemoveAllEvents();

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

                Parallel.For(0, batchCount, i => {
                    _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                    var events = new RandomEventGenerator().Generate(batchSize);
                    var compressedEvents = await Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events)).CompressAsync().AnyContext();
                    var actionResult = await _eventController.PostAsync(compressedEvents, version: 2, userAgent: "exceptionless/2.0.0.0").AnyContext();
                    Assert.IsType<StatusCodeResult>(actionResult);
                });

                Assert.Equal(batchCount, _eventQueue.GetQueueCount());

                var sw = new Stopwatch();
                var processEventsJob = IoC.GetInstance<EventPostsJob>();
                sw.Start();
                processEventsJob.RunUntilEmpty();
                sw.Stop();
                Trace.WriteLine(sw.Elapsed);

                Assert.Equal(0, _eventQueue.GetQueueCount());
                Assert.Equal(batchSize * batchCount, EventCount());

                bool success = countdown.Wait(5000);
                Assert.True(success);
            } finally {
                _eventQueue.DeleteQueue();
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

        private void ResetDatabase(bool force = false) {
            if (_databaseReset && !force)
                return;
            
            RemoveAllEvents();
            RemoveAllProjects();
            RemoveAllOrganizations();

            _databaseReset = true;
        }

        public void RemoveAllOrganizations() {
            _organizationRepository.RemoveAll();
            _client.Refresh(r => r.Force());
            _sampleOrganizationsAdded = false;
        }

        public void RemoveAllProjects() {
            _projectRepository.RemoveAll();
            _client.Refresh(r => r.Force());
            _sampleProjectsAdded = false;
        }

        public void RemoveAllEvents() {
            _eventRepository.RemoveAll();
            _client.Refresh(r => r.Force());
        }

        public long EventCount() {
            _client.Refresh(r => r.Force());
            return _eventRepository.Count();
        }
        
        public void AddSampleProjects() {
            if (_sampleProjectsAdded)
                return;

            _projectRepository.Add(ProjectData.GenerateSampleProjects());
            _client.Refresh(r => r.Force());
            _sampleProjectsAdded = true;
        }
        
        public void AddSampleOrganizations() {
            if (_sampleOrganizationsAdded)
                return;

            _organizationRepository.Add(OrganizationData.GenerateSampleOrganizations());
            _client.Refresh(r => r.Force());
            _sampleOrganizationsAdded = true;
        }

        public void AddSamples() {
            AddSampleProjects();
            AddSampleOrganizations();
        }
    }
}