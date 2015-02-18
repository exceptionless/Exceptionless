using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Dependency;
using Exceptionless.Models;
using Exceptionless.Storage;
using Foundatio.Metrics;
using Foundatio.Queues;
using Microsoft.Owin.Hosting;
using SimpleInjector;
using Xunit;

namespace Client.Tests {
    public class ExceptionlessClientTests {
        private ExceptionlessClient CreateClient() {
            return new ExceptionlessClient(c => {
                c.ApiKey = DataHelper.TEST_API_KEY;
                c.ServerUrl = Settings.Current.BaseURL;
                c.EnableSSL = false;
                c.UseDebugLogger();
                c.UserAgent = "testclient/1.0.0.0";
            });
        }

        [Fact]
        public async Task CanSubmitSimpleEvent() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container))) {
                var queue = container.GetInstance<IQueue<EventPost>>();
                Assert.NotNull(queue);
                Assert.Equal(0, queue.GetQueueCount());
                
                var statsCounter = container.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
                Assert.NotNull(statsCounter);
                
                EnsureSampleData(container);

                var client = CreateClient();
                client.SubmitEvent(new Event { Message = "Test" });

                var storage = client.Configuration.Resolver.GetFileStorage() as InMemoryObjectStorage;
                Assert.NotNull(storage);
                Assert.Equal(1, storage.GetObjectList().Count());

                Assert.True(statsCounter.WaitForCounter(MetricNames.EventsProcessed, work: client.ProcessQueue));

                Assert.Equal(0, queue.GetQueueCount());
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsSubmitted));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsQueued));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsParsed));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsDequeued));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.EventsProcessed));
            }
        }

        [Fact]
        public async Task CanSubmitSimpleException() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container))) {
                var queue = container.GetInstance<IQueue<EventPost>>();
                Assert.NotNull(queue);
                Assert.Equal(0, queue.GetQueueCount());

                var statsCounter = container.GetInstance<IMetricsClient>() as InMemoryMetricsClient;
                Assert.NotNull(statsCounter);

                EnsureSampleData(container);

                var client = CreateClient();
                var clientQueue = client.Configuration.Resolver.GetEventQueue();
                // make sure the queue isn't processed until we are ready for it
                clientQueue.SuspendProcessing(TimeSpan.FromSeconds(10));
                try {
                    throw new Exception("Simple Exception");
                } catch (Exception ex) {
                    client.SubmitException(ex);
                }

                var storage = client.Configuration.Resolver.GetFileStorage() as InMemoryObjectStorage; 
                Assert.NotNull(storage);
                Assert.Equal(1, storage.GetObjectList().Count());
                
                Assert.True(statsCounter.WaitForCounter(MetricNames.EventsProcessed, work: client.ProcessQueue));

                Assert.Equal(0, queue.GetQueueCount());
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsSubmitted));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsQueued));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsParsed));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.PostsDequeued));
                Assert.Equal(1, statsCounter.GetCount(MetricNames.EventsProcessed));
            }
        }

        private void EnsureSampleData(Container container) {
            var dataHelper = container.GetInstance<DataHelper>();
            var userRepository = container.GetInstance<IUserRepository>();
            var user = userRepository.GetByEmailAddress("test@exceptionless.io");
            if (user == null)
                user = userRepository.Add(new User { FullName = "Test User", EmailAddress = "test@exceptionless.io", VerifyEmailAddressToken = Guid.NewGuid().ToString(), VerifyEmailAddressTokenExpiration = DateTime.MaxValue });

            dataHelper.CreateTestOrganizationAndProject(user.Id);
        }
    }
}
