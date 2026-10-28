---
title: "Console apps"
description: "Add Exceptionless to a .NET console app or CLI tool: call Startup, submit events, and process the queue before the app exits."
redirectFrom:
  - /docs/clients/dotnet/guides/console-apps-example/
sources:
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/readme.txt
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs
---

# Console apps

The core `Exceptionless` package reports unhandled exceptions and sends events from console apps, CLI tools, and
scheduled jobs. It targets .NET Standard 2.0 and .NET Framework 4.6.2, so it runs on .NET 8, 9, and 10 and on .NET
Framework.

If your console app uses the generic host (`Host.CreateApplicationBuilder`), follow
[Worker services](/docs/clients/dotnet/worker-services/) instead.

## Install

```bash
dotnet add package Exceptionless
```

## Configure

Call `Startup()` on the default client as early as possible in `Program.cs`:

```csharp
using Exceptionless;

ExceptionlessClient.Default.Startup("YOUR_API_KEY");
```

`Startup()` reads configuration from assembly attributes and environment variables, applies the last
[client configuration values](/docs/clients/dotnet/client-configuration-values/) it saved, reports unhandled exceptions,
and sends queued events when the process exits.

To keep the key out of your code, call `Startup()` with no arguments and set the `Exceptionless__ApiKey` environment
variable. See the [configuration reference](/docs/clients/dotnet/configuration/) for the other ways to configure the
client.

Set anything else before you call `Startup()`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs -->

```csharp
using Exceptionless;

ExceptionlessClient.Default.Configuration.SetVersion("1.2.3");
ExceptionlessClient.Default.Configuration.DefaultTags.Add("nightly-import");
ExceptionlessClient.Default.Configuration.UseFolderStorage("exceptionless-queue");
ExceptionlessClient.Default.Startup("YOUR_API_KEY");
```

> [!NOTE]
> If you self-host Exceptionless, set `ExceptionlessClient.Default.Configuration.ServerUrl` to your instance's URL
> before calling `Startup()`.

## Send events before the app exits

The client queues events and sends them in the background. `Startup()` flushes the queue when the process exits
normally, but a CLI tool that does its work and returns should flush explicitly so nothing is lost:

```csharp
using Exceptionless;

ExceptionlessClient.Default.Startup("YOUR_API_KEY");

try {
    await RunImportAsync();
} catch (Exception ex) {
    ex.ToExceptionless().Submit();
    Environment.ExitCode = 1;
} finally {
    await ExceptionlessClient.Default.ProcessQueueAsync();
}
```

`ProcessQueueAsync()` sends everything in the queue and waits for it to finish. To also send a session end and
unregister the client's handlers, call `await ExceptionlessClient.Default.ShutdownAsync()` instead.

## Verify

Submit a test error and flush the queue:

```csharp
using Exceptionless;

ExceptionlessClient.Default.Startup("YOUR_API_KEY");

try {
    throw new InvalidOperationException("Exceptionless test error");
} catch (Exception ex) {
    ex.ToExceptionless().Submit();
}

await ExceptionlessClient.Default.ProcessQueueAsync();
```

Run the app, then open **Stacks** in the app. The error appears within a few seconds.

## Use your own client instance

`ExceptionlessClient.Default` is a shared static instance. Create your own instance when you need more than one
configuration, or want to pass the client to your code explicitly:

```csharp
using Exceptionless;

var client = new ExceptionlessClient(c => {
    c.ApiKey = "YOUR_API_KEY";
    c.DefaultTags.Add("nightly-import");
});

client.SubmitLog("Import started");

try {
    await RunImportAsync();
} catch (Exception ex) {
    ex.ToExceptionless(client).Submit();
} finally {
    await client.ProcessQueueAsync();
}
```

A client you create yourself doesn't report unhandled exceptions or flush on exit until you call `client.Startup()`.

## What is captured automatically

After `Startup()`:

- **Unhandled exceptions** from `AppDomain.UnhandledException` and unobserved task exceptions. The queue is sent right
  away, because the process is about to end.
- **Queued events are sent** when the process exits.
- **Environment details**: machine name, operating system, .NET version, process, command line, memory, and IP
  addresses.
- **Your app's version**, read from the entry assembly unless you call `SetVersion`.
- **Recent `System.Diagnostics.Trace` messages**, attached to events when you set the `TraceLogLimit`
  [client configuration value](/docs/clients/dotnet/client-configuration-values/) to the number of messages to keep. It
  is off until you set it.

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): logs, feature usage, custom data, and user identity.
- [Configuration](/docs/clients/dotnet/configuration/): queue storage, client logging, and disabling the client.
- [Logging integrations](/docs/clients/dotnet/logging/): NLog and log4net.

## Ask Exie

```prompt exie
Did the "Exceptionless test error" arrive? Show me the stack and the machine it came from.
```

```prompt exie
Which errors did the nightly-import tag have this week, grouped by version?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Check Exceptionless for errors from this CLI tool in the last 7 days, group them by version, and tell me whether the latest release introduced any new ones.
```
