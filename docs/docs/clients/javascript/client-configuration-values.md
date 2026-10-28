---
title: "Client configuration values in JavaScript"
navTitle: "Client configuration values"
description: "Read project configuration values in the Exceptionless JavaScript client to change log levels, drop events, or toggle behavior without redeploying."
order: 4
sources:
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/SettingsManager.ts
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/submission/DefaultSubmissionClient.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/Utils.ts
  - Exceptionless.JavaScript:packages/core/test/DocumentationExamples.test.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/configuration-values/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/dialogs/add-project-config-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/settings/+page.svelte
---

# Client configuration values in JavaScript

Configuration values are key and value pairs you set on a project in Exceptionless. The JavaScript client downloads them
and keeps them up to date, so you can change what your app sends, or how it behaves, without a redeploy. For what they
are and where they come from, see [Client configuration](/docs/project-settings/#client-configuration).

## Add a configuration value

1. Open your project's settings and select **Configuration**.
2. Select **Add Configuration Value**.
3. Enter a **Key** and a **Value**, and select **Add Configuration Value**.

![Add New Configuration Value dialog with the enableLogSubmission key and a value of true](/assets/img/docs/client-configuration-values-light.webp)

Clients pick up the change the next time they check, within a few minutes. See
[How values sync](#updating-client-configuration-settings).

## Read a value in your app

Values are strings in `Exceptionless.config.settings`:

```js
import { Exceptionless } from "@exceptionless/browser"

const showNewCheckout = Exceptionless.config.settings["showNewCheckout"] === "true"
```

To react when values change, subscribe during `startup`:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.subscribeServerSettingsChange((config) => {
    console.log("New configuration values", config.settings)
  })
})
```

## Turn events off with a plugin

A [plugin](/docs/clients/javascript/client-configuration/#plugins) can read a value and cancel events. This one drops
log events while `enableLogSubmission` is `false`:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.addPlugin("CancelLogsWhenDisabled", 100, (context) => {
    const enabled = context.client.config.settings["enableLogSubmission"]

    if (context.event.type === "log" && enabled === "false") {
      context.cancelled = true
    }

    return Promise.resolve()
  })
})
```

Add `enableLogSubmission` with the value `false` to stop log events, and change it to `true` to turn them back on.

## Built-in keys {#built-in-keys}

The client acts on these keys without any code. Keys start with `@@`.

| Key                      | Example                           | Effect                                                                                                                                                                                                                             |
| ------------------------ | --------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `@@log:<source>`         | `@@log:checkout` = `warn`         | The minimum level for log events from that source. Use `trace`, `debug`, `info`, `warn`, `error`, `fatal`, or `off`. Logs sent without a level are never dropped. [Log levels](/docs/setting-log-levels/) sets these keys for you. |
| `@@error:<type>`         | `@@error:TypeError` = `false`     | Drops errors of that type, including when it's an inner error.                                                                                                                                                                     |
| `@@usage:<feature>`      | `@@usage:Export to CSV` = `false` | Drops feature usage events for that feature.                                                                                                                                                                                       |
| `@@404:<path>`           | `@@404:/healthz` = `false`        | Drops 404 events for that path.                                                                                                                                                                                                    |
| `@@<type>:<source>`      | `@@racecar:Fuel System` = `false` | Drops events of any other type from that source.                                                                                                                                                                                   |
| `@@DataExclusions`       | Set by **Data Exclusions**        | Adds [data exclusions](/docs/clients/javascript/client-configuration/#data-exclusions). Set it in your project's settings, not here.                                                                                               |
| `@@UserAgentBotPatterns` | Set by **Spam Detection**         | Drops events from requests whose user agent matches. Set it in your project's settings, not here.                                                                                                                                  |

The part after the colon can use `*` at the start or end, such as `@@log:app.*` or `@@log:*`. Matching ignores case.
When several keys match, the longest key wins. For the `@@error`, `@@usage`, `@@404`, and other type keys, any value
other than `true`, `yes`, or `1` drops the events.

## How values sync {#updating-client-configuration-settings}

- At startup, the client applies the values it saved last time.
- It checks the server 5 seconds after startup, or 15 seconds if it already had saved values, and then every 2 minutes.
- When a response to sent events reports a newer configuration version, it downloads the new values right away.
- It saves the values in its storage: `localStorage` in the browser, a `.exceptionless` folder in Node.js, and
  AsyncStorage in React Native.

A check sends the API key and the version the client already has, and no user or event data. When nothing changed, the
server sends no values back. Checks are not events and don't count toward your plan's limits.

To stop the periodic check, set `updateSettingsWhenIdleInterval` to `-1`. The client still updates when a response to
sent events reports a newer version:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.updateSettingsWhenIdleInterval = -1
})
```

To check right away, call `SettingsManager.updateSettings`:

```js
import { Exceptionless, SettingsManager } from "@exceptionless/browser"

await SettingsManager.updateSettings(Exceptionless.config)
```

## Ask Exie

Exie can't change configuration values, but it can show you what to turn down:

```prompt exie
Which log sources sent the most log events in the last 24 hours?
```

```prompt exie
Which 404 paths were reported most often this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the noisiest log sources and 404 paths in my Exceptionless project from the last 7 days and suggest @@log and @@404 configuration values to quiet them.
```
