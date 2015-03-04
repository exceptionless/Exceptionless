using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using Exceptionless.Helpers;
using Exceptionless.Tests.Utility;
using Foundatio.Messaging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Microsoft.Owin;
using Newtonsoft.Json;
using Xunit;

namespace Exceptionless.Api.Tests.Controllers {
    public class EventControllerTests : MongoTestHelper {
        private readonly EventController _eventController = IoC.GetInstance<EventController>();
        private readonly IQueue<EventPost> _eventQueue = IoC.GetInstance<IQueue<EventPost>>();

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

                var statsCounter = IoC.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
                Assert.NotNull(statsCounter);
                
                Assert.True(statsCounter.WaitForCounter(MetricNames.PostsQueued, work: () => {
                    var actionResult = _eventController.Post(Encoding.UTF8.GetBytes("simple string"));
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
        public void CanPostCompressedString() {
            _eventQueue.DeleteQueue();
            RemoveAllEvents();

            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                var actionResult = _eventController.Post(Encoding.UTF8.GetBytes("simple string").Compress());
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
        public void CanPostSingleEvent() {
            _eventQueue.DeleteQueue();
            RemoveAllEvents();
            
            try {
                _eventController.Request = CreateRequestMessage(new ClaimsPrincipal(IdentityUtils.CreateUserIdentity(TestConstants.UserEmail, TestConstants.UserId, new[] { TestConstants.OrganizationId }, new[] { AuthorizationRoles.Client }, TestConstants.ProjectId)), true, false);
                var actionResult = _eventController.Post(Encoding.UTF8.GetBytes("simple string").Compress());
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
                    var compressedEvents = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(events)).Compress();
                    var actionResult = _eventController.Post(compressedEvents, version: 2, userAgent: "exceptionless/2.0.0.0");
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

        #region Helpers

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

        private static IEnumerable<object[]> Events {
            get {
                var result = new List<object[]>();
                foreach (var file in Directory.GetFiles(@"..\..\EventData\", "*.txt", SearchOption.AllDirectories).Where(f => !f.EndsWith(".expected.json")))
                    result.Add(new object[] { file });

                return result.ToArray();
            }
        }

        #endregion
    }
}