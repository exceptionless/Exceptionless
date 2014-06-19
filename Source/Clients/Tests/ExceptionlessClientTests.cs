using System;
using System.Linq;
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
        public void CanSubmitSimpleEvent() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container))) {
                var queue = container.GetInstance<IQueue<EventPost>>() as InMemoryQueue<EventPost>;
                var statsCounter = container.GetInstance<IAppStatsClient>() as InMemoryAppStatsClient;
                EnsureSampleData(container);

                Assert.NotNull(statsCounter);
                Assert.NotNull(queue);
                Assert.Equal(0, queue.Count);

                var client = CreateClient();
                client.SubmitEvent(new Event { Message = "Test" });

                var storage = client.Configuration.Resolver.GetFileStorage() as InMemoryFileStorage;
                Assert.NotNull(storage);
                Assert.Equal(1, storage.GetFileList().Count());

                client.ProcessQueue();
                statsCounter.WaitForCounter(StatNames.EventsProcessed);

                Assert.Equal(0, queue.Count);
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsSubmitted));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsQueued));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsParsed));
                Assert.Equal(1, statsCounter.GetCount(StatNames.PostsDequeued));
                Assert.Equal(1, statsCounter.GetCount(StatNames.EventsProcessed));
            }
        }

        [Fact]
        public void CanSubmitSimpleException() {
            var container = AppBuilder.CreateContainer();
            using (WebApp.Start(Settings.Current.BaseURL, app => AppBuilder.BuildWithContainer(app, container))) {
                var queue = container.GetInstance<IQueue<EventPost>>() as InMemoryQueue<EventPost>;
                var statsCounter = container.GetInstance<IAppStatsClient>() as InMemoryAppStatsClient;
                EnsureSampleData(container);

                Assert.NotNull(statsCounter);
                Assert.NotNull(queue);
                Assert.Equal(0, queue.Count);

                var client = CreateClient();
                try {
                    throw new Exception("Simple Exception");
                } catch (Exception ex) {
                    client.SubmitException(ex);
                }

                var storage = client.Configuration.Resolver.GetFileStorage() as InMemoryFileStorage; 
                Assert.NotNull(storage);
                Assert.Equal(1, storage.GetFileList().Count());
                
                client.ProcessQueue();
                statsCounter.WaitForCounter(StatNames.EventsProcessed);

                Assert.Equal(0, queue.Count);
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
                user = userRepository.Add(new User { EmailAddress = "test@test.com" });
            dataHelper.CreateSampleOrganizationAndProject(user.Id);
        }
    }
}
