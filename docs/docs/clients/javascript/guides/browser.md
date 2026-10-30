---
title: "Browser"
metaTitle: "Exceptionless error tracking for browser JavaScript"
description: "Add Exceptionless to any web page with npm or a CDN script tag, capture unhandled errors and promise rejections, and confirm a test error arrives."
order: 1
redirectFrom:
  - /docs/clients/javascript/javascript-example/
sources:
  - Exceptionless.JavaScript:packages/browser/README.md
  - Exceptionless.JavaScript:packages/browser/package.json
  - Exceptionless.JavaScript:packages/browser/src/BrowserExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserLifeCyclePlugin.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserRequestInfoPlugin.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserModuleInfoPlugin.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserIgnoreExtensionErrorsPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/DuplicateCheckerPlugin.ts
  - Exceptionless.JavaScript:example/browser/index.js
---

# Browser

`@exceptionless/browser` reports unhandled errors and unhandled promise rejections from any web page, with the page URL,
browser, and referrer attached. Use it for plain JavaScript, for bundler-based apps without a dedicated package, and as
the base for the Angular and SvelteKit guides.

It needs a browser that supports ES modules and ES2022.

## Install

With a bundler such as Vite, webpack, or esbuild:

```bash
npm install @exceptionless/browser
```

Without a bundler, skip the install and import the package from a CDN, as shown in the next section.

## Configure

Call `startup` once, as early as possible when the page loads. Errors that happen before `startup` runs are not
captured.

**With npm**, in your app's entry module:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3" // optional: your app's version, used for regressions
})
```

**With a CDN**, in a module script near the top of the page:

```html
<script type="module">
import { Exceptionless } from "https://unpkg.com/@exceptionless/browser@3"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
})
</script>
```

The CDN URL serves the package's minified ES2022 bundle. `https://cdn.jsdelivr.net/npm/@exceptionless/browser@3` also
works. Pin the version you tested with, for example `@exceptionless/browser@3.2.1`, if you want no updates at all.

Once `startup` has run, import `Exceptionless` from the same package anywhere in your app to send events:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.submitFeatureUsage("Export to CSV")
```

The API key is visible to anyone who loads your page. That's expected: client API keys are made to ship in apps, and
they can submit events but can't read them. See the
[configuration reference](/docs/clients/javascript/client-configuration/) for every setting, including privacy options.

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`.

## Verify

Add a button that throws an error nobody catches:

```html
<button id="exceptionless-test">Throw a test error</button>

<script type="module">
document.querySelector("#exceptionless-test").addEventListener("click", () => {
  throw new Error("Exceptionless test error")
})
</script>
```

Click it, then open **Events** in Exceptionless. The client sends its queue every 10 seconds, so the error appears
shortly after. The stack trace is on the event's **Exception** tab, and the browser and page URL are on its **Request**
tab.

If nothing arrives, turn on `c.useDebugLogger()` and read the browser console. See
[Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically {#what-is-captured-automatically}

- **Unhandled errors** from the window `error` event and **unhandled promise rejections** from `unhandledrejection`.
  Rejections with a value that isn't an `Error` are converted to one.
- **jQuery AJAX failures**, when jQuery is on the page: a 404 is sent as a not-found event and other failures except 401
  as errors.
- **Request details** on every event: the user agent, host, port, path, whether the page uses HTTPS, the referrer, and
  the page's cookies and query string. [Data exclusions](/docs/security/) remove matching cookie and query string keys,
  and `includeCookies` and `includeQueryString` turn them off.
- **Scripts on the page.** The `src` of each script tag is attached to errors as a module list.
- **Repeated errors.** When the same error repeats within 30 seconds, the client holds the repeats and sends them as one
  event with a count.

The client also:

- ignores errors whose stack trace comes from a browser extension (`chrome-extension://`, `moz-extension://`, and so
  on);
- drops events from user agents that match your project's **Spam Detection** list;
- sends the queue and pauses its timers when the page is hidden, and resumes when the page is visible again;
- keeps synced project settings in `localStorage`.

Events wait in memory until they are sent. To keep unsent events across page reloads, set
`c.usePersistedQueueStorage = true` to store the queue in `localStorage` as well.

## Next steps

- [Send logs, feature usage, and handled errors](/docs/clients/javascript/sending-events/).
- [Identify users and track sessions](/docs/user-sessions/) with `c.setUserIdentity(...)` and `c.useSessions()`.
- [Make minified stack traces readable](/docs/clients/javascript/source-maps/) with source maps.
- [Turn off logs or errors without redeploying](/docs/clients/javascript/client-configuration-values/).

## Ask Exie

```prompt exie
Which browsers reported the most errors in the last 7 days?
```

```prompt exie
What new error stacks showed up since yesterday's deploy?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most frequent JavaScript error in my Exceptionless project this week, open the latest event's stack trace, and fix the cause in this codebase.
```
