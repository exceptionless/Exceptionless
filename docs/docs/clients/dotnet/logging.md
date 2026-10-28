---
title: "Logging integrations"
description: "Send log entries to Exceptionless from Microsoft.Extensions.Logging, NLog, log4net, and Serilog, and control which levels are sent."
redirectFrom:
  - /docs/clients/dotnet/guides/logging-with-generic-host/
sources:
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Logging/ExceptionlessLoggerExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Logging/ExceptionlessLoggerProvider.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Logging/ExceptionlessLogger.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.NLog/ExceptionlessTarget.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.NLog/LogEventBuilderExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.NLog/readme.txt
  - Exceptionless.Net:src/Platforms/Exceptionless.Log4net/ExceptionlessAppender.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Log4net/readme.txt
  - Exceptionless.Net:src/Exceptionless/Models/Collections/SettingsDictionary.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Program.cs
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/App.config
---

# Logging integrations

If your app already writes logs through a logging library, Exceptionless can collect them. Log entries become log events
in Exceptionless, and entries that carry an exception become error events, so they stack with the rest of your errors.

| Library                      | Package                            | Targets                                    |
| ---------------------------- | ---------------------------------- | ------------------------------------------ |
| Microsoft.Extensions.Logging | `Exceptionless.Extensions.Logging` | .NET 8, 9, and 10                          |
| NLog                         | `Exceptionless.NLog`               | .NET Standard 2.0 and .NET Framework 4.6.2 |
| log4net                      | `Exceptionless.Log4net`            | .NET Standard 2.0 and .NET Framework 4.6.2 |
| Serilog                      | `Serilog.Sinks.Exceptionless`      | Separate package, versioned on its own     |

## Choose which levels are sent

Two filters decide whether an entry becomes an event:

1. **Your logging library's rules**, such as the `Logging` section in `appsettings.json`, NLog's `minlevel`, or
   log4net's `level`. Entries they filter out never reach Exceptionless.
2. **Exceptionless log levels**, which you change in the app without redeploying. They come from
   [client configuration values](/docs/clients/dotnet/client-configuration-values/) named `@@log:<source>`, where the
   source is the logger name or category, and `*` matches any source. See
   [Logs and log levels](/docs/setting-log-levels/) to set them in the app.

When no Exceptionless log level is set, the Microsoft.Extensions.Logging provider and the NLog target set the default
level for all sources (`@@log:*`) to `Trace`, so your logging rules decide. Without one of them, the client drops log
events below `Warn` until you set a level (see [Sending events](/docs/clients/dotnet/sending-events/#log-messages)).

A practical setup: keep the logging library permissive for the Exceptionless provider or target, and raise or lower the
level per source in the app when you need more detail.

| Microsoft.Extensions.Logging | Exceptionless |
| ---------------------------- | ------------- |
| `Trace`                      | `Trace`       |
| `Debug`                      | `Debug`       |
| `Information`                | `Info`        |
| `Warning`                    | `Warn`        |
| `Error`                      | `Error`       |
| `Critical`                   | `Fatal`       |

## Microsoft.Extensions.Logging

Install the provider alongside the [ASP.NET Core](/docs/clients/dotnet/aspnet-core/) or
[worker services](/docs/clients/dotnet/worker-services/) package:

```bash
dotnet add package Exceptionless.Extensions.Logging
```

Add it to the logging builder. It uses the `ExceptionlessClient` registered by `builder.AddExceptionless()`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Program.cs -->

```csharp
using Exceptionless;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddExceptionless();
builder.AddExceptionless();
```

Then decide which categories reach it with the standard `Logging` configuration. This sends `Information` and above from
your code, and only warnings and errors from the framework:

```json
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  }
}
```

Every entry that passes these rules is sent, so tighten them if your app logs heavily at `Information`.

### What each entry includes

- The **category** becomes the event source, so you can set Exceptionless log levels per category.
- An entry with an **exception** becomes an error event with the full stack trace. The log message is kept as the event
  message.
- **Structured values** in the message template become event properties.
  `_logger.LogWarning("Order {OrderId} is late",
  id)` adds an `OrderId` property.
- A non-zero **`EventId`** is added as a property.
- Entries written inside a **`BeginScope`** link to their parent scope through an event reference.

### Other ways to add the provider

| Call                                  | Client it uses                                                                                       |
| ------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `AddExceptionless()`                  | The `ExceptionlessClient` from dependency injection, or `ExceptionlessClient.Default`.               |
| `AddExceptionless(client)`            | The client you pass.                                                                                 |
| `AddExceptionless(c => { ... })`      | `ExceptionlessClient.Default`, configured by the lambda.                                             |
| `AddExceptionless(apiKey, serverUrl)` | `ExceptionlessClient.Default` with that key, switched to in-memory queue storage for faster logging. |

## NLog

```bash
dotnet add package Exceptionless.NLog
```

Add the target to `NLog.config`. Set `minlevel` to `Trace` so Exceptionless log levels decide what's kept:

<!-- source: Exceptionless.Net:src/Platforms/Exceptionless.NLog/readme.txt -->

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets async="true">
    <target type="Exceptionless, Exceptionless.NLog" name="exceptionless" apiKey="YOUR_API_KEY">
      <field name="host" layout="${machinename}" />
      <field name="process" layout="${processname}" />
    </target>
  </targets>
  <rules>
    <logger name="*" minlevel="Trace" writeTo="exceptionless" />
  </rules>
</nlog>
```

Each `field` adds a property to every event. The target also accepts `serverUrl`, `userIdentity`, and `userIdentityName`
layouts.

When you set `apiKey` or `serverUrl` on the target, it creates its own client that keeps its queue in memory. Leave them
out to use `ExceptionlessClient.Default` and its configuration, which is what you want if your app also calls
`Startup()` or `AddExceptionless()`.

You can also configure the target in code:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs -->

```csharp
using Exceptionless.NLog;
using NLog;
using NLog.Config;

var config = new LoggingConfiguration();
var exceptionlessTarget = new ExceptionlessTarget();
config.AddTarget("exceptionless", exceptionlessTarget);
config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Trace, exceptionlessTarget));
LogManager.Configuration = config;
```

The package adds Exceptionless helpers to NLog's fluent log builder:

```csharp
using Exceptionless.NLog;

var logger = LogManager.GetCurrentClassLogger();
logger.ForWarnEvent()
    .Message("Payment provider is slow")
    .Tag("payments")
    .Property("ElapsedMs", 2400)
    .Log();
```

`Tag`, `Critical`, `Identity`, `ContextProperty`, and `MarkUnhandled` are available.

## log4net

```bash
dotnet add package Exceptionless.Log4net
```

Add the appender to your log4net configuration:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleConsole/App.config -->

```xml
<log4net>
  <appender name="exceptionless" type="Exceptionless.Log4net.ExceptionlessAppender, Exceptionless.Log4net">
    <apiKey value="YOUR_API_KEY" />
  </appender>
  <root>
    <level value="DEBUG" />
    <appender-ref ref="exceptionless" />
  </root>
</log4net>
```

With `apiKey` or `serverUrl` set, the appender creates its own client that keeps its queue in memory and lets every
level through. Without them, it uses `ExceptionlessClient.Default`, which drops entries below `Warn` until you set an
Exceptionless log level.

## Serilog

Serilog support is in the separate
[`Serilog.Sinks.Exceptionless`](https://github.com/exceptionless/serilog-sinks-exceptionless) package, which is not part
of Exceptionless.Net and has its own releases. It uses `ExceptionlessClient.Default`, so start that client first:

```bash
dotnet add package Serilog.Sinks.Exceptionless
```

```csharp
using Exceptionless;
using Serilog;

ExceptionlessClient.Default.Startup("YOUR_API_KEY");

Log.Logger = new LoggerConfiguration()
    .WriteTo.Exceptionless(b => b.AddTags("Serilog"))
    .CreateLogger();
```

Call `await Log.CloseAndFlushAsync()` before your app exits. See the sink's README for its other options.

## High-volume logging

Queuing to disk adds work for every event. If you log thousands of entries a minute, keep the logging client's queue in
memory with `ExceptionlessClient.Default.Configuration.UseInMemoryStorage()`, and accept that unsent entries are lost if
the process crashes. Raise the Exceptionless log level for noisy sources instead of logging less in code.

## Ask Exie

```prompt exie
Which log sources sent the most warnings and errors today?
```

```prompt exie
Count log events by level for the last 24 hours, by hour.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Look at the noisiest log sources in my Exceptionless project this week, find where this code logs them, and tell me which ones should be lowered to Debug.
```
