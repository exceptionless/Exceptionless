---
title: "JavaScript client"
metaTitle: "JavaScript client for browsers and Node.js"
description: "Install the Exceptionless JavaScript client for browsers, Node.js, React, Vue, React Native, and Expo, from npm or a CDN, and capture unhandled errors."
sources:
  - Exceptionless.JavaScript:README.md
  - Exceptionless.JavaScript:packages/core/package.json
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/browser/package.json
  - Exceptionless.JavaScript:packages/browser/src/BrowserExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/node/package.json
  - Exceptionless.JavaScript:packages/react/package.json
  - Exceptionless.JavaScript:packages/react-native/package.json
  - Exceptionless.JavaScript:packages/vue/src/index.ts
  - Exceptionless.JavaScript:packages/angularjs/README.md
  - Exceptionless.JavaScript:.agents/skills/exceptionless-javascript/SKILL.md
---

# JavaScript client

The Exceptionless JavaScript client sends errors, logs, feature usage, and 404s from browsers, Node.js, and React Native
apps. After you call `startup`, it reports unhandled errors and unhandled promise rejections automatically.

## Install with your coding agent

Paste this into Claude Code, Codex, Copilot, or another coding agent from the root of your app:

```prompt agent
Add Exceptionless error monitoring to this JavaScript app. Read https://exceptionless.com/docs/clients/javascript.md, pick the framework guide that matches this app, and follow it. The agent skill at https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/.agents/skills/exceptionless-javascript has source-checked setup notes for every package. Read the API key from configuration or an environment variable instead of hard-coding it, and add a way for me to throw a test error so I can confirm it shows up in Exceptionless.
```

The agent skill lives in the
[Exceptionless.JavaScript repository](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/.agents/skills/exceptionless-javascript).
See [Coding agents](/docs/ai/coding-agents/) for adding it to your agent.

## Packages

Install the package for your platform. Each one includes everything in `@exceptionless/core` and exports a ready-to-use
`Exceptionless` client.

| Package                       | Use it for                                                    | What it adds                                                                                                      |
| ----------------------------- | ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `@exceptionless/browser`      | Browser apps, the CDN, Angular, and the SvelteKit client side | Global `error` and `unhandledrejection` handlers, page request details, and stored settings in `localStorage`.    |
| `@exceptionless/node`         | Node.js 18 and later: servers, scripts, Express, serverless   | `uncaughtException` and `unhandledRejection` handlers, machine details, request details, and a flush on exit.     |
| `@exceptionless/react`        | React 18 and later in the browser                             | Everything in `@exceptionless/browser`, plus `ExceptionlessErrorBoundary`.                                        |
| `@exceptionless/vue`          | Vue apps                                                      | Everything in `@exceptionless/browser`, plus `ExceptionlessErrorHandler`.                                         |
| `@exceptionless/react-native` | React Native 0.71 and later, and Expo                         | Global JavaScript error capture, an error boundary, an AsyncStorage queue, and native iOS crash reporting.        |
| `@exceptionless/angularjs`    | AngularJS 1.x apps                                            | An `exceptionless` module with `$ExceptionlessClient`, an `$exceptionHandler` decorator, and an HTTP interceptor. |
| `@exceptionless/core`         | Other JavaScript runtimes and custom clients                  | The client, event builder, plugins, queue, and configuration that the other packages build on.                    |

For AngularJS, see the
[package README](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/packages/angularjs). For modern
Angular, use `@exceptionless/browser` as shown in the [Angular guide](/docs/clients/javascript/guides/angular/).

## Requirements

- **ES modules only.** Every package is an ES module (`"type": "module"`) with no CommonJS build. Load it with `import`.
  In a CommonJS file, use a dynamic `import("@exceptionless/node")`.
- **ES2022.** The packages and CDN bundles are compiled for ES2022, so they need a browser or runtime that supports
  ES2022.
- **Node.js 18 or later** for `@exceptionless/node`.
- **React 18 or later** for `@exceptionless/react` and `@exceptionless/react-native`.
- **React Native 0.71 or later** and **iOS 14 or later** for native iOS crash reporting.

## Quick start

Install the browser package:

```bash
npm install @exceptionless/browser
```

Start the client once when your app loads. `startup` takes your project's API key, which you can copy from the project's
**API Keys** page in Exceptionless:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup("YOUR_API_KEY")

try {
  throw new Error("Exceptionless test error")
} catch (error) {
  await Exceptionless.submitException(error)
}
```

The client sends queued events every 10 seconds, so the error shows up on the **Events** page shortly after. Unhandled
errors are reported without a `try`/`catch`.

To set more than the API key, pass a callback:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3"
  c.defaultTags.push("web")
})
```

Every setting is listed on the [configuration reference](/docs/clients/javascript/client-configuration/).

> [!NOTE]
> If you self-host Exceptionless, also set `c.serverUrl` to your instance's URL, for example
> `c.serverUrl = "https://exceptionless.example.com"`.

## Load it from a CDN

Without a bundler, import the browser package from a CDN in a module script. Both unpkg and jsDelivr serve the minified
ES2022 bundle:

```html
<script type="module">
import { Exceptionless } from "https://unpkg.com/@exceptionless/browser@3"

await Exceptionless.startup("YOUR_API_KEY")
</script>
```

`https://cdn.jsdelivr.net/npm/@exceptionless/browser@3` works the same way. Pin at least the major version so a future
release can't change your app without notice. The [browser guide](/docs/clients/javascript/guides/browser/) has the full
setup.

## Framework guides

- [Browser](/docs/clients/javascript/guides/browser/): npm or CDN in any web page.
- [Node.js and serverless](/docs/clients/javascript/guides/node/): servers, scripts, workers, and functions.
- [Express](/docs/clients/javascript/guides/express/): error and 404 middleware with request details.
- [React](/docs/clients/javascript/guides/react/): `ExceptionlessErrorBoundary` with a fallback UI.
- [React Native and Expo](/docs/clients/javascript/guides/react-native/): JavaScript errors and native iOS crashes.
- [Next.js](/docs/clients/javascript/guides/nextjs/): App Router, `instrumentation.js`, and `onRequestError`.
- [SvelteKit](/docs/clients/javascript/guides/sveltekit/): `handleError` hooks on the client and server.
- [Vue](/docs/clients/javascript/guides/vue/): the global Vue error handler.
- [Angular](/docs/clients/javascript/guides/angular/): an `ErrorHandler` provider for standalone apps.

## Readable production stack traces

Minified bundles produce stack traces with short names and one-line files. Exceptionless finds public source maps
automatically and accepts private ones from your CI/CD pipeline. See
[Source maps](/docs/clients/javascript/source-maps/).

## Next steps

- [Send logs, feature usage, and custom events](/docs/clients/javascript/sending-events/).
- [Change client behavior from Exceptionless](/docs/clients/javascript/client-configuration-values/) without
  redeploying.
- [Track user sessions](/docs/user-sessions/).
- [Fix events that don't arrive](/docs/clients/javascript/troubleshooting/).
