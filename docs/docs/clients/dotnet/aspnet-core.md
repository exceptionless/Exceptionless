---
title: "ASP.NET Core"
description: "Add Exceptionless to an ASP.NET Core app or API on .NET 8 or later to capture unhandled exceptions, 404s, request details, and ILogger entries."
redirectFrom:
  - /docs/clients/dotnet/guides/web-server-example/
sources:
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExceptionHandler.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessMiddleware.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessDiagnosticListener.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessAspNetCorePlugin.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/RequestInfoCollector.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessLifetimeService.cs
  - Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Program.cs
  - Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Controllers/ValuesController.cs
  - Exceptionless.Net:.agents/skills/exceptionless-dotnet/SKILL.md
---

# ASP.NET Core

The `Exceptionless.AspNetCore` package reports unhandled exceptions, 404 responses, and the details of the request that
caused them, from controllers, minimal APIs, Razor Pages, and Blazor Server apps. It requires .NET 8 or later. For .NET
Framework apps, see [Legacy ASP.NET](/docs/clients/dotnet/legacy-aspnet/).

## Install with your coding agent

Paste this into Claude Code, Codex, Copilot, or another coding agent from the root of your app:

```prompt agent
Add Exceptionless error reporting to this ASP.NET Core app. Read https://exceptionless.com/docs/clients/dotnet/aspnet-core.md and follow it exactly: install Exceptionless.AspNetCore and Exceptionless.Extensions.Logging, register Exceptionless in Program.cs with builder.AddExceptionless(), AddProblemDetails(), UseExceptionHandler(), and UseExceptionless(), and read the API key from the Exceptionless:ApiKey configuration value instead of hard-coding it. If the exceptionless-dotnet agent skill from the Exceptionless.Net repository (.agents/skills/exceptionless-dotnet) is installed, use it. Then add a temporary endpoint that throws, and tell me how to call it so I can confirm the error shows up.
```

The Exceptionless.Net repository ships an agent skill,
[`exceptionless-dotnet`](https://github.com/exceptionless/Exceptionless.Net/tree/main/.agents/skills/exceptionless-dotnet),
that teaches coding agents how to set up and review the .NET client. See [Coding agents](/docs/ai/coding-agents/).

## Install

```bash
dotnet add package Exceptionless.AspNetCore
dotnet add package Exceptionless.Extensions.Logging
```

`Exceptionless.Extensions.Logging` is optional. Add it to also send `ILogger` entries to Exceptionless (see
[Logging integrations](/docs/clients/dotnet/logging/)).

## Configure

Register Exceptionless in `Program.cs`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Program.cs -->

```csharp
using Exceptionless;

var builder = WebApplication.CreateBuilder(args);

// Optional: send ILogger entries to Exceptionless.
builder.Logging.AddExceptionless();

// Registers the client, reads the "Exceptionless" configuration section,
// flushes the queue on shutdown, and registers the Exceptionless IExceptionHandler.
builder.AddExceptionless();

// Required for the exception handler pipeline to produce an error response.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

app.UseExceptionHandler();

// Starts the client, records 404s, and adds request details to events.
app.UseExceptionless();

app.MapControllers();

app.Run();
```

Then add your API key to `appsettings.json`. Find the key in the app under your project's **API Keys** settings.

```json
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY"
  }
}
```

Keep the key out of source control in production by setting the `Exceptionless__ApiKey` environment variable or using
your secret store instead. Every setting you can put in this section is on the
[configuration reference](/docs/clients/dotnet/configuration/).

You can also configure the client in code. The lambda runs after the configuration section is read:

```csharp
builder.AddExceptionless(c => {
    c.ApiKey = "YOUR_API_KEY";
    c.DefaultTags.Add("checkout-api");
    c.SetVersion("2.4.1");
});
```

> [!NOTE]
> If you self-host Exceptionless, add `"ServerUrl": "https://exceptionless.example.com"` to the `Exceptionless` section,
> pointing at your instance.

### What each call does

| Call                                   | What it does                                                                                                                                                                                                                          |
| -------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `builder.AddExceptionless()`           | Registers `ExceptionlessClient` (the same instance as `ExceptionlessClient.Default`) as a singleton, reads the `Exceptionless` section, and registers an `IExceptionHandler` and a hosted service that flushes the queue on shutdown. |
| `builder.Services.AddProblemDetails()` | Gives the built-in exception handler a response to send. `UseExceptionHandler()` needs it, or an error path of your own.                                                                                                              |
| `app.UseExceptionHandler()`            | Turns on the exception handler pipeline, which calls the Exceptionless `IExceptionHandler` for every unhandled exception.                                                                                                             |
| `app.UseExceptionless()`               | Calls `Startup()` on the client, adds request and user details to events, ignores bot user agents, listens to ASP.NET Core diagnostics, and records 404 responses. Call it before mapping endpoints.                                  |

If you build the host another way, `builder.Services.AddExceptionless()` registers just the client, and
`builder.Services.AddExceptionlessExceptionHandler()` registers just the exception handler.

## Verify

Add an endpoint that throws, run the app, and request it:

```csharp
app.MapGet("/throw", () => {
    throw new InvalidOperationException("Exceptionless test error");
});
```

Open **Stacks** in the app. The error appears within a few seconds, with the request URL, headers, and the machine that
threw it on the event. Remove the endpoint when you're done.

If nothing shows up, see [Troubleshooting](/docs/clients/dotnet/troubleshooting/).

## Use the client in your code

`ExceptionlessClient` is registered in dependency injection. Inject it to submit handled exceptions, feature usage, and
other events:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Controllers/ValuesController.cs -->

```csharp
using Exceptionless;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase {
    private readonly ExceptionlessClient _exceptionless;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ExceptionlessClient exceptionless, ILogger<OrdersController> logger) {
        _exceptionless = exceptionless;
        _logger = logger;
    }

    [HttpPost("{id}/refund")]
    public IActionResult Refund(string id) {
        _exceptionless.SubmitFeatureUsage("Refund");

        try {
            // ...
        } catch (Exception ex) {
            ex.ToExceptionless(_exceptionless)
                .SetProperty("OrderId", id)
                .AddTags("refunds")
                .Submit();

            return Problem("The refund failed.");
        }

        return Ok();
    }
}
```

In minimal APIs, add `ExceptionlessClient` as a handler parameter:

```csharp
app.MapPost("/orders/{id}/refund", (string id, ExceptionlessClient exceptionless) => {
    exceptionless.SubmitFeatureUsage("Refund");
    return Results.Ok();
});
```

Events you submit during a request get the request details automatically, because the package registers
`IHttpContextAccessor`. To attach a specific request to an event outside the request pipeline, call
`.SetHttpContext(httpContext)` on the event builder. More examples are on
[Sending events](/docs/clients/dotnet/sending-events/).

## What is captured automatically

- **Unhandled exceptions** from the request pipeline, through the `IExceptionHandler` and ASP.NET Core diagnostic
  events. Each exception is submitted once per request.
- **404 responses**, as not-found events with the request path.
- **Request details**: HTTP method, URL, host and port, user agent, referrer, client IP address, headers, cookies, and
  query string. The `Authorization`, `Cookie`, and `Proxy-Authorization` headers are left out.
- **Request body** for unhandled exceptions on non-GET requests, when it's form data or a seekable stream of 50 KB or
  less. Call `context.Request.EnableBuffering()` in your own middleware if you want raw bodies.
- **The signed-in user's name** from `HttpContext.User`, when no user identity is set on the event.
- **Environment details**: machine name, operating system, .NET version, process, memory, and IP addresses.
- **Unhandled exceptions outside requests**, from `AppDomain.UnhandledException` and unobserved task exceptions.
- **`ILogger` entries**, if you added `builder.Logging.AddExceptionless()`.

Requests from bots are ignored using the project's user agent bot patterns. To keep private data such as IP addresses,
cookies, and request bodies out of events, see [Private information](/docs/clients/dotnet/private-information/).

Queued events are sent in the background and flushed when the app shuts down. To flush after every request, for example
on AWS Lambda, set `ProcessQueueOnCompletedRequest` to `true` (see [Serverless](/docs/clients/dotnet/serverless/)).

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): logs, feature usage, custom data, and user identity.
- [Logging integrations](/docs/clients/dotnet/logging/): control which `ILogger` entries are sent.
- [Configuration](/docs/clients/dotnet/configuration/): every setting and where to put it.
- [Reference IDs](/docs/references-ids/): show users an ID they can give your support team.

## Ask Exie

```prompt exie
Did the "Exceptionless test error" from my ASP.NET Core app arrive? Show me the stack and its latest event.
```

```prompt exie
Which errors first appeared in this project in the last 24 hours?
```

```prompt exie
Count today's 404 events by source and show me the top paths.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can work from the same data:

```prompt agent
Find the most frequent unhandled exception in my Exceptionless project over the last 7 days, read its latest event and request details, find the code in this ASP.NET Core app that throws it, and propose a fix.
```
