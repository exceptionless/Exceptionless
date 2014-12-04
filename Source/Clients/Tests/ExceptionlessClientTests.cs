using System;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Dependency;
using Exceptionless.Models;
using Exceptionless.Storage;
using Microsoft.Owin.Hosting;
using SimpleInjector;
using Xunit;

namespace Client.Tests {
    public class ExceptionlessClientTests {
        private ExceptionlessClient CreateClient() {
            return new ExceptionlessClient(c => {
                c.ApiKey = DataHelper.SAMPLE_API_KEY;
                c.ServerUrl = Settings.Current.BaseURL;
                c.EnableSSL = false;
                c.UseDebugLogger();
                c.UserAgent = "testclient/1.0.0.0";
            });
        }

        [Fact]
        public async Task CanSubmitSimpleEvent() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container, false))) {
                var queue = container.GetInstance<IQueue<EventPost>>();
                Assert.NotNull(queue);
                Assert.Equal(0, queue.GetQueueCount());
                
                var statsCounter = container.GetInstance<IAppStatsClient>() as InMemoryAppStatsClient;
                Assert.NotNull(statsCounter);
                
                EnsureSampleData(container);

                var client = CreateClient();
                client.SubmitEvent(new Event { Message = "Test" });

                var storage = client.Configuration.Resolver.GetFileStorage() as InMemoryFileStorage;
                Assert.NotNull(storage);
                Assert.Equal(1, storage.GetFileList().Count());

                Assert.True(statsCounter.WaitForCounter(StatNames.EventsProcessed, work: client.ProcessQueue));

                Assert.Equal(0, queue.GetQueueCount());
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsSubmitted));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsQueued));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsParsed));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsDequeued));
                Assert.Equal(1, statsCounter.GetCount(StatNames.EventsProcessed));
            }
        }

        [Fact]
        public async Task CanSubmitSimpleException() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container, false))) {
                var queue = container.GetInstance<IQueue<EventPost>>();
                Assert.NotNull(queue);
                Assert.Equal(0, queue.GetQueueCount());

                var statsCounter = container.GetInstance<IAppStatsClient>() as InMemoryAppStatsClient;
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

                var storage = client.Configuration.Resolver.GetFileStorage() as InMemoryFileStorage; 
                Assert.NotNull(storage);
                Assert.Equal(1, storage.GetFileList().Count());
                
                Assert.True(statsCounter.WaitForCounter(StatNames.EventsProcessed, work: client.ProcessQueue));

                Assert.Equal(0, queue.GetQueueCount());
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsSubmitted));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsQueued));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsParsed));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsDequeued));
                Assert.Equal(1, statsCounter.GetCount(StatNames.EventsProcessed));
            }
        }

        private void EnsureSampleData(Container container) {
            var dataHelper = container.GetInstance<DataHelper>();
            var userRepository = container.GetInstance<IUserRepository>();
            var user = userRepository.GetByEmailAddress("test@test.com");
            if (user == null)
                user = userRepository.Add(new User { FullName = "Test User", EmailAddress = "test@test.com", VerifyEmailAddressToken = Guid.NewGuid().ToString(), VerifyEmailAddressTokenExpiration = DateTime.MaxValue});
            dataHelper.CreateSampleOrganizationAndProject(user.Id);
        }
    }
}
