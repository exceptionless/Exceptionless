---
title: "SvelteKit"
metaTitle: "Exceptionless error tracking for SvelteKit"
description: "Report unexpected SvelteKit errors from the browser and the server to Exceptionless with handleError hooks in hooks.client.js and hooks.server.js."
order: 7
sources:
  - Exceptionless.JavaScript:example/svelte-kit/README.md
  - Exceptionless.JavaScript:example/svelte-kit/package.json
  - Exceptionless.JavaScript:example/svelte-kit/src/hooks.client.js
  - Exceptionless.JavaScript:example/svelte-kit/src/hooks.server.js
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeRequestInfoPlugin.ts
---

# SvelteKit

SvelteKit runs your code in the browser and on the server, so it uses `@exceptionless/browser` in `src/hooks.client.js`
and `@exceptionless/node` in `src/hooks.server.js`. Each hook file starts a client and reports the unexpected errors
that SvelteKit passes to `handleError`. Both clients also report unhandled errors and promise rejections on their side.

This guide uses SvelteKit 3 and Svelte 5, which `npx sv create` sets up. For an existing SvelteKit 2 app, see
[SvelteKit 2](#sveltekit-2). The server hook needs a Node.js-based adapter, such as `adapter-node` or a Node.js
serverless runtime.

## Install

```bash
npm install @exceptionless/browser @exceptionless/node
```

Add your API key to `.env`:

```bash
PUBLIC_EXCEPTIONLESS_API_KEY=YOUR_API_KEY
EXCEPTIONLESS_API_KEY=YOUR_API_KEY
```

Then declare both variables in `src/env.js`. `public: true` makes the first one available to browser code:

```js
import { defineEnvVars } from "@sveltejs/kit/env"

export const variables = defineEnvVars({
  PUBLIC_EXCEPTIONLESS_API_KEY: { public: true },
  EXCEPTIONLESS_API_KEY: {},
})
```

## Configure

`src/hooks.client.js`:

```js
import { PUBLIC_EXCEPTIONLESS_API_KEY } from "$app/env/public"
import { Exceptionless, toError } from "@exceptionless/browser"

Exceptionless.startup((c) => {
  c.apiKey = PUBLIC_EXCEPTIONLESS_API_KEY
  c.defaultTags.push("client")
})

/** @type {import("@sveltejs/kit/hooks").HandleClientError} */
export async function handleError({ kind, error, event }) {
  // Only report unexpected errors, not error() calls or 404s.
  if (kind !== "unknown") {
    return
  }

  await Exceptionless.createException(toError(error)).setProperty("path", event.url.pathname).submit()
}
```

`src/hooks.server.js`:

```js
import { EXCEPTIONLESS_API_KEY } from "$app/env/private"
import { Exceptionless, toError } from "@exceptionless/node"

Exceptionless.startup((c) => {
  c.apiKey = EXCEPTIONLESS_API_KEY
  c.defaultTags.push("server")
})

/** @type {import("@sveltejs/kit/hooks").HandleServerError} */
export async function handleError({ kind, error, event }) {
  // Only report unexpected errors, not error() calls, 404s, or validation errors.
  if (kind !== "unknown") {
    return
  }

  await Exceptionless.createException(toError(error)).setProperty("path", event.url.pathname).submit()

  // Send now in case the platform stops the function after the response.
  await Exceptionless.processQueue()
}
```

SvelteKit passes every error to `handleError`, including errors you raise with its `error()` helper (`kind` is `"app"`)
and 404s for unknown routes (`kind` is `"framework"`). The `kind` check skips those and reports only errors your code
didn't expect. `toError` turns any thrown value into an `Error`. `startup` sets the API key right away, so the hooks
don't need to wait for it.

To report a caught error elsewhere, import `Exceptionless` from the package for that side and call
`Exceptionless.submitException(toError(error))`.

> [!NOTE]
> Self-hosted instances use your own URL: set `c.serverUrl` in both hooks.

### SvelteKit 2 {#sveltekit-2}

In SvelteKit 2, `handleError` receives `status` and `message` instead of `kind`, and errors from `error()` don't reach
it, but 404s for unknown routes do. Change the hooks like this:

- Import the hook types from `@sveltejs/kit` instead of `@sveltejs/kit/hooks`.
- Skip `src/env.js`. In `hooks.client.js`, import `PUBLIC_EXCEPTIONLESS_API_KEY` from `$env/static/public`. In
  `hooks.server.js`, import `env` from `$env/dynamic/private` and set `c.apiKey = env.EXCEPTIONLESS_API_KEY`.
- Replace the `kind` check with `if (status !== 500) return`, since SvelteKit 2 gives unexpected errors a status of
  `500`.

## Verify

Add a page that throws during server-side load, at `src/routes/exceptionless-test/+page.server.js`:

```js
export function load() {
  throw new Error("Exceptionless test error")
}
```

Run `npm run dev` and open `http://localhost:5173/exceptionless-test`. SvelteKit shows its error page, and the error
appears on the **Events** page in Exceptionless, tagged `server`, with the `path` in its extended data. Delete the route
when you're done.

To check the browser side, throw from a button's click handler in a component. That error arrives tagged `client`.

## What is captured automatically

- **In the browser**: unexpected errors from navigation and client-side `load` functions through `handleError`, plus
  everything the [browser client captures](/docs/clients/javascript/guides/browser/#what-is-captured-automatically),
  such as unhandled errors, promise rejections, and the page URL.
- **On the server**: unexpected errors from server `load` functions, form actions, and endpoints through `handleError`,
  plus uncaught exceptions, unhandled rejections, and machine details from the
  [Node.js client](/docs/clients/javascript/guides/node/#what-is-captured-automatically).

Server events don't include request details, because the Node.js client reads Express-style requests and SvelteKit
passes a standard `Request`. The hooks add the `path` from the `event` argument of `handleError`. Add more with
`setProperty` in the same way.

## Next steps

- [Make minified browser stack traces readable](/docs/clients/javascript/source-maps/) with source maps.
- [Identify users](/docs/user-sessions/) in the browser with `c.setUserIdentity(...)`.
- [Send logs and feature usage](/docs/clients/javascript/sending-events/).
- [Flush events in serverless deployments](/docs/clients/javascript/guides/node/#short-lived-processes-and-serverless).

## Ask Exie

```prompt exie
Which error stacks tagged server happened most in the last 24 hours?
```

```prompt exie
Show me the latest event in this stack and explain what caused it.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most frequent new error in my Exceptionless project this week, read its latest event, and fix the SvelteKit load function or component that throws it.
```
