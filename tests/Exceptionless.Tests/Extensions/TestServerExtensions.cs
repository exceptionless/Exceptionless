using System.Runtime.CompilerServices;
using System.Diagnostics;
using Foundatio.Extensions.Hosting.Startup;
using Microsoft.AspNetCore.TestHost;

namespace Exceptionless.Tests;

public static class TestServerExtensions
{
    private static readonly object s_waitedMarker = new();
    private static readonly ConditionalWeakTable<TestServer, object> s_waitedServers = new();

    public static async Task WaitForReadyAsync(this TestServer server)
    {
        var startupContext = server.Services.GetService<StartupActionsContext>();
        var maxWaitTime = !s_waitedServers.TryGetValue(server, out _) ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(2);
        if (Debugger.IsAttached)
            maxWaitTime = maxWaitTime.Add(TimeSpan.FromMinutes(1));

        var client = server.CreateClient();
        var startTime = DateTime.UtcNow;
        do
        {
            if (startupContext is not null && startupContext.IsStartupComplete && startupContext.Result.Success == false)
                throw new OperationCanceledException($"Startup action \"{startupContext.Result.FailedActionName}\" failed: {startupContext.Result.ErrorMessage}");

            var response = await client.GetAsync("/ready");
            if (response.IsSuccessStatusCode)
            {
                s_waitedServers.GetValue(server, _ => s_waitedMarker);
                break;
            }

            if (DateTime.UtcNow.Subtract(startTime) > maxWaitTime)
                throw new TimeoutException("Failed waiting for server to be ready.");

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        } while (true);
    }
}
