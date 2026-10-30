---
title: "Express"
metaTitle: "Exceptionless error tracking for Express"
description: "Report Express route errors and 404s to Exceptionless with request details, using error-handling middleware and @exceptionless/node."
order: 3
redirectFrom:
  - /docs/clients/javascript/express-example/
sources:
  - Exceptionless.JavaScript:example/express/app.js
  - Exceptionless.JavaScript:example/express/package.json
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeRequestInfoPlugin.ts
  - Exceptionless.JavaScript:packages/node/src/plugins/NodeGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/EventBuilder.ts
---

# Express

Two middleware functions report every error that reaches Express's error handler and every request that matches no
route, with the request's method, path, headers, and body attached. Errors outside a request are reported by the
[Node.js client](/docs/clients/javascript/guides/node/) on its own.

This guide uses Express 5, Node.js 18 or later, and ES modules.

## Install

```bash
npm install @exceptionless/node express
```

## Configure

Start the client before you create the app, then add the two middleware functions after all your routes. Save this as
`app.js` in a project whose `package.json` has `"type": "module"`:

```js
import express from "express"
import { Exceptionless, KnownEventDataKeys, toError } from "@exceptionless/node"

await Exceptionless.startup((c) => {
  c.apiKey = process.env.EXCEPTIONLESS_API_KEY
  c.addDataExclusions("authorization", "cookie", "password", "token")
})

const app = express()
app.use(express.json())

app.get("/", (req, res) => {
  res.send("Hello World!")
})

app.get("/boom", () => {
  throw new Error("Exceptionless test error")
})

// Report errors. Express treats middleware with four arguments as an error handler.
app.use(async (err, req, res, next) => {
  if (res.headersSent) {
    return next(err)
  }

  await Exceptionless.createUnhandledException(toError(err), "express")
    .setContextProperty(KnownEventDataKeys.RequestInfo, req)
    .submit()

  res.status(500).send("Something broke!")
})

// Report requests that matched no route.
app.use(async (req, res) => {
  await Exceptionless.createNotFound(req.originalUrl).setContextProperty(KnownEventDataKeys.RequestInfo, req).submit()

  res.status(404).send("Sorry, can't find that!")
})

app.listen(3000, () => {
  console.log("Listening on http://localhost:3000")
})
```

`setContextProperty(KnownEventDataKeys.RequestInfo, req)` hands the Express request to the client, which turns it into
the event's request details. Use the same call when you report a handled error inside a route:

```js
app.get("/orders/:id", async (req, res) => {
  try {
    // loadOrder is your own data access function.
    res.json(await loadOrder(req.params.id))
  } catch (error) {
    await Exceptionless.createException(toError(error)).setContextProperty(KnownEventDataKeys.RequestInfo, req).submit()

    res.status(500).send("Unable to load the order")
  }
})
```

Express 5 sends errors thrown or rejected in `async` route handlers to the error middleware. In Express 4, pass errors
from async code to `next(error)` so they reach it.

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`.

## Verify

Start the app with `EXCEPTIONLESS_API_KEY=YOUR_API_KEY node app.js` and open `http://localhost:3000/boom`. Within about
10 seconds, the error appears on the **Events** page in Exceptionless with the request on its **Request** tab. Open
`http://localhost:3000/missing` to send a 404.

If nothing arrives, add `c.useDebugLogger()` to `startup` and watch the console. See
[Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically

For events that carry an Express request:

- the HTTP method, host, port, path, whether the request used HTTPS, the user agent, and the referrer;
- the client IP address, unless `includeIpAddress` is off;
- cookies, unless `includeCookies` is off;
- request headers, unless `includeHeaders` is off. `Authorization`, `Cookie`, `Host`, `Proxy-Authorization`, `Referer`,
  and `User-Agent` are never copied into the headers;
- route parameters (`req.params`), unless `includeQueryString` is off;
- the parsed body (`req.body`), unless `includePostData` is off. It's only there if a body parser such as
  `express.json()` ran.

[Data exclusions](/docs/security/), from `c.addDataExclusions(...)` and from your project settings, remove matching
cookie, header, parameter, and body keys. Requests whose user agent matches your project's **Spam Detection** list are
not reported.

Everything the [Node.js client](/docs/clients/javascript/guides/node/#what-is-captured-automatically) captures is also
included, such as uncaught exceptions outside requests and machine details.

## Next steps

- [Turn off request data you don't need](/docs/clients/javascript/client-configuration/#general-data-protection-regulation).
- [Send logs and feature usage from routes](/docs/clients/javascript/sending-events/).
- [Ignore 404s from known paths](/docs/clients/javascript/client-configuration-values/) without redeploying.

## Ask Exie

```prompt exie
Which paths returned the most 404s this week?
```

```prompt exie
Show me the request details for the latest event in this stack.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the Express route errors in my Exceptionless project from the last 24 hours, read the request details on the latest events, and fix the failing route handler in this app.
```
