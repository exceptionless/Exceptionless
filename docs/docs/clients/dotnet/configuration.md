---
title: ".NET client configuration"
navTitle: "Configuration"
description: "Every Exceptionless .NET client setting: appsettings.json keys, environment variables, and ExceptionlessConfiguration properties and methods."
metaTitle: "Exceptionless .NET client configuration reference"
sources:
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Dependency/DependencyResolver.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/080_VersionPlugin.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/ExceptionlessExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.MessagePack/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/appsettings.json
---

# .NET client configuration

The .NET client reads its settings from `appsettings.json` and environment variables, and you can set anything in code.
This page lists every setting. For app-specific setup, start from your app type on the
[.NET overview](/docs/clients/dotnet/). For `web.config`, `app.config`, and assembly attributes on .NET Framework, see
[Legacy ASP.NET](/docs/clients/dotnet/legacy-aspnet/#the-exceptionless-config-section).

## Where settings come from

| How the client starts                                         | What it reads                                                                                                                              |
| ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `builder.AddExceptionless()` or `services.AddExceptionless()` | The `Exceptionless` section of `IConfiguration` (`appsettings.json`, environment variables, user secrets, and so on), then your lambda.    |
| `app.UseExceptionless()` in ASP.NET Core                      | Calls `Startup()`, which also reads assembly attributes and environment variables, and applies saved client configuration values.          |
| `client.Startup()`                                            | Assembly attributes, the config section and `appSettings` on .NET Framework, environment variables, and saved client configuration values. |
| `new ExceptionlessClient(c => ...)`                           | Only what you set in the lambda, until you call `Startup()` or one of the `ReadFrom...` methods.                                           |

Configure the client before it sends its first event. After that, changing `ApiKey` or `ServerUrl` throws an
`ArgumentException`.

## appsettings.json

The `Exceptionless` section supports these keys:

<!-- source: Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs (ReadFromConfiguration) -->

```json
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY",
    "ServerUrl": "https://exceptionless.example.com",
    "Enabled": true,
    "IncludePrivateInformation": false,
    "ProcessQueueOnCompletedRequest": false,
    "StoragePath": "exceptionless-queue",
    "StorageSerializer": "Exceptionless.MessagePack.MessagePackStorageSerializer, Exceptionless.MessagePack",
    "QueueMaxAge": "3.00:00:00",
    "QueueMaxAttempts": 5,
    "EnableLogging": true,
    "LogPath": "exceptionless-client.log",
    "DefaultTags": ["checkout-api"],
    "DefaultData": {
      "Region": "us-east"
    },
    "Settings": {
      "IncludeOrderData": "true"
    }
  }
}
```

| Key                              | Default            | What it does                                                                                                                                                                      |
| -------------------------------- | ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ApiKey`                         | none               | Your project's API key. The client sends nothing without one.                                                                                                                     |
| `ServerUrl`                      | The hosted service | Your self-hosted Exceptionless URL. Only set it if you self-host.                                                                                                                 |
| `Enabled`                        | `true`             | `false` turns the client off. Only `false` has an effect.                                                                                                                         |
| `IncludePrivateInformation`      | `true`             | `false` leaves out user names, machine names, IP addresses, headers, cookies, POST data, and query strings. See [Private information](/docs/clients/dotnet/private-information/). |
| `ProcessQueueOnCompletedRequest` | `false`            | `true` sends the queue at the end of every ASP.NET Core request. For [serverless](/docs/clients/dotnet/serverless/) hosts.                                                        |
| `StoragePath`                    | in memory          | A folder for the queue. See [Queue storage](#queue-storage).                                                                                                                      |
| `StorageSerializer`              | JSON               | An assembly-qualified type that serializes the stored queue.                                                                                                                      |
| `QueueMaxAge`                    | `7.00:00:00`       | How long a queued event is kept and retried, as a `TimeSpan`.                                                                                                                     |
| `QueueMaxAttempts`               | `3`                | How many times the client tries to send an event.                                                                                                                                 |
| `EnableLogging`                  | `false`            | `true` writes the client's own diagnostic log, to `LogPath` or to `exceptionless.log` in `StoragePath`. See [Troubleshooting](/docs/clients/dotnet/troubleshooting/).             |
| `LogPath`                        | none               | The diagnostic log file.                                                                                                                                                          |
| `DefaultTags`                    | none               | Tags added to every event.                                                                                                                                                        |
| `DefaultData`                    | none               | Values added to every event as extended data. Values are strings.                                                                                                                 |
| `Settings`                       | none               | Local [client configuration values](/docs/clients/dotnet/client-configuration-values/). Values from your project override them.                                                   |

## Environment variables {#environment-variables}

In ASP.NET Core, worker services, and any app that uses `IConfiguration`, every key above can come from an environment
variable. Use a double underscore for each level, and an index for list items:

```bash
Exceptionless__ApiKey=YOUR_API_KEY
Exceptionless__ServerUrl=https://exceptionless.example.com
Exceptionless__DefaultTags__0=checkout-api
Exceptionless__DefaultData__Region=us-east
```

`Startup()` and `ReadFromEnvironmentalVariables()` read four variables directly from the process environment, with
either `:` or `__` as the separator:

| Variable                                        | What it sets                                                     |
| ----------------------------------------------- | ---------------------------------------------------------------- |
| `Exceptionless__ApiKey`                         | `ApiKey`                                                         |
| `Exceptionless__ServerUrl`                      | `ServerUrl`                                                      |
| `Exceptionless__Enabled`                        | `false` turns the client off.                                    |
| `Exceptionless__ProcessQueueOnCompletedRequest` | `true` sends the queue at the end of every ASP.NET Core request. |

When `Exceptionless__ProcessQueueOnCompletedRequest` isn't set and the `FUNCTIONS_WORKER_RUNTIME` variable is (as it is
in Azure Functions), the client turns `ProcessQueueOnCompletedRequest` on.

## Code

Set properties on `ExceptionlessClient.Default.Configuration`, on the lambda passed to `AddExceptionless` or
`new ExceptionlessClient(...)`, or on any client's `Configuration`:

```csharp
using Exceptionless;

builder.AddExceptionless(c => {
    c.ApiKey = "YOUR_API_KEY";
    c.SetVersion("2.4.1");
    c.DefaultTags.Add("checkout-api");
    c.DefaultData["Region"] = "us-east";
    c.IncludePrivateInformation = false;
    c.UseFolderStorage("exceptionless-queue");
});
```

### Properties

| Property                              | Default                     | What it does                                                                                                                      |
| ------------------------------------- | --------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| `ApiKey`                              | none                        | Your project's API key.                                                                                                           |
| `ServerUrl`                           | The hosted service          | Your self-hosted URL. Setting it also sets `ConfigServerUrl` and `HeartbeatServerUrl`.                                            |
| `ConfigServerUrl`                     | The hosted service          | Where client configuration values are fetched from.                                                                               |
| `HeartbeatServerUrl`                  | The hosted service          | Where session heartbeats are sent.                                                                                                |
| `Enabled`                             | `true`                      | Turns the client on or off.                                                                                                       |
| `DefaultTags`                         | empty                       | Tags added to every event.                                                                                                        |
| `DefaultData`                         | empty                       | Values added to every event. Any object is serialized.                                                                            |
| `Settings`                            | empty                       | [Client configuration values](/docs/clients/dotnet/client-configuration-values/), local and synced from your project.             |
| `IncludePrivateInformation`           | `true`                      | Sets all the `Include*` flags below at once.                                                                                      |
| `IncludeUserName`                     | `true`                      | See [Private information](/docs/clients/dotnet/private-information/) for each flag.                                               |
| `IncludeMachineName`                  | `true`                      |                                                                                                                                   |
| `IncludeIpAddress`                    | `true`                      |                                                                                                                                   |
| `IncludeHeaders`                      | `true`                      |                                                                                                                                   |
| `IncludeCookies`                      | `true`                      |                                                                                                                                   |
| `IncludePostData`                     | `true`                      |                                                                                                                                   |
| `IncludeQueryString`                  | `true`                      |                                                                                                                                   |
| `IncludeModules`                      | `true`                      | Includes the loaded assemblies in error events.                                                                                   |
| `QueueMaxAge`                         | 7 days                      | How long a queued event is kept and retried.                                                                                      |
| `QueueMaxAttempts`                    | `3`                         | How many times the client tries to send an event.                                                                                 |
| `SubmissionBatchSize`                 | `50`                        | How many events are sent in one request.                                                                                          |
| `ProcessQueueOnCompletedRequest`      | `false`                     | Sends the queue at the end of every ASP.NET Core request.                                                                         |
| `UpdateSettingsWhenIdleInterval`      | 2 minutes after `Startup()` | How often to check for new client configuration values while no events are sent. Minimum 2 minutes. `TimeSpan.Zero` turns it off. |
| `Proxy`                               | system proxy                | An `IWebProxy` for requests to Exceptionless. See [Troubleshooting](/docs/clients/dotnet/troubleshooting/#proxies).               |
| `ServerCertificateValidationCallback` | none                        | Custom server certificate validation. See [Troubleshooting](/docs/clients/dotnet/troubleshooting/#certificates).                  |
| `UserAgent`                           | `exceptionless/<version>`   | The user agent sent with requests.                                                                                                |

### Methods

| Method                                                                    | What it does                                                                                                                              |
| ------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `SetVersion(version)`                                                     | Sets your app's version on every event. See [Versioning](#versioning).                                                                    |
| `SetUserIdentity(identity, name)`                                         | Sets the user on every event. See [Sending events](/docs/clients/dotnet/sending-events/#user-identity).                                   |
| `UseSessions(sendHeartbeats, heartbeatInterval, useSessionIdManagement)`  | Turns on [sessions](/docs/user-sessions/). Heartbeats are on by default, every minute.                                                    |
| `UseReferenceIds()`                                                       | Adds a [reference ID](/docs/references-ids/) to every error event.                                                                        |
| `UseFolderStorage(folder)`                                                | Stores the queue in a folder. See [Queue storage](#queue-storage).                                                                        |
| `UseInMemoryStorage()`                                                    | Keeps the queue in memory, the default.                                                                                                   |
| `UseMessagePackSerializer()`                                              | Serializes the stored queue with MessagePack. From the `Exceptionless.MessagePack` package, in the `Exceptionless.MessagePack` namespace. |
| `UseFileLogger(path, minLevel)`                                           | Writes the client's diagnostic log to a file.                                                                                             |
| `UseTraceLogger(minLevel)`                                                | Writes the client's diagnostic log to `System.Diagnostics.Trace`.                                                                         |
| `UseInMemoryLogger(minLevel)`                                             | Keeps the client's diagnostic log in memory and returns it.                                                                               |
| `UseLogger(logger)`                                                       | Uses your own `IExceptionlessLog`.                                                                                                        |
| `AddPlugin(...)`, `RemovePlugin(...)`                                     | Adds or removes a [plugin](/docs/clients/dotnet/plugins/).                                                                                |
| `AddEventExclusion(predicate)`                                            | Drops events for which the predicate returns `false`. See [Plugins](/docs/clients/dotnet/plugins/#exclude-events).                        |
| `AddDataExclusions(patterns)`                                             | Removes matching fields from events. See [Private information](/docs/clients/dotnet/private-information/#data-exclusions).                |
| `AddUserAgentBotPatterns(patterns)`                                       | Ignores web requests from matching user agents, in addition to the project's patterns.                                                    |
| `SetDefaultMinLogLevel(level)`                                            | Sets the minimum log level used until one comes from your project.                                                                        |
| `TrustCertificateThumbprint(thumbprint)`, `TrustCAThumbprint(thumbprint)` | Trusts a specific server or CA certificate. See [Troubleshooting](/docs/clients/dotnet/troubleshooting/#certificates).                    |
| `ReadFromConfiguration(configuration)`                                    | Reads the `Exceptionless` section of an `IConfiguration`.                                                                                 |
| `ReadFromEnvironmentalVariables()`                                        | Reads the four environment variables listed above.                                                                                        |
| `ReadFromAttributes(assemblies)`                                          | Reads [assembly attributes](/docs/clients/dotnet/legacy-aspnet/#assembly-attributes).                                                     |

## Versioning {#versioning}

Set your app's version so you can see which release an error came from and mark stacks fixed in a version (see
[Releases and regressions](/docs/versioning/)):

```csharp
c.SetVersion("2.4.1");
```

If you don't set one, the client uses the entry assembly's informational version or file version.

## Queue storage {#queue-storage}

By default the queue is kept in memory, so events that haven't been sent are lost if the process stops. To keep them
across restarts, store the queue in a folder the app can write to:

```csharp
c.UseFolderStorage("exceptionless-queue");
```

or `"StoragePath": "exceptionless-queue"` in `appsettings.json`. Relative paths are resolved from the current directory.
Writing every event to disk adds work, so use in-memory storage for high-volume logging.

To make the stored queue smaller, install `Exceptionless.MessagePack` and call `c.UseMessagePackSerializer()`. Don't
switch serializers on a folder that still holds unsent events.

## Turn the client off {#disabling-exceptionless}

Set `Enabled` to `false`, for example in `appsettings.Development.json`:

```json
{
  "Exceptionless": {
    "Enabled": false
  }
}
```

The client then submits and sends nothing.

## Self-hosted Exceptionless {#self-hosted-options}

Point the client at your instance with `ServerUrl`:

```json
{
  "Exceptionless": {
    "ApiKey": "YOUR_API_KEY",
    "ServerUrl": "https://exceptionless.example.com"
  }
}
```

The client sends events, fetches client configuration values, and sends heartbeats to that URL. See
[Self-hosting](/docs/self-hosting/).
