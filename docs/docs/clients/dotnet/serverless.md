---
title: "Serverless"
description: "Use Exceptionless in AWS Lambda and Azure Functions .NET apps, and send queued events before each invocation returns."
sources:
  - Exceptionless.Net:samples/Exceptionless.SampleLambda/Function.cs
  - Exceptionless.Net:samples/Exceptionless.SampleLambdaAspNetCore/appsettings.json
  - Exceptionless.Net:samples/Exceptionless.SampleLambdaAspNetCore/Startup.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessMiddleware.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessExtensions.cs
---

# Serverless

Exceptionless works in AWS Lambda and Azure Functions, with one rule: send the queue before your function returns. The
client queues events and sends them in the background, and a serverless platform can freeze or stop your process as soon
as the invocation ends, taking unsent events with it.

Await `client.ProcessQueueAsync()` at the end of every invocation, including when the function throws. It sends batches
until the queue is empty or a send fails, and waits for those requests. Events from a failed send stay in memory, and
are lost if the platform stops the process before a later invocation sends them.

## AWS Lambda

### Function handlers

Install the core package:

```bash
dotnet add package Exceptionless
```

Create the client once, outside the handler, and process the queue in a `finally` block:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleLambda/Function.cs -->

```csharp
using Amazon.Lambda.Core;
using Exceptionless;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace MyFunction;

public class Function {
    private static readonly ExceptionlessClient _exceptionless = new(c => {
        // Reads Exceptionless__ApiKey and Exceptionless__ServerUrl from the function's environment variables.
        c.ReadFromEnvironmentalVariables();
    });

    public async Task<string> FunctionHandler(string input, ILambdaContext context) {
        try {
            _exceptionless.SubmitFeatureUsage("Process input");
            return input.ToUpperInvariant();
        } catch (Exception ex) {
            ex.ToExceptionless(_exceptionless).Submit();
            throw;
        } finally {
            await _exceptionless.ProcessQueueAsync();
        }
    }
}
```

Set the `Exceptionless__ApiKey` environment variable on the function to your API key. Lambda catches exceptions your
handler throws, so they never reach an unhandled exception handler. Catch them, submit them, and rethrow.

### ASP.NET Core on Lambda

An ASP.NET Core app hosted on Lambda uses the [ASP.NET Core](/docs/clients/dotnet/aspnet-core/) setup. Add
`ProcessQueueOnCompletedRequest` so the client processes the queue after each response completes:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleLambdaAspNetCore/appsettings.json -->

```json
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY",
    "ProcessQueueOnCompletedRequest": true
  }
}
```

The queue is processed in the response's completion callback, the same way as `ProcessQueueAsync()`, so a failed send
leaves events in the queue.

## Azure Functions

In the isolated worker model, register Exceptionless on the Functions host with the
[worker services](/docs/clients/dotnet/worker-services/) package:

```bash
dotnet add package Exceptionless.Extensions.Hosting
```

```csharp
using Exceptionless;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddExceptionless();

builder.Build().Run();
```

Set the `Exceptionless__ApiKey` application setting to your API key (in `local.settings.json` when running locally).
Then inject `ExceptionlessClient` into your functions and process the queue before each one returns:

```csharp
using Exceptionless;
using Microsoft.Azure.Functions.Worker;

public class ProcessOrder {
    private readonly ExceptionlessClient _exceptionless;

    public ProcessOrder(ExceptionlessClient exceptionless) {
        _exceptionless = exceptionless;
    }

    [Function(nameof(ProcessOrder))]
    public async Task Run([QueueTrigger("orders")] string message) {
        try {
            // Your work goes here.
        } catch (Exception ex) {
            ex.ToExceptionless(_exceptionless).SetProperty("Message", message).Submit();
            throw;
        } finally {
            await _exceptionless.ProcessQueueAsync();
        }
    }
}
```

The Functions host catches exceptions your function throws, so catch, submit, and rethrow them as shown.

> [!NOTE]
> If you self-host Exceptionless, also set `Exceptionless__ServerUrl` to your instance's URL.

## Reuse the flush with a scope {#reuse-the-flush-with-a-scope}

To avoid repeating `try`/`finally`, wrap the flush in a small `IAsyncDisposable` and declare it with `await using` at
the top of each handler:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleLambda/Function.cs -->

```csharp
using Exceptionless;

public sealed class ProcessQueueScope : IAsyncDisposable {
    private readonly ExceptionlessClient _client;

    public ProcessQueueScope(ExceptionlessClient client) {
        _client = client;
    }

    public async ValueTask DisposeAsync() {
        await _client.ProcessQueueAsync();
    }
}
```

```csharp
await using var _ = new ProcessQueueScope(_exceptionless);
```

## Verify

Throw a test error from a handler:

```csharp
throw new InvalidOperationException("Exceptionless test error");
```

Invoke the function, then open **Stacks** in the app. The error appears within a few seconds. If it doesn't, check that
the queue is processed in a `finally` block, and see [Troubleshooting](/docs/clients/dotnet/troubleshooting/).

## What is captured automatically

- **Exceptions you submit**, with the exception type, message, and stack trace.
- **Environment details**: the host's operating system, .NET version, process, and memory.
- **Request details**, for ASP.NET Core apps on Lambda.

Because the platform catches exceptions from your function, nothing is reported unless you submit it.

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): logs, feature usage, and custom data.
- [Configuration](/docs/clients/dotnet/configuration/#environment-variables): the environment variables the client
  reads.

## Ask Exie

```prompt exie
Which errors did my functions throw in the last hour? Group them by error type.
```

```prompt exie
Is the error rate for this project higher today than yesterday? Show it by hour.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the newest error stacks in my Exceptionless project from the last day, match them to the function handlers in this repository, and fix the most frequent one.
```
