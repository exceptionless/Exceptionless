using System.Diagnostics;
using Foundatio.Extensions.Hosting.Startup;
using Microsoft.AspNetCore.TestHost;

namespace Exceptionless.Tests;

public static class TestServerExtensions
{
    public static async Task WaitForReadyAsync(this TestServer server)
    {
        var startupContext = server.Services.GetService<StartupActionsContext>();
        var maxWaitTime = TimeSpan.FromSeconds(30);
        if (Debugger.IsAttached)
            maxWaitTime = maxWaitTime.Add(TimeSpan.FromMinutes(1));

        using var client = server.CreateClient();
        var startTime = DateTime.UtcNow;
        do
        {
            // Foundatio publishes IsStartupComplete before replacing the default result.
            // Only fail early for a populated failure; /ready remains the success condition.
            var result = startupContext?.Result;
            if (startupContext?.IsStartupComplete == true && result?.Success == false
                && (result.FailedActionName is not null || result.ErrorMessage is not null))
                throw new OperationCanceledException($"Startup action \"{result.FailedActionName}\" failed: {result.ErrorMessage}");

            using var response = await client.GetAsync("/ready");
            if (response.IsSuccessStatusCode)
                break;

            if (DateTime.UtcNow.Subtract(startTime) > maxWaitTime)
                throw new TimeoutException("Failed waiting for server to be ready.");

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        } while (true);
    }
}
