---
title: "Next.js"
metaTitle: "Exceptionless error tracking for Next.js"
description: "Report Next.js App Router errors from the browser and the server to Exceptionless with instrumentation files, onRequestError, and error boundaries."
order: 6
sources:
  - Exceptionless.JavaScript:example/nextjs/README.md
  - Exceptionless.JavaScript:example/nextjs/package.json
  - Exceptionless.JavaScript:example/nextjs/instrumentation.js
  - Exceptionless.JavaScript:example/nextjs/instrumentation-client.js
  - Exceptionless.JavaScript:example/nextjs/lib/exceptionless-browser.js
  - Exceptionless.JavaScript:example/nextjs/lib/exceptionless-server.js
  - Exceptionless.JavaScript:example/nextjs/lib/next-request.js
  - Exceptionless.JavaScript:example/nextjs/app/error.jsx
  - Exceptionless.JavaScript:example/nextjs/app/global-error.jsx
  - Exceptionless.JavaScript:example/nextjs/app/api/demo/route.js
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeRequestInfoPlugin.ts
---

# Next.js

A Next.js App Router app runs code in two places, so it uses two packages: `@exceptionless/browser` in the browser and
`@exceptionless/node` on the server. With the files in this guide, Exceptionless receives:

- unhandled errors and promise rejections in the browser;
- client render errors caught by `app/error.jsx` and `app/global-error.jsx`;
- server render, route handler, and server action errors from Next.js's `onRequestError` hook, with the request
  attached.

This guide follows the
[Next.js example app](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/example/nextjs) in the SDK
repository, which uses Next.js 16 and the App Router. The server side runs in the Node.js runtime only. Errors in the
Edge runtime are not reported.

## Install

```bash
npm install @exceptionless/browser @exceptionless/node
```

Add your API key to `.env.local`. The browser needs the `NEXT_PUBLIC_` variable, and the server reads the other:

```bash
NEXT_PUBLIC_EXCEPTIONLESS_API_KEY=YOUR_API_KEY
EXCEPTIONLESS_API_KEY=YOUR_API_KEY
```

## Configure

### 1. Start each client once

Create a small module for each side. Each one keeps the `startup` promise, so every caller shares one started client and
later calls wait for the same startup.

`lib/exceptionless-browser.js`:

```js
import { Exceptionless } from "@exceptionless/browser"

export { Exceptionless }

let startupPromise

export function startup() {
  startupPromise ??= Exceptionless.startup((c) => {
    c.apiKey = process.env.NEXT_PUBLIC_EXCEPTIONLESS_API_KEY
    c.defaultTags.push("client")
  })

  return startupPromise
}
```

`lib/exceptionless-server.js`:

```js
import { Exceptionless, KnownEventDataKeys, toError } from "@exceptionless/node"

export { Exceptionless, KnownEventDataKeys, toError }

let startupPromise

export function startup() {
  startupPromise ??= Exceptionless.startup((c) => {
    c.apiKey = process.env.EXCEPTIONLESS_API_KEY
    c.addDataExclusions("authorization", "cookie", "password", "set-cookie", "token")
    c.defaultTags.push("server")
  })

  return startupPromise
}
```

### 2. Start the browser client

Next.js runs `instrumentation-client.js`, in the project root or `src/`, before your app becomes interactive:

```js
import { startup } from "./lib/exceptionless-browser.js"

void startup()
```

### 3. Report server errors

`instrumentation.js` starts the server client and reports every error that Next.js passes to `onRequestError`. It
imports the server module dynamically and checks `NEXT_RUNTIME`, so the Node.js package never loads in the Edge runtime:

```js
export async function register() {
  if (process.env.NEXT_RUNTIME !== "nodejs") {
    return
  }

  const { startup } = await import("./lib/exceptionless-server.js")
  await startup()
}

export async function onRequestError(error, request, context) {
  if (process.env.NEXT_RUNTIME !== "nodejs") {
    return
  }

  const { Exceptionless, KnownEventDataKeys, startup, toError } = await import("./lib/exceptionless-server.js")
  await startup()

  await Exceptionless.createUnhandledException(toError(error), `nextjs.${context.routeType}`)
    .setContextProperty(KnownEventDataKeys.RequestInfo, toRequestInfo(request))
    .setProperty("digest", error?.digest)
    .setProperty("routePath", context.routePath)
    .setProperty("routeType", context.routeType)
    .submit()

  // Send now: a serverless function can be frozen right after the response.
  await Exceptionless.processQueue()
}

// Converts the request Next.js passes to onRequestError into the shape the Node.js client reads.
function toRequestInfo(request) {
  const headers = Object.fromEntries(
    Object.entries(request.headers).map((
      [key, value],
    ) => [key.toLowerCase(), Array.isArray(value) ? value.join(", ") : String(value)]),
  )
  const protocol = headers["x-forwarded-proto"] ?? "http"
  const host = headers["x-forwarded-host"] ?? headers.host ?? "localhost"
  const url = new URL(request.path, `${protocol}://${host}`)

  return {
    method: request.method,
    secure: url.protocol === "https:",
    ip: headers["x-forwarded-for"]?.split(",")[0]?.trim() ?? "",
    hostname: url.hostname,
    path: url.pathname,
    headers,
    params: Object.fromEntries(url.searchParams.entries()),
  }
}
```

The `digest` property matches the digest Next.js shows in the browser for a server error, so you can connect what a user
saw with the server event.

### 4. Report client render errors

Errors during client rendering go to the nearest `error.jsx`. Errors with a `digest` came from the server and were
already reported by `onRequestError`, so report only the others.

`app/error.jsx`:

```jsx
"use client"

import { useEffect } from "react"

import { Exceptionless, startup } from "../lib/exceptionless-browser.js"

export default function ErrorPage({ error, reset }) {
  useEffect(() => {
    if (error.digest) {
      return
    }

    void startup().then(() => Exceptionless.createException(error).addTags("error-boundary").submit())
  }, [error])

  return (
    <main>
      <h1>Something went wrong.</h1>
      <button type="button" onClick={() => reset()}>
        Try again
      </button>
    </main>
  )
}
```

`app/global-error.jsx` catches errors in the root layout. It uses the same effect and renders its own `<html>` and
`<body>`:

```jsx
"use client"

import { useEffect } from "react"

import { Exceptionless, startup } from "../lib/exceptionless-browser.js"

export default function GlobalError({ error, reset }) {
  useEffect(() => {
    if (error.digest) {
      return
    }

    void startup().then(() => Exceptionless.createException(error).addTags("global-error").submit())
  }, [error])

  return (
    <html lang="en">
      <body>
        <h1>Something went wrong.</h1>
        <button type="button" onClick={() => reset()}>
          Try again
        </button>
      </body>
    </html>
  )
}
```

### 5. Send events from route handlers

Start the server client and send events as usual. Use `after()` to send the queue once the response is on its way:

```js
import { after } from "next/server"

import { Exceptionless, startup } from "../../../lib/exceptionless-server.js"

export async function POST() {
  await startup()
  await Exceptionless.submitFeatureUsage("Checkout")

  after(() => Exceptionless.processQueue())

  return Response.json({ ok: true })
}
```

In client components, import `Exceptionless` and `startup` from `lib/exceptionless-browser.js` and call
`await startup()` before you submit.

> [!NOTE]
> Self-hosted instances use your own URL: set `c.serverUrl` in both modules, for example from
> `NEXT_PUBLIC_EXCEPTIONLESS_SERVER_URL` and `EXCEPTIONLESS_SERVER_URL`.

## Verify

Add a route handler that throws, at `app/api/boom/route.js`:

```js
export async function GET() {
  throw new Error("Exceptionless test error")
}
```

Run `npm run dev` and open `http://localhost:3000/api/boom`. The error appears on the **Events** page in Exceptionless,
tagged `server`, with the request on its **Request** tab and the route path and type in its extended data. Delete the
route when you're done.

To check the browser side, throw from a button's click handler in a client component. That error arrives tagged
`client`.

## What is captured automatically

- **In the browser**: everything the
  [browser client captures](/docs/clients/javascript/guides/browser/#what-is-captured-automatically), including
  unhandled errors, promise rejections, and the page URL.
- **On the server**: errors from server rendering, route handlers, and server actions through `onRequestError`, with the
  request method, path, headers, query string, and client IP, the route path and type, and the digest. Data exclusions
  remove matching header and query keys.
- **In the Node.js process**: uncaught exceptions and machine details, as described in the
  [Node.js guide](/docs/clients/javascript/guides/node/#what-is-captured-automatically).

## Next steps

- [Make minified browser stack traces readable](/docs/clients/javascript/source-maps/) with source maps.
- [Identify users](/docs/user-sessions/) in the browser with `c.setUserIdentity(...)`.
- [Send logs and feature usage](/docs/clients/javascript/sending-events/).
- [Flush events in other serverless code](/docs/clients/javascript/guides/node/#short-lived-processes-and-serverless).

## Ask Exie

```prompt exie
Which error stacks tagged server happened most in the last 24 hours?
```

```prompt exie
Compare error counts for events tagged client and server this week.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the newest error stack tagged server in my Exceptionless project, read the routePath and request on its latest event, and fix the failing route or server component in this Next.js app.
```
