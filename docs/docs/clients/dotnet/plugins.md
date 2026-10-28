---
title: ".NET client plugins"
navTitle: "Plugins"
description: "Write Exceptionless .NET client plugins that add data to events or drop them, set their priority, and add or remove them at runtime."
metaTitle: "Exceptionless .NET client plugins"
sources:
  - Exceptionless.Net:src/Exceptionless/Plugins/IEventPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/EventPluginContext.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/PriortyAttribute.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/EventPluginManager.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/Plugins/SystemUptimePlugin.cs
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs
---

# .NET client plugins

A plugin is code that runs on every event before it's queued. Use plugins to add data to events, remove data, or drop
events you don't want. The client's own features, such as error details, environment info, and log level filtering, are
plugins too.

## Write a plugin

Implement `IEventPlugin`. The `Run` method gets an `EventPluginContext`:

| Property      | What it is                                                                                                                                                    |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Event`       | The event being submitted. Change it in place.                                                                                                                |
| `ContextData` | Data passed with the submission, such as the original exception (`ContextData.GetException()`) and whether it was unhandled (`ContextData.IsUnhandledError`). |
| `Client`      | The `ExceptionlessClient`, including its `Configuration` and [client configuration values](/docs/clients/dotnet/client-configuration-values/).                |
| `Resolver`    | The client's dependency resolver.                                                                                                                             |
| `Log`         | The client's diagnostic log.                                                                                                                                  |
| `Cancel`      | Set to `true` to drop the event. No later plugins run.                                                                                                        |

This plugin adds how long the machine has been up to every feature usage event. It works on Windows, Linux, and macOS:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleConsole/Plugins/SystemUptimePlugin.cs -->

```csharp
using System;
using System.Diagnostics;
using Exceptionless.Models;
using Exceptionless.Plugins;

[Priority(100)]
public class SystemUptimePlugin : IEventPlugin {
    public void Run(EventPluginContext context) {
        // Only update feature usage events.
        if (context.Event.Type != Event.KnownTypes.FeatureUsage)
            return;

        var uptime = TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);

        // Shown in its own section on the event's Extended Data tab.
        context.Event.SetProperty("System Uptime", $"{uptime.Days} Days {uptime.Hours} Hours {uptime.Minutes} Minutes {uptime.Seconds} Seconds");
    }
}
```

Properties a plugin adds with `SetProperty` show up on the event's **Extended Data** tab, each in its own section:

![Custom data on an event's Extended Data tab](/assets/img/docs/plugins-extended-data-light.webp)

## Add a plugin

Register a plugin class before the client starts:

```csharp
using Exceptionless;

ExceptionlessClient.Default.Configuration.AddPlugin<SystemUptimePlugin>();
```

In ASP.NET Core or worker services, do it in the configuration lambda:

```csharp
builder.AddExceptionless(c => c.AddPlugin<SystemUptimePlugin>());
```

For something small, pass a key, a priority, and an action instead of writing a class. The key lets you remove it later:

```csharp
ExceptionlessClient.Default.Configuration.AddPlugin("region", 100, context => {
    context.Event.SetProperty("Region", Environment.GetEnvironmentVariable("REGION"));
});
```

Adding a plugin with a key or class that's already registered replaces it.

## Drop events {#exclude-events}

Set `context.Cancel = true` to drop an event:

```csharp
ExceptionlessClient.Default.Configuration.AddPlugin("ignore-cancellations", 5, context => {
    if (context.ContextData.GetException() is OperationCanceledException)
        context.Cancel = true;
});
```

For a simple yes-or-no check on the event, `AddEventExclusion` is shorter. Return `false` to drop the event:

```csharp
ExceptionlessClient.Default.Configuration.AddEventExclusion(ev => ev.Source != "HealthCheck");
```

To drop events by type, source, or exception type without code, use the
[built-in client configuration values](/docs/clients/dotnet/client-configuration-values/#turn-events-off-without-code).

## Priority

Plugins run in priority order, lowest first. The order of plugins with the same priority isn't guaranteed. Set the
priority with `[Priority(n)]` on a class or the second argument of `AddPlugin`. A plugin with no priority runs at `0`,
before the built-in plugins.

| Priority | Built-in plugin                                                           |
| -------- | ------------------------------------------------------------------------- |
| 5        | Splits aggregate exceptions into separate events                          |
| 10       | Event exclusions and log level filtering                                  |
| 15       | Adds default tags and data                                                |
| 16       | Sets the Windows user as the event's user (WPF and Windows Forms)         |
| 20       | Adds the error details and stack trace                                    |
| 25       | Manages session IDs, with `UseSessions(useSessionIdManagement: true)`     |
| 40       | Adds reference IDs, when turned on                                        |
| 50       | Adds environment information                                              |
| 70       | Adds recent trace messages                                                |
| 80       | Adds the app version                                                      |
| 90       | Records how the event was submitted, and adds request details in web apps |
| 100      | Session heartbeats, when sessions are on                                  |
| 110      | Drops web requests from bot user agents                                   |
| 900      | Drops session starts that have no user                                    |
| 910      | Merges duplicate events submitted within a minute                         |

Pick a priority based on what you need:

- **Below 10** to drop events before anything else runs.
- **Around 100** to add data after the client has added its own.
- **Above 900** to see events exactly as they'll be sent.

## Remove a plugin

Remove a class plugin by type, and an action plugin by its key:

```csharp
ExceptionlessClient.Default.Configuration.RemovePlugin<SystemUptimePlugin>();
ExceptionlessClient.Default.Configuration.RemovePlugin("region");
```

## Plugins and SubmittingEvent

The client raises its `SubmittingEvent` event after all plugins have run, so handlers see the finished event and can
still cancel it. Plugins are better for reusable logic and can be tested on their own. See
[Sending events](/docs/clients/dotnet/sending-events/#change-or-cancel-events).

## Ask Exie

```prompt exie
Show me the latest feature usage event and its extended data.
```

```prompt exie
Which error types did this project get most often this week? I'm deciding what to filter out.
```
