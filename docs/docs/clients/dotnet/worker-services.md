---
title: "Worker services and the generic host"
navTitle: "Worker services"
description: "Add Exceptionless to .NET worker services and generic host apps on .NET 8 or later, with dependency injection and a queue flush on shutdown."
sources:
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessLifetimeService.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/readme.txt
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Logging/ExceptionlessLogger.cs
  - Exceptionless.Net:samples/Exceptionless.SampleHosting/Program.cs
  - Exceptionless.Net:samples/Exceptionless.SampleHosting/SampleService.cs
---

# Worker services and the generic host

The `Exceptionless.Extensions.Hosting` package adds Exceptionless to apps built on the .NET generic host, such as worker
services, queue processors, and Windows services. It registers `ExceptionlessClient` for dependency injection, reports
unhandled exceptions, and sends queued events before the host stops. It requires .NET 8 or later.

For web apps, use the [ASP.NET Core](/docs/clients/dotnet/aspnet-core/) package instead. It includes everything in this
package.

## Install

```bash
dotnet add package Exceptionless.Extensions.Hosting
dotnet add package Exceptionless.Extensions.Logging
```

`Exceptionless.Extensions.Logging` is optional. Add it to also send `ILogger` entries to Exceptionless.

## Configure

Register Exceptionless on the host builder in `Program.cs`:

```csharp
using Exceptionless;

var builder = Host.CreateApplicationBuilder(args);

// Optional: send ILogger entries to Exceptionless.
builder.Logging.AddExceptionless();

// Registers the client, reads the "Exceptionless" configuration section,
// and flushes the queue when the host stops.
builder.AddExceptionless();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

Add your API key to `appsettings.json`, or set the `Exceptionless__ApiKey` environment variable:

```json
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY"
  }
}
```

`builder.AddExceptionless()` also accepts an API key or a configuration lambda, for example
`builder.AddExceptionless(c => c.DefaultTags.Add("billing-worker"))`. See the
[configuration reference](/docs/clients/dotnet/configuration/) for every setting.

> [!NOTE]
> If you self-host Exceptionless, add `"ServerUrl": "https://exceptionless.example.com"` to the `Exceptionless` section.

### Older host builder

Apps that use `Host.CreateDefaultBuilder` and `IHostBuilder` register the client in `ConfigureServices` and call
`UseExceptionless()` on the host builder to add the shutdown flush:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleHosting/Program.cs -->

```csharp
using Exceptionless;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging => logging.AddExceptionless())
    .UseExceptionless()
    .ConfigureServices(services => {
        services.AddExceptionless();
        services.AddHostedService<Worker>();
    })
    .Build()
    .RunAsync();
```

`services.AddExceptionless()` on its own only registers the client. Without `UseExceptionless()` nothing reports
unhandled exceptions or flushes the queue when the host stops.

## Verify

Submit a test error from your worker:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleHosting/SampleService.cs -->

```csharp
using Exceptionless;

public class Worker : BackgroundService {
    private readonly ExceptionlessClient _exceptionless;
    private readonly ILogger<Worker> _logger;

    public Worker(ExceptionlessClient exceptionless, ILogger<Worker> logger) {
        _exceptionless = exceptionless;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            throw new InvalidOperationException("Exceptionless test error");
        } catch (Exception ex) {
            ex.ToExceptionless(_exceptionless).Submit();
        }

        _logger.LogWarning("Worker started");

        while (!stoppingToken.IsCancellationRequested) {
            // Your work goes here.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

Run the worker, then open **Stacks** in the app. The error appears within a few seconds. If you added the logging
provider, the warning shows up as a log event.

## What is captured automatically

- **Unhandled exceptions** from `AppDomain.UnhandledException` and unobserved task exceptions, registered when the host
  starts.
- **Queued events are sent** when the host stops. The client processes the queue while the host is stopping, then shuts
  down.
- **Environment details**: machine name, operating system, .NET version, process, memory, and IP addresses.
- **`ILogger` entries**, if you added `builder.Logging.AddExceptionless()`.

Exceptions that your own code catches are not unhandled. Submit them with `ex.ToExceptionless(client).Submit()` or log
them with `ILogger`. An exception that escapes a `BackgroundService` doesn't reach these handlers either: the host logs
it and stops. With the logging provider added, that log entry is reported as an error event.

The hosting package doesn't call `Startup()` on the client, so it doesn't poll for
[client configuration values](/docs/clients/dotnet/client-configuration-values/) while the worker is idle. The client
still picks up new values whenever it sends events. To poll while idle, set the interval when you register the client:

```csharp
builder.AddExceptionless(c => c.UpdateSettingsWhenIdleInterval = TimeSpan.FromMinutes(2));
```

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): logs, feature usage, custom data, and user identity.
- [Logging integrations](/docs/clients/dotnet/logging/): control which `ILogger` entries are sent.
- [Configuration](/docs/clients/dotnet/configuration/): queue storage for workers that may stop abruptly.

## Ask Exie

```prompt exie
Show me the open stacks tagged billing-worker, newest first.
```

```prompt exie
How many errors did this project have each hour over the last day? Is anything spiking?
```

```prompt exie
Snooze the stack for this timeout error for 2 hours while I look into it.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Look at the open Exceptionless stacks for this worker service, find the one with the most occurrences this week, trace it to the code in this repository, and fix it.
```
