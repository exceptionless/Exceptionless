---
title: "Troubleshoot the JavaScript client"
navTitle: "Troubleshooting"
metaTitle: "Troubleshooting the Exceptionless JavaScript client"
description: "Find out why JavaScript events don't reach Exceptionless: turn on debug logging, read server responses, flush the queue, and check filters and privacy settings."
order: 6
sources:
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/queue/DefaultEventQueue.ts
  - Exceptionless.JavaScript:packages/core/src/submission/DefaultSubmissionClient.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/DuplicateCheckerPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/logging/ILog.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserIgnoreExtensionErrorsPlugin.ts
  - Exceptionless.JavaScript:packages/browser/package.json
  - Exceptionless.JavaScript:.agents/skills/exceptionless-javascript/references/troubleshooting.md
---

# Troubleshoot the JavaScript client

The client doesn't throw when it can't send events, so a bad API key or an unreachable server fails quietly. Start with
debug logging, which shows every step from queuing an event to the server's response.

## Turn on debug logging

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.useDebugLogger()
})
```

The client writes to the console: the browser's developer tools, your Node.js output, or the Metro logs in React Native.
When events flow, you see lines such as `Enqueued event`, `Sending 1 events to ...`, and `Sent 1 events`.

To send the client's messages somewhere else, set `c.services.log` to an object with `trace`, `info`, `warn`, and
`error` methods. Turn debug logging off again in production.

## Nothing is sent

Check these in order:

1. **`startup` ran.** Events submitted before `startup`, or from a different copy of the package, aren't sent. Import
   `Exceptionless` from the same package everywhere.
2. **The API key is right.** The client sends nothing while `apiKey` is shorter than 10 characters, and logs
   `Exceptionless is not configured and will not process events.` Copy the key from your project's **API Keys** page,
   and make sure it isn't disabled.
3. **The client is enabled.** `c.enabled = false` discards every event.
4. **The process lived long enough.** Events wait in a queue that's sent every 10 seconds. A script, serverless
   function, or page that ends first loses them. Call `await Exceptionless.processQueue()` before it ends. See
   [Node.js and serverless](/docs/clients/javascript/guides/node/#short-lived-processes-and-serverless).
5. **The request isn't blocked.** In the browser, look for a failed request in the network tab. If your site has a
   Content Security Policy, its `connect-src` must allow your Exceptionless server. For the hosted service, set
   `c.serverUrl = "https://api.exceptionless.io"` and allow that origin. Self-hosted servers must also allow your site's
   origin for cross-origin requests.

## The client stopped sending

The client pauses when the server turns events away. The debug log shows which case you hit:

| Server response                              | Log message                                                     | What the client does                                                                                       |
| -------------------------------------------- | --------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `401` or `403`                               | `Unable to authenticate, please check your configuration.`      | Discards the batch and pauses for 15 minutes. Check the API key.                                           |
| `402`                                        | `Too many events have been submitted, please upgrade your plan` | Your organization is over its plan's limit. Discards the queue and new events until the next quarter hour. |
| `429` or `503`                               | `Server returned service unavailable`                           | Keeps the queue and pauses until the next quarter hour.                                                    |
| `400` or `404`                               | `Error while trying to submit data: ...`                        | Discards the batch and pauses for 4 hours. A `404` usually means a wrong `serverUrl`.                      |
| `413`                                        | `Event submission discarded for being too large.`               | Retries with smaller batches, and discards an event that's too large on its own.                           |
| No response, such as a network or CORS error | `Error processing queue: ...`                                   | Keeps the queue and pauses until the next quarter hour.                                                    |
| Any other error status                       | `Error submitting events: ...`                                  | Keeps the queue and pauses until the next quarter hour.                                                    |

Reloading the page or restarting the process clears a pause.

## Some events are missing

The client drops events on purpose in these cases:

- **A configuration value turns them off**, such as a `@@log` minimum level or `@@error:<type>` set to `false`. See
  [built-in keys](/docs/clients/javascript/client-configuration-values/#built-in-keys).
- **A plugin cancelled them**, by setting `context.cancelled = true` or by throwing.
- **The user agent matches a bot pattern** from your project's **Spam Detection** list or `addUserAgentBotPatterns`.
- **The error came from a browser extension.** The browser client ignores errors whose stack trace has an
  `-extension://` URL.
- **The error repeated within 30 seconds.** Repeats are held and sent later as one event with a count.

`submit()` returns the plugin context, and `context.cancelled` is `true` when the client dropped the event:

```js
import { Exceptionless } from "@exceptionless/browser"

const context = await Exceptionless.submitLog("checkout", "Order placed", "info")
console.log("Dropped:", context.cancelled)
```

## Some data is missing

If cookies, headers, query strings, request bodies, IP addresses, or extended data values are missing, check the
`include*` settings and your data exclusions, both in code and in your project's **Data Exclusions** setting. See
[Private information](/docs/clients/javascript/client-configuration/#general-data-protection-regulation).

## Stack traces are minified

Production bundles need source maps for readable stack traces. See [Source maps](/docs/clients/javascript/source-maps/).
For Node.js, run with `--enable-source-maps`.

## Update the client

Make sure you have the current release, and that every `@exceptionless/*` package you use is the same version:

```bash
npm install @exceptionless/browser@latest
```

To step through the client's code in the browser, load the unminified bundle from the CDN, which ships with a source
map:

```html
<script type="module">
import { Exceptionless } from "https://unpkg.com/@exceptionless/browser@3/dist/index.bundle.js"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.useDebugLogger()
})
</script>
```

If you're still stuck, [open an issue](https://github.com/exceptionless/Exceptionless.JavaScript/issues/new) with the
debug log.

## Ask Exie

```prompt exie
Did this project receive any events in the last 15 minutes?
```

```prompt exie
When did each of my projects last receive an event?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can check while it works:

```prompt agent
I just added Exceptionless to this app. Throw a test error, run the app, and then check my Exceptionless project for the event. If it doesn't arrive, turn on useDebugLogger, read the client's output, and fix the setup.
```
