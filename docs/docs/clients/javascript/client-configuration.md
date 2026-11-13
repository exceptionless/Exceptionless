---
title: "JavaScript client configuration"
navTitle: "Configuration"
metaTitle: "JavaScript client configuration reference"
description: "Every Exceptionless JavaScript client setting: API key, server URL, version, default tags and data, privacy flags, data exclusions, queue, plugins, and logging."
order: 2
sources:
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/queue/DefaultEventQueue.ts
  - Exceptionless.JavaScript:packages/core/src/submission/DefaultSubmissionClient.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/EventPluginManager.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/IEventPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/ConfigurationDefaultsPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/Utils.ts
  - Exceptionless.JavaScript:packages/core/test/DocumentationExamples.test.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserRequestInfoPlugin.ts
  - Exceptionless.JavaScript:packages/node/src/NodeExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeEnvironmentInfoPlugin.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeRequestInfoPlugin.ts
  - Exceptionless.JavaScript:packages/react-native/src/ReactNativeExceptionlessClient.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/settings/+page.svelte
---

# JavaScript client configuration

Configure the client in the callback you pass to `startup`. Only `apiKey` is required. The settings and methods below
are the same in every package: browser, Node.js, React, Vue, React Native, and AngularJS.

## Set configuration

Pass the API key on its own:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup("YOUR_API_KEY")
```

Or pass a callback to set more:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3"
  c.defaultTags.push("checkout")
  c.setUserIdentity("12345678", "Blake")
})
```

The callback runs only the first time you call `startup`. After that, read or change settings on `Exceptionless.config`.
Calling `startup()` again with no arguments resumes a client that was suspended.

## Settings

| Setting                          | Default                            | Description                                                                                                                                                             |
| -------------------------------- | ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `apiKey`                         | `""`                               | Your project's API key. The client doesn't send events until the key is at least 10 characters long.                                                                    |
| `serverUrl`                      | The hosted Exceptionless service   | Where events are sent. Setting it also sets `configServerUrl` and `heartbeatServerUrl`, so set those after it if you need them to differ.                               |
| `heartbeatServerUrl`             | The hosted service, or `serverUrl` | Where [session](/docs/user-sessions/) heartbeats are sent.                                                                                                              |
| `configServerUrl`                | The hosted service, or `serverUrl` | Kept for compatibility. In 3.2.1, the client gets [configuration values](/docs/clients/javascript/client-configuration-values/) from `serverUrl`.                       |
| `version`                        | Not set                            | Your app's version, sent with every event. It enables [regression detection](/docs/versioning/).                                                                        |
| `defaultTags`                    | `[]`                               | Tags added to every event.                                                                                                                                              |
| `defaultData`                    | `{}`                               | Extended data added to every event, unless the event already has a value with the same key. Data exclusions apply.                                                      |
| `enabled`                        | `true`                             | When `false`, the client discards events and stops checking for configuration values.                                                                                   |
| `submissionBatchSize`            | `50`                               | The most events sent in one request. The client lowers it on its own if the server rejects a batch as too large.                                                        |
| `updateSettingsWhenIdleInterval` | `120000`                           | How often, in milliseconds, the client checks for new configuration values. Values below `120000` are raised to `120000`. `0` or a negative value turns the check off.  |
| `usePersistedQueueStorage`       | `false` (`true` in React Native)   | Also store queued events in storage, so unsent events survive a reload or restart. See [Queue and storage](#offline-storage).                                           |
| `includePrivateInformation`      | `true`                             | Turns all the `include*` settings on or off at once. See [Private information](#general-data-protection-regulation).                                                    |
| `settings`                       | `{}`                               | The [configuration values](/docs/clients/javascript/client-configuration-values/) synced from your project. Read them; the client replaces them when new values arrive. |
| `services`                       | Platform defaults                  | The client's log, storage, queue, submission client, and last reference ID manager. Replace one to customize the client, for example `c.services.log`.                  |

## Methods

| Method                                                                                          | What it does                                                                                                                                                                                                                             |
| ----------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `setUserIdentity(identity, name?)`                                                              | Adds the user to every event. Also accepts `{ identity, name, data }`. Call it with an empty identity and name to remove the user. See [Sessions](/docs/user-sessions/).                                                                 |
| `useSessions(sendHeartbeats = true, heartbeatInterval = 60000, useSessionIdManagement = false)` | Sends session start events and heartbeats. Intervals below 30000 ms become 60000 ms. Pass `true` as the third argument to have the client create session IDs instead of using the user's identity. See [Sessions](/docs/user-sessions/). |
| `addDataExclusions(...patterns)`                                                                | Adds [data exclusion](#data-exclusions) patterns.                                                                                                                                                                                        |
| `addUserAgentBotPatterns(...patterns)`                                                          | Drops events from requests whose user agent matches a pattern, such as `*bot*`. Used together with your project's **Spam Detection** list.                                                                                               |
| `addPlugin(plugin)` or `addPlugin(name, priority, run)`                                         | Adds a [plugin](#plugins).                                                                                                                                                                                                               |
| `removePlugin(pluginOrName)`                                                                    | Removes a plugin by name.                                                                                                                                                                                                                |
| `useDebugLogger()`                                                                              | Writes the client's diagnostic messages to the console. See [Troubleshooting](/docs/clients/javascript/troubleshooting/).                                                                                                                |
| `useLocalStorage()`                                                                             | Stores settings and, with `usePersistedQueueStorage`, queued events in `localStorage`. The browser and Node.js packages do this for you.                                                                                                 |
| `subscribeServerSettingsChange(handler)`                                                        | Calls `handler(config)` whenever new configuration values arrive.                                                                                                                                                                        |

## Private information {#general-data-protection-regulation}

The client sends the data it can collect unless you turn it off. To meet the GDPR or your own privacy rules, remove what
you don't need before it leaves the app.

Turn off all of it at once:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.includePrivateInformation = false
})
```

Or turn off individual items. Set `includePrivateInformation` first, if you use it, because it overwrites the others:

| Setting              | Default | What it controls                                                                                            |
| -------------------- | ------- | ----------------------------------------------------------------------------------------------------------- |
| `includeMachineName` | `true`  | Node.js: the machine's host name.                                                                           |
| `includeIpAddress`   | `true`  | Node.js: the machine's IP addresses and the client IP of a request.                                         |
| `includeHeaders`     | `true`  | Node.js: request headers.                                                                                   |
| `includeCookies`     | `true`  | Browser: the page's cookies. Node.js: request cookies.                                                      |
| `includePostData`    | `true`  | Node.js: the request body.                                                                                  |
| `includeQueryString` | `true`  | Browser: the page's query string. Node.js: the request's route parameters.                                  |
| `includeUserName`    | `true`  | Kept for parity with other clients. The 3.2.1 packages don't read it; a user is only sent when you set one. |

A user is attached to events only when you call `setUserIdentity`, on the configuration or on a single event. Don't pass
names or email addresses you don't need.

> [!NOTE]
> The project setting **Automatically remove user identifiable information from events** is applied by the server when
> it processes events, not by the JavaScript client. The client still sends the data, and the server removes the user,
> email address, IP addresses, machine name, and request cookies, query strings, and bodies before storing the event. To
> keep that data from leaving your app, set `includePrivateInformation = false` in code.

### Data exclusions {#data-exclusions}

Data exclusions remove values by key name before an event is sent. They apply to extended data you add with
`setProperty`, `defaultData`, the properties of error objects, and request cookies, headers, query strings, and bodies.
Matching ignores case, and `*` at the start or end of a pattern matches any prefix or suffix:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.addDataExclusions("authorization", "cookie", "set-cookie", "*password*", "*secret*", "*token*")
})
```

The client also applies the **Data Exclusions** list from your project's settings, which it receives as a
[configuration value](/docs/clients/javascript/client-configuration-values/). See [Security](/docs/security/) for how
exclusions work across Exceptionless.

## Queue and storage {#offline-storage}

Submitted events go into a queue that the client sends every 10 seconds, up to `submissionBatchSize` events at a time.
The queue holds up to 250 events and drops the oldest when it's full. If the server is unavailable or rate limits the
client, the client waits and tries again later.

By default the queue lives in memory, so events still in it are lost if the page closes or the process exits. Set
`usePersistedQueueStorage` to store them as well:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.usePersistedQueueStorage = true
})
```

| Package                       | Where settings and the persisted queue are stored                                            |
| ----------------------------- | -------------------------------------------------------------------------------------------- |
| `@exceptionless/browser`      | `localStorage`                                                                               |
| `@exceptionless/node`         | A `.exceptionless` folder in the current working directory, or memory if it can't be created |
| `@exceptionless/react-native` | AsyncStorage. The persisted queue is on by default.                                          |
| `@exceptionless/core`         | Memory, unless you call `useLocalStorage()`                                                  |

To send the queue right away, call `await Exceptionless.processQueue()`. See
[short-lived processes](/docs/clients/javascript/guides/node/#short-lived-processes-and-serverless).

## Plugins {#plugins}

A plugin runs for every event before it's queued. Use one to add data, change events, or cancel them. Plugins run in
order of `priority`, lowest first. Setting `context.cancelled = true` stops the event. If a plugin throws, the client
cancels the event rather than send it half processed.

Add a plugin with a name, a priority, and a function:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.addPlugin("IgnoreHealthChecks", 80, (context) => {
    if (context.event.type === "404" && context.event.source === "/healthz") {
      context.cancelled = true
    }

    return Promise.resolve()
  })
})
```

Or add an object with `name`, `priority`, and `run`. It can also have `startup` and `suspend` functions that run when
the client starts and suspends:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.addPlugin({
    name: "DeploymentPlugin",
    priority: 90,
    run(context) {
      context.event.data ??= {}
      context.event.data.deployment = "blue"
      return Promise.resolve()
    },
  })
})
```

In a plugin, `context.event` is the event, `context.client` is the client, and `context.eventContext` holds values
passed with `setContextProperty`. Built-in plugins use priorities from 1 to 1010, and the duplicate checker runs last at
1010.

## Self-hosted options {#self-hosted-options}

To send events to your own Exceptionless instance, set `serverUrl`:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.serverUrl = "https://exceptionless.example.com"
})
```

`serverUrl` also sets `heartbeatServerUrl` and `configServerUrl`. If heartbeats go to a different host, set
`heartbeatServerUrl` after `serverUrl`. See [Self-hosting](/docs/self-hosting/) to run Exceptionless.
