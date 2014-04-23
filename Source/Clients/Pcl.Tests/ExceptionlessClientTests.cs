using System;
using Exceptionless;
using Exceptionless.Api;
using Exceptionless.Core;
using Exceptionless.Models;
using Microsoft.Owin.Hosting;
using Xunit;

namespace Pcl.Tests {
    public class ExceptionlessClientTests {
        public ExceptionlessClientTests() {
            ExceptionlessConfiguration.ConfigureDefaults.Clear();
            ExceptionlessConfiguration.ConfigureDefaults.Add(c => c.UseDebugLogger());
        }

        [Fact]
        public void CanSubmitSimpleEvent() {
            using (WebApp.Start(Settings.Current.BaseURL, AppBuilder.Build)) {
                var client = new ExceptionlessClient("e3d51ea621464280bbcb79c11fd6483e");
                client.SubmitEvent(new Event { Message = "Test" });
                client.ProcessQueue();
            }
        }

        [Fact]
        public void CanConfigureClientUsingActionMethod() {
            var client = new ExceptionlessClient(c => {
                c.ApiKey = "e3d51ea621464280bbcb79c11fd6483e";
                c.UseDebugLogger();
            });
            client.SubmitEvent(new Event { Message = "Test" });
            client.ProcessQueue();
        }
    }
}
