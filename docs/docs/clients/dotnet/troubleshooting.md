---
title: "Troubleshooting the .NET client"
navTitle: "Troubleshooting"
description: "Find out why .NET client events don't arrive: turn on client logging, check the queue and storage, and set up proxies and certificates."
metaTitle: "Troubleshooting the Exceptionless .NET client"
sources:
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/Queue/DefaultEventQueue.cs
  - Exceptionless.Net:src/Exceptionless/Submission/DefaultSubmissionClient.cs
  - Exceptionless.Net:src/Exceptionless/Submission/SubmissionResponse.cs
  - Exceptionless.Net:src/Exceptionless/Storage/FolderObjectStorage.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/FileStorageExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/910_DuplicateCheckerPlugin.cs
  - Exceptionless.Net:build/common.props
---

# Troubleshooting the .NET client

If events from your .NET app don't show up in Exceptionless, work through these checks. Most problems are a missing API
key, a process that exits before its queue is sent, or an event the client drops on purpose.

## Update the client

Make sure every Exceptionless package in your app is on the same, latest version (6.2.0 at the time of writing). See
[Upgrading](/docs/clients/dotnet/upgrading/).

## Turn on client logging {#enable-client-logging}

The client writes nothing about itself unless you give it a logger. Turn on its diagnostic log to see what it's doing:

```csharp tab="Code"
using Exceptionless;
using Exceptionless.Logging;

ExceptionlessClient.Default.Configuration.UseFileLogger("exceptionless.log", LogLevel.Trace);
```

```json tab="appsettings.json"
{
  "Exceptionless": {
    "EnableLogging": true,
    "LogPath": "exceptionless.log"
  }
}
```

Make sure the app can write to the file. `UseTraceLogger(LogLevel.Trace)` writes to `System.Diagnostics.Trace` instead,
and `UseInMemoryLogger()` keeps entries in memory where you can inspect them. The log shows when the client starts, each
event it queues or drops and why, each batch it sends, and every error from the server.

## Check the configuration

The client turns itself off when its configuration is invalid. The log says `Configuration is invalid` with the reason.
Check that:

- the API key is set, has at least 10 characters, has no spaces, and isn't the `API_KEY_HERE` placeholder;
- `Enabled` isn't `false`, in code, `appsettings.json`, or the `Exceptionless__Enabled` environment variable;
- if you self-host, `ServerUrl` points at your instance;
- you call `Startup()`, `AddExceptionless()`, or the registration for your [app type](/docs/clients/dotnet/) before the
  first event.

Set the API key and server URL before the first event. Changing them afterwards throws an `ArgumentException`.

## Make sure the queue is sent {#queue}

Events are queued and sent in the background every 10 seconds, in batches of 50. If the process exits first, queued
events are lost unless they're stored on disk.

- **Console apps, CLI tools, and scripts**: call `await ExceptionlessClient.Default.ProcessQueueAsync()` before exiting.
  See [Console apps](/docs/clients/dotnet/console/).
- **Serverless functions**: process the queue before each invocation returns. See
  [Serverless](/docs/clients/dotnet/serverless/).
- **ASP.NET Core and worker services**: the queue is sent when the host stops. A process that's killed doesn't stop
  cleanly.

When sending fails, the client reacts to the server's response:

| Response                              | What the client does                                                                            |
| ------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `401` or `403`                        | Logs that it can't authenticate, drops the batch, and pauses for 15 minutes. Check the API key. |
| `402`                                 | Your organization is over its plan limit. Drops queued events and new ones for 5 minutes.       |
| `400` or `404`                        | Drops the batch and pauses for 4 hours. Check `ServerUrl`.                                      |
| `413`                                 | Retries with smaller batches, and drops a single event that's still too large.                  |
| `503`, other errors, or no connection | Keeps the events and retries in 5 minutes.                                                      |

Events are kept and retried for up to 7 days or 3 attempts. Change that with `QueueMaxAge` and `QueueMaxAttempts` (see
[Configuration](/docs/clients/dotnet/configuration/)). Calling `ProcessQueueAsync()` sends right away, even while
background sending is paused.

## Check whether the event was dropped on purpose

The client drops some events before they're queued. The client log says which and why.

- **Log levels.** Log events below the project's log level are dropped. With no level set and no
  [logging integration](/docs/clients/dotnet/logging/), that's anything below `Warn`. See
  [Sending events](/docs/clients/dotnet/sending-events/#log-messages).
- **Client configuration values.** Keys like `@@error:<type>` or `@@404:*` set to `false` drop matching events. See
  [Client configuration values](/docs/clients/dotnet/client-configuration-values/#turn-events-off-without-code).
- **Your own code.** A [plugin](/docs/clients/dotnet/plugins/), event exclusion, or `SubmittingEvent` handler set
  `Cancel`.
- **Duplicates.** Repeats of an identical event within a minute are held back and sent later as one event with a count.
- **Bots.** Web requests whose user agent matches the project's bot patterns are ignored.
- **Sessions.** Session starts without a user identity are dropped.

Exceptionless can also discard events on the server, for example when a stack is discarded or the project is over its
limits. See [Limits and deduplication](/docs/concepts/limits/).

## Check the storage folder {#storage}

The queue is kept in memory unless you call `UseFolderStorage` or set `StoragePath`. With folder storage, queued events
are JSON files under `<folder>/<first 8 characters of the API key>/q/`. If files pile up there, the client isn't able to
send them, and the client log says why.

The account the app runs as needs read and write access to the folder. On IIS that's the application pool identity, and
in containers it's the container user.

## Proxies {#proxies}

The client uses the system proxy by default. To use a specific proxy, set `Configuration.Proxy` before the client
starts:

```csharp
using System.Net;
using Exceptionless;

ExceptionlessClient.Default.Configuration.Proxy = new WebProxy("http://proxy.example.com:8080") {
    UseDefaultCredentials = true
};
```

On .NET Framework, you can also configure the default proxy in `web.config` or `app.config`:

```xml
<system.net>
  <defaultProxy useDefaultCredentials="true">
    <proxy proxyaddress="http://proxy.example.com:8080" usesystemdefault="true" />
  </defaultProxy>
</system.net>
```

Your firewall must allow outbound HTTPS from the app to the hosted service's `exceptionless.io` hosts, or to your
self-hosted `ServerUrl`. Blazor WebAssembly apps send from the browser and use the browser's network settings.

## Certificates {#certificates}

If your self-hosted instance uses a certificate the machine doesn't trust, such as one from an internal certificate
authority, trust it explicitly instead of turning validation off:

```csharp
// Trust one server certificate.
ExceptionlessClient.Default.Configuration.TrustCertificateThumbprint("SERVER_CERT_THUMBPRINT");

// Or trust every certificate issued by your internal CA.
ExceptionlessClient.Default.Configuration.TrustCAThumbprint("CA_CERT_THUMBPRINT");
```

Both accept certificates that already pass validation. For custom rules, set `ServerCertificateValidationCallback`.
`SkipCertificateValidation()` accepts any certificate and is marked obsolete. Don't use it in production.

## Debug into the client

The packages include symbols and Source Link, so you can step into the client's code. Turn on Source Link and turn off
Just My Code in your debugger.

## Ask Exie

```prompt exie
Did this project receive any events in the last hour? Break them down by type.
```

```prompt exie
When did this project last receive an error event, and from which version?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can check too:

```prompt agent
I just deployed this app with Exceptionless set up. Check my Exceptionless project for events from the last 15 minutes and tell me whether the deploy is reporting. If nothing arrived, review how this app configures the Exceptionless client and find the problem.
```
