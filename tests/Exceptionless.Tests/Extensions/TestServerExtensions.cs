using System;
using System.Threading.Tasks;
using Foundatio.Hosting.Startup;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Exceptionless.Tests {
    public static class TestServerExtensions {
        public static async Task WaitForReadyAsync(this TestServer server, TimeSpan? maxWaitTime = null) {
            var startupContext = server.Services.GetService<StartupActionsContext>();
            maxWaitTime = maxWaitTime ?? TimeSpan.FromSeconds(5);
            
            var client = server.CreateClient();
            var startTime = DateTime.Now;
            do {
                if (startupContext != null && startupContext.IsStartupComplete && startupContext.Result.Success == false)
                    throw new OperationCanceledException($"Startup action \"{startupContext.Result.FailedActionName}\" failed");

                var response = await client.GetAsync("/ready");
                if (response.IsSuccessStatusCode)
                    break;

                if (DateTime.Now.Subtract(startTime) > maxWaitTime)
                    throw new TimeoutException("Failed waiting for server to be ready.");

                await Task.Delay(TimeSpan.FromMilliseconds(100));
            } while (true);
        }
    }
}
