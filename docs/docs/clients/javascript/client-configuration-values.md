---
title: "Client Configuration Values"
order: 2
---

# Client Configuration Values

- [About](#about)
- [Usage Example](#usage-example)
- [Updating Client Configuration settings](#updating-client-configuration-settings)
- [Subscribing to Client Configuration Setting changes](#subscribing-to-client-configuration-setting-changes)

## About

[Read about client configuration and view in-depth examples](/docs/project-settings)

## Usage Example

The below example demonstrates **how we would turn on or off log event submissions at runtime** without redeploying the app or changing server config settings.

First, we add a (completely arbitrary for this example) `enableLogSubmission` client configuration value key with value `true` in the Project's Settings in the Exceptionless dashboard.

![Exceptionless Client Configuration Value](/assets/img/docs/client-configuration.png)

Then, we register a new client side plugin that runs each time an event is created. If our key (`enableLogSubmission`) is set to false and the event type is set to log, we will discard the event.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
   c.addPlugin('Conditionally cancel log submission', 100, (context) => {
      var enableLogSubmission = context.client.config.settings['enableLogSubmission'];

      // only cancel event submission if it’s a log event and
      // enableLogSubmission is set to a value and the value is not true.
      if (context.event.type === 'log' && (!!enableLogSubmission && enableLogSubmission !== 'true')) {
         context.cancelled = true;
      }
   });
});
```

***

## Updating Client Configuration settings

![Exceptionless Client Configuration Settings](/assets/img/docs/client-configuration.png)

All project settings are synced to the client in almost real time. When an event is submitted to Exceptionless we send down a response header with the current configuration version. If a newer version is available we will immediately retrieve and apply the latest configuration.

By default the client will check after `5 seconds` on client startup (*if no events are submitted on startup*) and then every `2 minutes` after the last event submission for updated configuration settings.

- Checking for updated settings doesn't count towards plan limits.
- Only the current configuration version is sent when checking for updated settings (no user information will ever be sent).
- If the settings haven't changed, then no settings will be retrieved.

You can also **turn off the automatic updating of configuration settings when idle** using the code below.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
   c.updateSettingsWhenIdleInterval = -1;
});
```

You can also manually update the configuration settings using the code below.

```js
import { Exceptionless, SettingsManager } from "@exceptionless/browser";

await SettingsManager.updateSettings(Exceptionless.config);
```

## Subscribing to Client Configuration Setting changes

To be notified when client configuration settings change, subscribe to them using the below code.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.startup(c => {
   c.subscribeServerSettingsChange((configuration) => {
      // configuration.settings contains the new settings
   });
});
```

***

[Next > Sending Events](/docs/clients/javascript/sending-events)
