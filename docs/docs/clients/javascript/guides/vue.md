---
title: "Vue"
metaTitle: "Exceptionless error tracking for Vue"
description: "Install @exceptionless/vue, start the Exceptionless client, and set ExceptionlessErrorHandler as Vue's global error handler to report component errors."
order: 8
redirectFrom:
  - /docs/clients/javascript/vue-example/
sources:
  - Exceptionless.JavaScript:packages/vue/README.md
  - Exceptionless.JavaScript:packages/vue/src/index.ts
  - Exceptionless.JavaScript:example/vue/src/main.js
  - Exceptionless.JavaScript:example/vue/src/components/HelloWorld.vue
  - Exceptionless.JavaScript:packages/browser/src/BrowserExceptionlessClient.ts
---

# Vue

`@exceptionless/vue` reports errors from Vue components, and everything the
[browser client](/docs/clients/javascript/guides/browser/) reports: unhandled errors and promise rejections anywhere on
the page. `ExceptionlessErrorHandler` plugs into Vue's global `app.config.errorHandler`.

The example app uses Vue 3.

## Install

```bash
npm install @exceptionless/vue
```

## Configure

Start the client and set the error handler in your entry file, usually `src/main.js`:

```js
import { createApp } from "vue"
import { Exceptionless, ExceptionlessErrorHandler } from "@exceptionless/vue"

import App from "./App.vue"

Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3" // optional: your app's version, used for regressions
})

const app = createApp(App)
app.config.errorHandler = ExceptionlessErrorHandler
app.mount("#app")
```

`startup` returns a promise, but you don't need to wait for it before mounting. It sets the API key right away, so early
errors are queued and sent once the client finishes starting.

Vue calls `errorHandler` for errors in component rendering, lifecycle hooks, watchers, and event handlers bound in
templates. `ExceptionlessErrorHandler` submits the error with two extra properties: `vm`, the component instance, and
`info`, Vue's description of where the error happened.

Setting `app.config.errorHandler` replaces Vue's default handling, so Vue stops logging those errors to the console. To
keep the console output, wrap the handler:

```js
app.config.errorHandler = (err, instance, info) => {
  console.error(err)
  return ExceptionlessErrorHandler(err, instance, info)
}
```

Errors you catch yourself are not reported unless you submit them. Import `Exceptionless` from `@exceptionless/vue`
anywhere in your app:

```js
import { Exceptionless, toError } from "@exceptionless/vue"

export async function saveSettings(settings) {
  try {
    await fetch("/api/settings", { method: "PUT", body: JSON.stringify(settings) })
  } catch (error) {
    await Exceptionless.submitException(toError(error))
  }
}
```

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`.

## Verify

Add a button to a component that throws when clicked:

```html
<script setup>
function throwTestError() {
  throw new Error("Exceptionless test error")
}
</script>

<template>
  <button @click="throwTestError">Throw a test error</button>
</template>
```

Click it. Within about 10 seconds, the error appears on the **Events** page in Exceptionless, with Vue's `info` string
on its **Extended Data** tab. Remove the button when you're done.

If nothing arrives, add `c.useDebugLogger()` to `startup` and check the browser console. See
[Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically

- **Component errors** passed to Vue's `errorHandler`, with the component instance and Vue's `info` string.
- **Unhandled errors and promise rejections** that don't go through Vue, such as errors in timers and async code.
- Everything else the
  [browser client captures](/docs/clients/javascript/guides/browser/#what-is-captured-automatically), such as request
  details and ignoring errors from browser extensions.

## Next steps

- [Identify users and track sessions](/docs/user-sessions/) with `c.setUserIdentity(...)` and `c.useSessions()`.
- [Make minified stack traces readable](/docs/clients/javascript/source-maps/) for your production build.
- [Send feature usage and logs](/docs/clients/javascript/sending-events/) from components.

## Ask Exie

```prompt exie
What are the most frequent errors in the last 7 days, and are any of them new?
```

```prompt exie
Snooze this stack for a week.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most frequent new error in my Exceptionless project this week, read its latest event, fix the Vue component that throws it, and mark the stack fixed in the next version.
```
