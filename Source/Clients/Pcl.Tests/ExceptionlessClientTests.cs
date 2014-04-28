using System;
using System.Threading.Tasks;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using Microsoft.Owin.Hosting;
using SimpleInjector;
using Xunit;

namespace Pcl.Tests {
    public class ExceptionlessClientTests {
        public ExceptionlessClientTests() {
            ExceptionlessConfiguration.ConfigureDefaults.Add(c => {
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

                Assert.NotNull(queue);
                Assert.Equal(0, queue.Count);
                
                var client = new ExceptionlessClient();
                client.SubmitEvent(new Event { Message = "Test" });
                client.ProcessQueue();

                var processEventsJob = container.GetInstance<ProcessEventPostsJob>();
                Task.Factory.StartNew(() => processEventsJob.Run());
                Task.Delay(TimeSpan.FromSeconds(2)).Wait();
                processEventsJob.Cancel();
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
            var user = userRepository.FirstOrDefault(u => u.EmailAddress == "test@test.com");
            if (user == null)
                user = userRepository.Add(new User { EmailAddress = "test@test.com" });
            dataHelper.CreateSampleOrganizationAndProject(user.Id);
        }
    }
}
