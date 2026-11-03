---
title: "Upgrading the .NET client"
navTitle: "Upgrading"
description: "Upgrade the Exceptionless .NET client to 6.2: .NET 8 for ASP.NET Core and hosting, the new ASP.NET Core setup, and changes from older major versions."
metaTitle: "Upgrading the Exceptionless .NET client"
sources:
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessMiddleware.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/Exceptionless.AspNetCore.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessLifetimeService.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Logging/ExceptionlessLoggerExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.NLog/Exceptionless.NLog.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Log4net/Exceptionless.Log4net.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.MessagePack/Exceptionless.MessagePack.csproj
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
---

# Upgrading the .NET client

Update every Exceptionless package in your app to the same version. This page lists what you need to change, newest
release first.

## Upgrading to 6.2

### ASP.NET Core, hosting, and logging require .NET 8 or later

`Exceptionless.AspNetCore`, `Exceptionless.Extensions.Hosting`, and `Exceptionless.Extensions.Logging` now target .NET
8, 9, and 10 instead of .NET Standard 2.0. Apps on an older .NET, or on .NET Framework, that use these packages need to
move to .NET 8 or stay on 6.1. The core `Exceptionless` package and the other packages still support .NET Standard 2.0
and .NET Framework 4.6.2.

### New ASP.NET Core setup

The `UseExceptionless` overloads that took configuration are gone. Configure the client when you register it, and call
`UseExceptionless()` with no arguments:

```csharp tab="6.2"
var builder = WebApplication.CreateBuilder(args);
builder.AddExceptionless();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseExceptionless();
```

```csharp tab="6.1 and earlier"
services.AddExceptionless();
// ...
app.UseExceptionless(Configuration);
```

| Removed                                                    | Use instead                                                           |
| ---------------------------------------------------------- | --------------------------------------------------------------------- |
| `app.UseExceptionless(IConfiguration)`                     | `builder.AddExceptionless()`, which reads the `Exceptionless` section |
| `app.UseExceptionless(string apiKey)`                      | `builder.AddExceptionless("YOUR_API_KEY")`                            |
| `app.UseExceptionless(Action<ExceptionlessConfiguration>)` | `builder.AddExceptionless(c => { ... })`                              |
| `ILoggerFactory.AddExceptionless(...)`                     | `builder.Logging.AddExceptionless()`                                  |

Unhandled exceptions are now captured through ASP.NET Core's `IExceptionHandler` and diagnostics instead of by the
Exceptionless middleware. `builder.AddExceptionless()` registers the handler. Add `builder.Services.AddProblemDetails()`
and `app.UseExceptionHandler()` as shown above so the handler runs. If you register the client with
`services.AddExceptionless()`, also call `services.AddExceptionlessExceptionHandler()`.

See [ASP.NET Core](/docs/clients/dotnet/aspnet-core/) for the full setup.

### Generic host

`builder.AddExceptionless()` now works on `Host.CreateApplicationBuilder` and any `IHostApplicationBuilder`, and
registers the shutdown flush for you. `ExceptionlessLifetimeService` is now sealed, takes only the client, and runs as
an `IHostedLifecycleService` that sends the queue while the host is stopping. See
[Worker services](/docs/clients/dotnet/worker-services/).

### Request bodies only for unhandled exceptions

The ASP.NET Core and legacy ASP.NET packages now collect the request body only for unhandled exceptions, so reading it
can't break model binding for requests that keep running. `GetRequestInfo` in the ASP.NET Core, Web API, and legacy
ASP.NET packages has a new optional `isUnhandledError` parameter. Recompile code that calls it.

### Dependency updates

| Package                     | Now depends on    | Was                 |
| --------------------------- | ----------------- | ------------------- |
| `Exceptionless.NLog`        | NLog 6.1.3        | NLog 5.1.0          |
| `Exceptionless.Log4net`     | log4net 3.3.1     | log4net 2.0.15      |
| `Exceptionless.MessagePack` | MessagePack 3.1.7 | MessagePack 2.5.187 |

The NLog target now rejects a `field` without a name or layout when NLog loads its configuration.

### Default data exclusions

The client now excludes fields named `__Restricted*` and `Restricted*` by default. See
[Private information](/docs/clients/dotnet/private-information/#data-exclusions).

## Upgrading from 5.x to 6.0

Version 6 changed how events are serialized. Snake case naming now applies only to Exceptionless's own models, so custom
exception properties and data you add keep their names exactly. Empty collections and dictionaries are now sent instead
of left out.

## Upgrading from 4.x to 5.0

Synchronous methods that did network work were removed or made async:

| 4.x                                                     | 5.0                                                                                                                                      |
| ------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| `client.ProcessQueue()`                                 | `await client.ProcessQueueAsync()`                                                                                                       |
| `client.ProcessQueueDeferred()` and `ProcessQueueScope` | An `IAsyncDisposable` that calls `ProcessQueueAsync()` (see [Serverless](/docs/clients/dotnet/serverless/#reuse-the-flush-with-a-scope)) |
| `client.Shutdown()`                                     | `await client.ShutdownAsync()`                                                                                                           |
| `client.SubmitSessionEnd()`                             | `await client.SubmitSessionEndAsync()`                                                                                                   |
| `client.SubmitSessionHeartbeat()`                       | `await client.SubmitSessionHeartbeatAsync()`                                                                                             |
| `client.UpdateUserEmailAndDescription()`                | `await client.UpdateUserEmailAndDescriptionAsync()`                                                                                      |
| `SettingsManager.UpdateSettings()`, `CheckVersion()`    | `UpdateSettingsAsync()`, `CheckVersionAsync()`                                                                                           |
| `client.UnregisterWebApi()` (Web API)                   | `await client.UnregisterWebApiAsync()`                                                                                                   |
| `client.Unregister()` (WPF, Windows Forms)              | `await client.UnregisterAsync()`                                                                                                         |

Custom `ISubmissionClient` and `IEventQueue` implementations must implement the async methods (`PostEventsAsync`,
`PostUserDescriptionAsync`, `GetSettingsAsync`, `SendHeartbeatAsync`, and `ProcessAsync`).

## Upgrading from 3.x

The `Exceptionless.Portable` package and the `Exceptionless.Extras` assembly were merged into `Exceptionless`. Remove
them and reference `Exceptionless`.

## Upgrading from 2.x

Enrichments became plugins:

| 2.x                                              | 3.0 and later                                     |
| ------------------------------------------------ | ------------------------------------------------- |
| `IEventEnrichment`                               | `IEventPlugin`                                    |
| `Enrich(context, event)`                         | `Run(context)`, with the event on `context.Event` |
| `AddEnrichment<T>()`                             | `AddPlugin<T>()`                                  |
| `EventPluginContext.Data`                        | `EventPluginContext.ContextData`                  |
| `EventSubmittingEventArgs.EnrichmentContextData` | `EventSubmittingEventArgs.PluginContextData`      |

## Moving from .NET Framework to ASP.NET Core

When you move a web app from ASP.NET MVC or Web API to ASP.NET Core, replace `Exceptionless.Mvc`,
`Exceptionless.WebApi`, or `Exceptionless.Web` with `Exceptionless.AspNetCore`, remove the `exceptionless` section and
module from `web.config`, and move your settings to the `Exceptionless` section of `appsettings.json`. See
[ASP.NET Core](/docs/clients/dotnet/aspnet-core/) and the
[configuration reference](/docs/clients/dotnet/configuration/).

## Ask Exie

```prompt exie
Compare errors by version for the last 14 days. Did the release after my client upgrade change anything?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the upgrade:

```prompt agent
Upgrade the Exceptionless packages in this repository to 6.2.0. Read https://exceptionless.com/docs/clients/dotnet/upgrading.md and apply every change that affects this code, then build and run the tests. Afterwards, check my Exceptionless project for new errors from this app.
```
