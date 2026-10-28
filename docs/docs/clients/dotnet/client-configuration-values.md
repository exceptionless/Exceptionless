---
title: "Client configuration values in .NET"
navTitle: "Client configuration values"
description: "Read Exceptionless client configuration values in .NET, react when they change, and use built-in keys to turn events off without redeploying."
redirectFrom:
  - /docs/clients/dotnet/settings/
sources:
  - Exceptionless.Net:src/Exceptionless/Models/Collections/SettingsDictionary.cs
  - Exceptionless.Net:src/Exceptionless/Models/Collections/ObservableDictionary.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/SettingsManager.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/Submission/DefaultSubmissionClient.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/070_TraceLogPlugin.cs
  - Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/configuration-values/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/dialogs/add-project-config-dialog.svelte
---

# Client configuration values in .NET

Client configuration values are key-value settings you set on a project in Exceptionless. The .NET client downloads them
and keeps them up to date, so you can change how your app behaves without redeploying it. This page covers reading them
in .NET. For what they are and how to manage them, see [Projects](/docs/project-settings/#client-configuration).

## Add a value

In the app, open your project's settings, select **Configuration**, and click **Add Configuration Value**.

![Add New Configuration Value dialog with the enableLogSubmission key and a value of true](/assets/img/docs/client-configuration-values-light.webp)

The client picks up the new value the next time it sends events or checks for updates.

## Read a value

Values are in `Configuration.Settings`, a dictionary of strings with typed helpers. Each helper takes the key and an
optional default, returned when the key is missing or can't be converted:

```csharp
using Exceptionless;

var settings = ExceptionlessClient.Default.Configuration.Settings;

bool includeOrderData = settings.GetBoolean("IncludeOrderData", false);
int maxItems = settings.GetInt32("MaxItemsToAttach", 10);
string region = settings.GetString("Region", "us-east");
```

| Helper                            | Returns                                                                   |
| --------------------------------- | ------------------------------------------------------------------------- |
| `GetString(key, default)`         | `string`                                                                  |
| `GetBoolean(key, default)`        | `bool`                                                                    |
| `GetInt32(key, default)`          | `int`                                                                     |
| `GetInt64(key, default)`          | `long`                                                                    |
| `GetDouble(key, default)`         | `double`                                                                  |
| `GetDateTime(key, default)`       | `DateTime`                                                                |
| `GetDateTimeOffset(key, default)` | `DateTimeOffset`                                                          |
| `GetGuid(key, default)`           | `Guid`                                                                    |
| `GetStringCollection(key)`        | The value split on commas, semicolons, and new lines, with spaces trimmed |

Keys are case-insensitive.

## Example: turn log submission on and off

This plugin drops log events while the `enableLogSubmission` value is `false`. Change the value in the app and the app's
behavior changes within minutes, with no deploy:

```csharp
using Exceptionless;
using Exceptionless.Models;

ExceptionlessClient.Default.Configuration.AddPlugin("conditionally-cancel-logs", 100, context => {
    bool enableLogSubmission = context.Client.Configuration.Settings.GetBoolean("enableLogSubmission", true);

    if (context.Event.Type == Event.KnownTypes.Log && !enableLogSubmission)
        context.Cancel = true;
});
```

The same pattern works for any switch, such as deciding whether to attach extra data:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs -->

```csharp
ExceptionlessClient.Default.Configuration.AddPlugin(context => {
    if (context.Client.Configuration.Settings.GetBoolean("IncludeConditionalData", true))
        context.Event.AddObject(new { Total = 32.34, ItemCount = 2 }, "ConditionalData");
});
```

See [Plugins](/docs/clients/dotnet/plugins/) for more.

## Set local defaults

Give a value a default in your app, used until the project's value arrives. Values from the project replace local ones
with the same key.

In `appsettings.json`:

```json
{
  "Exceptionless": {
    "Settings": {
      "IncludeOrderData": "true"
    }
  }
}
```

Or in code, before the client starts:

```csharp
ExceptionlessClient.Default.Configuration.Settings["IncludeOrderData"] = "true";
```

On .NET Framework you can also use the `<settings>` element of the config section or the `ExceptionlessSetting` assembly
attribute (see [Legacy ASP.NET](/docs/clients/dotnet/legacy-aspnet/#the-exceptionless-config-section)).

## React to changes

`Settings.Changed` fires when a value is added, updated, or removed:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleConsole/Program.cs -->

```csharp
ExceptionlessClient.Default.Configuration.Settings.Changed += (sender, args) => {
    Console.WriteLine($"{args.Item.Key} was {args.Action}: {args.Item.Value}");
};
```

`args.Action` is `Add`, `Update`, `Remove`, or `Clear`.

## How values are kept up to date {#updating-client-configuration-settings}

- **When the client sends events**, the response includes the project's configuration version. If it's newer than the
  client's, the client downloads the values right away.
- **While no events are sent**, the client checks for a newer version 5 seconds after it starts and then every 2
  minutes, once `Startup()` has run. Change the interval with `UpdateSettingsWhenIdleInterval`. The minimum is 2
  minutes, and `TimeSpan.Zero` turns idle checks off.
- **Values are saved** in the client's storage and applied the next time `Startup()` runs, before new ones are
  downloaded. With the default in-memory storage, they last until the process exits.
- **Only the version number is sent** when checking. If nothing changed, nothing is downloaded. Checks aren't events and
  don't count toward your plan.

To fetch the latest values on demand:

```csharp
using Exceptionless.Configuration;

await SettingsManager.UpdateSettingsAsync(ExceptionlessClient.Default.Configuration);
```

## Built-in keys: turn events off without code {#turn-events-off-without-code}

The client acts on some keys itself:

| Key                        | Value                             | Effect                                                                                                                                                                 |
| -------------------------- | --------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@@log:<source>`           | `Trace` through `Fatal`, or `Off` | The minimum level for log events from that source. `@@log:*` applies to every source. Set these from [Logs and log levels](/docs/setting-log-levels/).                 |
| `@@<type>:<source>`        | `false`                           | Drops events of that type from matching sources, for example `@@usage:Export*` or `@@404:*.php`.                                                                       |
| `@@error:<exception type>` | `false`                           | Drops errors whose exception, or any inner exception, has that full type name, for example `@@error:System.OperationCanceledException`.                                |
| `@@DataExclusions`         | Comma-separated patterns          | Removes matching fields from events. Set it from the project's data exclusions (see [Private information](/docs/clients/dotnet/private-information/#data-exclusions)). |
| `@@UserAgentBotPatterns`   | Comma-separated patterns          | Ignores web requests from matching user agents.                                                                                                                        |
| `TraceLogLimit`            | A number                          | Attaches that many recent `System.Diagnostics.Trace` messages to each event.                                                                                           |

Sources and exception types accept `*` as a wildcard at the start or end. When several patterns match, the longest one
wins.

## Ask Exie

```prompt exie
Which log sources sent the most events this week? I want to decide what to turn down.
```

```prompt exie
How many OperationCanceledException errors did this project get in the last 7 days?
```
