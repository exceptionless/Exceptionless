using System;
using Exceptionless;
using Exceptionless.Logging;
using Exceptionless.Models;
using Xunit;

namespace Pcl.Tests {
    public class ExceptionlessClientTests {
        [Fact]
        public void CanSubmitSimpleEvent() {
            ExceptionlessConfiguration.ConfigureDefaults.Add(c => c.Resolver.Register<IExceptionlessLog, TraceExceptionlessLog>());
            var client = new ExceptionlessClient("e3d51ea621464280bbcb79c11fd6483e");
            client.SubmittingEvent += (sender, args) => args.Cancel = true;
            client.SubmitEvent(new Event { Message = "Test" });
            client.ProcessQueue();
        }
    }
}
