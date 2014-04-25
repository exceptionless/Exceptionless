using System;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Queues;
using Exceptionless.Models;
using Microsoft.Owin.Hosting;
using Xunit;

namespace Pcl.Tests {
    public class ExceptionlessClientTests {
        public ExceptionlessClientTests() {
            ExceptionlessConfiguration.ConfigureDefaults.Add(c => {
                c.ApiKey = "e3d51ea621464280bbcb79c11fd6483e";
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
                Assert.NotNull(queue);
                Assert.Equal(0, queue.Count);
                
                var client = new ExceptionlessClient();
                client.SubmitEvent(new Event { Message = "Test" });
                client.ProcessQueue();

                Assert.Equal(1, queue.Count);

                var processEventsJob = container.GetInstance<ProcessEventPostsJob>();
                var result = processEventsJob.Run();
                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal(0, queue.Count);
            }
        }
    }
}
