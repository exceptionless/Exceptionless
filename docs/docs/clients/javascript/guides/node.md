---
title: "Node.js and serverless"
metaTitle: "Node.js and serverless error tracking"
description: "Report Node.js errors to Exceptionless from servers, scripts, workers, and serverless functions, and flush the queue before a short-lived process ends."
order: 2
redirectFrom:
  - /docs/clients/javascript/node-example/
sources:
  - Exceptionless.JavaScript:packages/node/README.md
  - Exceptionless.JavaScript:packages/node/package.json
  - Exceptionless.JavaScript:packages/node/src/NodeExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeLifeCyclePlugin.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeEnvironmentInfoPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/queue/DefaultEventQueue.ts
  - Exceptionless.JavaScript:example/nextjs/lib/exceptionless-server.js
---

# Node.js and serverless

`@exceptionless/node` reports uncaught exceptions and unhandled promise rejections from Node.js, with details about the
machine and process. It works in long-running servers, scripts, CLIs, queue workers, and serverless functions.
Short-lived processes need one extra call, covered [below](#short-lived-processes-and-serverless).

It requires Node.js 18 or later and ES modules.

## Install

```bash
npm install @exceptionless/node
```

## Configure

Call `startup` once when the process starts, before the rest of your app runs:

```js
import { Exceptionless } from "@exceptionless/node"

await Exceptionless.startup((c) => {
  c.apiKey = process.env.EXCEPTIONLESS_API_KEY
  c.version = "1.2.3" // optional: your app's version, used for regressions
})
```

Then import `Exceptionless` anywhere in your app to send events:

```js
import { Exceptionless } from "@exceptionless/node"

await Exceptionless.submitLog("worker", "Nightly import finished", "info")
```

The package is ES modules only. From a CommonJS file, load it with a dynamic `import("@exceptionless/node")`.

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`.

> [!IMPORTANT]
> The client listens for `uncaughtException` and `unhandledRejection`. Node.js does not exit on its own when a listener
> handles these, so after `startup` an uncaught exception no longer stops the process. If your process should stop, exit
> it yourself from your own handler, and send queued events first.

## Short-lived processes and serverless {#short-lived-processes-and-serverless}

The client sends its queue every 10 seconds, and those timers don't keep the process alive. In a long-running server
that's all you need. In a script that ends on its own, the client sends what's left when Node.js emits `beforeExit`.

Call `await Exceptionless.processQueue()` yourself when the process can end before that:

- before calling `process.exit()`, which skips `beforeExit`;
- at the end of a serverless function, because the platform freezes or stops the process after the response and the
  timers never fire;
- after a critical event in a CLI or job that a scheduler may kill.

Each call sends up to 50 events, the `submissionBatchSize` setting.

For a serverless function, start the client once outside the handler, and flush in a `finally` block:

```js
import { Exceptionless } from "@exceptionless/node"

const exceptionlessReady = Exceptionless.startup((c) => {
  c.apiKey = process.env.EXCEPTIONLESS_API_KEY
})

export async function handler(event) {
  await exceptionlessReady

  try {
    return await handleOrder(event)
  } catch (error) {
    await Exceptionless.submitException(error)
    throw error
  } finally {
    await Exceptionless.processQueue()
  }
}

async function handleOrder(event) {
  // your function's work
  return { statusCode: 200 }
}
```

Keeping the `startup` promise in a module-level variable means warm invocations reuse the started client. The same shape
works on AWS Lambda, Vercel and Netlify functions, and Google Cloud Functions. On Next.js, see the
[Next.js guide](/docs/clients/javascript/guides/nextjs/), which flushes with `after()`.

The client stores synced settings in a `.exceptionless` folder in the current working directory. On a read-only file
system it can't create the folder, so it keeps them in memory instead.

## Verify

Save this as `exceptionless-test.mjs` and run `node exceptionless-test.mjs`:

```js
import { Exceptionless } from "@exceptionless/node"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
})

await Exceptionless.submitException(new Error("Exceptionless test error"))
await Exceptionless.processQueue()
```

Open **Events** in Exceptionless. The error is there, with the machine name, Node.js version, and command line on the
event's **Machine & runtime** tab. If it isn't, add `c.useDebugLogger()` to `startup` and run the script again. See
[Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically {#what-is-captured-automatically}

- **Uncaught exceptions** and **unhandled promise rejections**. Rejections with a value that isn't an `Error` are
  converted to one. The client also raises `Error.stackTraceLimit` to 50 frames.
- **Environment details** on every event: OS name and version, architecture, processor count, memory, Node.js version,
  process name, ID, and command line, load average, and uptime, plus the machine name and IP addresses unless
  `includeMachineName` or `includeIpAddress` is off.
- **Request details** when you attach an Express-style request to an event. The
  [Express guide](/docs/clients/javascript/guides/express/) shows how.
- **Repeated errors.** When the same error repeats within 30 seconds, the client holds the repeats and sends them as one
  event with a count.

On `beforeExit`, the client also ends the current session if you turned on [sessions](/docs/user-sessions/).

### Readable stack traces for TypeScript and bundled code

Exceptionless applies source maps only to stack frames whose file is an HTTP or HTTPS URL. Node.js stack frames are file
paths, so let Node.js apply your source maps before the error is reported:

```bash
node --enable-source-maps dist/server.js
```

Or set `NODE_OPTIONS=--enable-source-maps` in the environment.

## Next steps

- [Add error and 404 middleware to Express](/docs/clients/javascript/guides/express/).
- [Send logs, feature usage, and handled errors](/docs/clients/javascript/sending-events/).
- [Remove private data](/docs/clients/javascript/client-configuration/#general-data-protection-regulation) such as
  headers, cookies, and request bodies.
- [Change log levels without redeploying](/docs/clients/javascript/client-configuration-values/).

## Ask Exie

```prompt exie
Which error types happened most often in the last 24 hours?
```

```prompt exie
How many errors did version 1.2.3 produce compared with the version before it?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
List the open error stacks in my Exceptionless project from the last 7 days, find the one with the most occurrences, read its latest event, and fix the cause in this service.
```
