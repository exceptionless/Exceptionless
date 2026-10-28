---
title: "Sending events from .NET"
navTitle: "Sending events"
description: "Send handled exceptions, logs, feature usage, 404s, and custom events from the Exceptionless .NET client, with extra data, user identity, and sessions."
metaTitle: "Sending events from the Exceptionless .NET client"
sources:
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionExtensions.cs
  - Exceptionless.Net:src/Exceptionless/EventBuilder.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/EventBuilderExtensions.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/Events/EventSubmittingEventArgs.cs
  - Exceptionless.Net:src/Exceptionless/Models/Collections/SettingsDictionary.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/040_ReferenceIdPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/900_CancelSessionsWithNoUserPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Controllers/ValuesController.cs
  - Exceptionless.Net:samples/Exceptionless.SampleWindows/Program.cs
---

# Sending events from .NET

Once the client is set up for your [app type](/docs/clients/dotnet/), unhandled exceptions are reported for you. This
page shows how to send everything else: exceptions you catch, logs, feature usage, 404s, and your own event types, and
how to add data to them.

The examples use `ExceptionlessClient.Default`. With dependency injection, inject `ExceptionlessClient` and call the
same methods on it, and pass it to `ToExceptionless(client)`.

## Handled exceptions

Submit an exception you catch with `ToExceptionless()`:

```csharp
using Exceptionless;

try {
    await ImportOrdersAsync();
} catch (Exception ex) {
    ex.ToExceptionless().Submit();
}
```

`ToExceptionless()` returns an event builder, so you can [add data](#add-data-to-an-event) before calling `Submit()`.
Nothing is sent until you call `Submit()`. `ExceptionlessClient.Default.SubmitException(ex)` is a shortcut when you
don't need to add anything.

## Log messages {#log-messages}

```csharp
using Exceptionless;
using Exceptionless.Logging;

ExceptionlessClient.Default.SubmitLog("Import finished");
ExceptionlessClient.Default.SubmitLog(typeof(OrderImporter).FullName, "Import took longer than expected", LogLevel.Warn);

ExceptionlessClient.Default.CreateLog(typeof(OrderImporter).FullName, "Skipped a duplicate order", LogLevel.Info)
    .SetProperty("OrderId", orderId)
    .Submit();
```

The first argument is the log source, usually the class name. Levels are `Trace`, `Debug`, `Info`, `Warn`, `Error`, and
`Fatal`.

Log events with a level are filtered by the project's [log levels](/docs/setting-log-levels/). Until you set one, the
client drops log events below `Warn`, unless a [logging integration](/docs/clients/dotnet/logging/) is installed, which
lets every level through. Set a level for all sources in the app, or set a local default in code:

```csharp
ExceptionlessClient.Default.Configuration.SetDefaultMinLogLevel(LogLevel.Info);
```

If your app already uses `ILogger`, NLog, log4net, or Serilog, send logs through it instead. See
[Logging integrations](/docs/clients/dotnet/logging/).

## Feature usage

Record that someone used a feature, to see which features are used most (see [Feature usage](/docs/feature-usage/)):

```csharp
ExceptionlessClient.Default.SubmitFeatureUsage("Export to CSV");
```

## 404s

The ASP.NET Core and legacy ASP.NET packages record 404s for you. To record one yourself:

```csharp
ExceptionlessClient.Default.SubmitNotFound("/products/discontinued-widget");
```

## Custom event types

Send any event type with `CreateEvent()`:

```csharp
ExceptionlessClient.Default.CreateEvent()
    .SetType("deployment")
    .SetSource("deploy-script")
    .SetMessage("Deployed 2.4.1 to production")
    .Submit();
```

## Add data to an event {#add-data-to-an-event}

Every `Create...` method and `ToExceptionless()` return an event builder. Chain these methods before `Submit()`:

```csharp
using Exceptionless;

try {
    await PlaceOrderAsync(order);
} catch (Exception ex) {
    ex.ToExceptionless()
        // Shown on the event's Extended Data tab. Leave out sensitive properties.
        .AddObject(order, "Order", excludedPropertyNames: new[] { "CardNumber" }, maxDepth: 2)
        .SetProperty("QuoteId", quoteId)
        .AddTags("checkout")
        .MarkAsCritical()
        .SetUserIdentity(user.Id, user.FullName)
        .SetReferenceId(Guid.NewGuid().ToString("N"))
        .Submit();
}
```

| Method                                                      | What it does                                                                              |
| ----------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `SetProperty(name, value)`                                  | Adds a value, shown in its own section on the event's **Extended Data** tab.              |
| `AddObject(value, name, excludedPropertyNames, maxDepth)`   | Adds an object, serialized with optional excluded properties and a maximum depth.         |
| `AddTags(tags)`                                             | Adds tags.                                                                                |
| `MarkAsCritical()`                                          | Marks the event critical.                                                                 |
| `SetUserIdentity(identity, name)`                           | Sets the user for this event.                                                             |
| `SetUserDescription(email, description)`                    | Adds the user's email address and their description of what happened.                     |
| `SetReferenceId(id)`                                        | Sets an ID you can look the event up by. See [Reference IDs](/docs/references-ids/).      |
| `SetEventReference(name, id)`                               | Links the event to another event's reference ID.                                          |
| `SetValue(value)`                                           | Sets a numeric value on the event.                                                        |
| `SetGeo(latitude, longitude)`                               | Sets where the event happened.                                                            |
| `SetVersion(version)`                                       | Overrides the app version for this event.                                                 |
| `SetManualStackingKey(key)`                                 | Groups events with the same key into one stack. See [Stacking](/docs/concepts/stacking/). |
| `SetSource(source)`, `SetMessage(message)`, `SetType(type)` | Set the event's source, message, and type.                                                |

Data you add is checked against your [data exclusions](/docs/clients/dotnet/private-information/#data-exclusions).

To add the same tags and data to every event, use `DefaultTags` and `DefaultData` in the
[configuration](/docs/clients/dotnet/configuration/).

## User identity {#user-identity}

Tell Exceptionless who the current user is, so you can see which users hit an error. Set it for every event after the
user signs in:

```csharp
ExceptionlessClient.Default.Configuration.SetUserIdentity("user-123", "Ada Lovelace");
```

or for one event with `.SetUserIdentity(...)` on the builder. In web apps, set it per event: `Configuration` is shared
by every request. The ASP.NET Core and legacy ASP.NET packages add the signed-in user's name for you when you don't set
one.

## Sessions

Sessions group a user's events and show how long they used your app. Turn them on at startup and set a user identity,
because a session start without a user is dropped:

```csharp
ExceptionlessClient.Default.Configuration.UseSessions();
ExceptionlessClient.Default.Configuration.SetUserIdentity("user-123", "Ada Lovelace");
ExceptionlessClient.Default.Startup();
```

`Startup()` starts a session when sessions are on, and the client sends a heartbeat every minute while the app runs. End
the session when the user signs out:

```csharp
await ExceptionlessClient.Default.SubmitSessionEndAsync();
```

See [Sessions](/docs/user-sessions/) for how sessions work.

## Collect user feedback

Turn on reference IDs so every error event gets a short ID, show it to the user, and attach what they tell you:

```csharp
ExceptionlessClient.Default.Configuration.UseReferenceIds();

// After an error was submitted:
string referenceId = ExceptionlessClient.Default.GetLastReferenceId();

// When the user sends feedback:
await ExceptionlessClient.Default.UpdateUserEmailAndDescriptionAsync(referenceId, "ada@example.com", "I clicked Save twice.");
```

See [Reference IDs](/docs/references-ids/) for a full support workflow.

## Change or cancel events before they're sent {#change-or-cancel-events}

Handle `SubmittingEvent` to add data to every event or to drop events, including unhandled exceptions:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleWindows/Program.cs -->

```csharp
using Exceptionless;

ExceptionlessClient.Default.SubmittingEvent += OnSubmittingEvent;

static void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
    // Ignore an exception type you expect.
    var exception = e.PluginContextData.GetException();
    if (exception is OperationCanceledException) {
        e.Cancel = true;
        return;
    }

    if (e.IsUnhandledError)
        e.Event.Tags.Add("unhandled");
}
```

For reusable logic, write a [plugin](/docs/clients/dotnet/plugins/) instead. Plugins run before `SubmittingEvent`. To
drop events of a type or source without code, use
[client configuration values](/docs/clients/dotnet/client-configuration-values/#turn-events-off-without-code).

## Send events before your app exits

Events are queued and sent in the background every few seconds. In short-lived processes, call
`await ExceptionlessClient.Default.ProcessQueueAsync()` before exiting. See
[Console apps](/docs/clients/dotnet/console/) and [Serverless](/docs/clients/dotnet/serverless/).

## Ask Exie

```prompt exie
Which features were used most this week? Show the top ten.
```

```prompt exie
Show me the critical errors from the last 24 hours.
```

```prompt exie
Which users hit the most errors this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the critical errors in my Exceptionless project from the last 7 days, read the extended data on their latest events, and find the code in this repository that throws them.
```
