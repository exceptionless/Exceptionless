---
title: "Client Configuration Values"
order: 3
---

# Client Configuration Values

## Index

- [Index](#index)
- [About](#about)
- [Usage Example](#usage-example)
- [.NET Helpers](#net-helpers)
  - [Helpers](#helpers)
- [Updating Client Configuration settings](#updating-client-configuration-settings)
- [Subscribing to Client Configuration Setting changes](#subscribing-to-client-configuration-setting-changes)

## About

[Read about client configuration and view in-depth examples](/docs/project-settings)

## Usage Example

The below example demonstrates **how we would turn on or off log event submissions at runtime** without redeploying the app or changing server config settings.

First, we add a (completely arbitrary for this example) `enableLogSubmission` client configuration value key with value `true` in the Project's Settings in the Exceptionless dashboard.

Then, we register a new client side plugin that runs each time an event is created. If our key (`enableLogSubmission`) is set to false and the event type is set to log, we will discard the event.

```csharp
ExceptionlessClient.Default.Configuration.AddPlugin("Conditionally cancel log submission", 100, context => {
    var enableLogSubmission = context.Client.Configuration.Settings.GetBoolean("enableLogSubmission", true);

    // only cancel event submission if it's a log event and enableLogSubmission is false
    if (context.Event.Type == Event.KnownTypes.Log && !enableLogSubmission) {
        context.Cancel = true;
    }
});
```

***

## .NET Helpers

The `GetBoolean` method checks the `enableLogSubmission` key. This helper method makes it easy to consume saved client configuration values. The first parameter defines the settings key (name). The second parameter is optional and allows you to set a default value if the key doesn’t exist in the settings or was unable to be converted to the proper type (e.g., a boolean).

We have a few helpers to convert string configuration values to different system types. These methods also contain overloads that allow you to specify default values.

### Helpers

- `GetString`
- `GetBoolean`
- `GetInt32`
- `GetInt64`
- `GetDouble`
- `GetDateTime`
- `GetDateTimeOffset`
- `GetGuid`
- `GetStringCollection` (breaks a comma delimited list into an IEnumerable of strings)

***

## Updating Client Configuration settings

All project settings are synced to the client in almost real time. When an event is submitted to Exceptionless we send down a response header with the current configuration version. If a newer version is available we will immediately retrieve and apply the latest configuration.

By default the client will check after `5 seconds` on client startup (*if no events are submitted on startup*) and then every `2 minutes` after the last event submission for updated configuration settings.

- Checking for updated settings doesn't count towards plan limits.
- Only the current configuration version is sent when checking for updated settings (no user information will ever be sent).
- If the settings haven't changed, then no settings will be retrieved.

You can also **turn off the automatic updating of configuration settings when idle** using the code below.

```csharp
ExceptionlessClient.Default.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.Zero;
```

You can also manually update the configuration settings using the code below.

```csharp
await Exceptionless.Configuration.SettingsManager.UpdateSettingsAsync(ExceptionlessClient.Default.Configuration);
```

## Subscribing to Client Configuration Setting changes

To be notified when client configuration settings change, subscribe to them using the below code.

```csharp
ExceptionlessClient.Default.Configuration.Settings.Changed += SettingsOnChanged;

private void SettingsOnChanged(object sender, ChangedEventArgs<KeyValuePair<string, string>> args) {
   Console.WriteLine("The key {0} was {1}", args.Item.Key, args.Action);
}
```

---

[Next > Platform Guides](/docs/clients/dotnet/guides/)
