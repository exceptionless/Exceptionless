---
title: "Angular"
metaTitle: "Exceptionless error tracking for Angular"
description: "Report Angular errors to Exceptionless from a standalone app with @exceptionless/browser and a custom ErrorHandler provider."
order: 9
sources:
  - Exceptionless.JavaScript:packages/browser/README.md
  - Exceptionless.JavaScript:packages/browser/src/BrowserExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserGlobalHandlerPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/Utils.ts
---

# Angular

Angular catches errors from components, templates, and event bindings and passes them to its `ErrorHandler`, so they
never reach the browser's global error handlers. A small `ErrorHandler` provider sends them to Exceptionless.
`@exceptionless/browser` reports everything else, such as unhandled promise rejections.

There is no Angular-specific package. This guide uses `@exceptionless/browser` with a standalone Angular app, the
default for new projects. For AngularJS 1.x, use
[`@exceptionless/angularjs`](https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/packages/angularjs).

## Install

```bash
npm install @exceptionless/browser
```

## Configure

### 1. Add an error handler

Create `src/app/exceptionless-error-handler.ts`. It submits each error and then calls Angular's default handler, which
logs it to the console:

```ts
import { ErrorHandler, Injectable } from "@angular/core"
import { Exceptionless, toError } from "@exceptionless/browser"

@Injectable()
export class ExceptionlessErrorHandler extends ErrorHandler {
  override handleError(error: unknown): void {
    void Exceptionless.submitException(toError(error))
    super.handleError(error)
  }
}
```

`toError` turns any value into an `Error`, since Angular can pass strings and other values to `handleError`.

> [!TIP]
> If your `app.config.ts` includes `provideBrowserGlobalErrorListeners()`, Angular also forwards window errors and
> unhandled rejections to `ErrorHandler`. The browser client already reports those, so the same error can arrive twice.
> Remove that provider if you see duplicates.

### 2. Provide it

Add the provider to `src/app/app.config.ts`, next to the providers your app already has:

```ts
import { ApplicationConfig, ErrorHandler } from "@angular/core"
import { provideRouter } from "@angular/router"

import { routes } from "./app.routes"
import { ExceptionlessErrorHandler } from "./exceptionless-error-handler"

export const appConfig: ApplicationConfig = {
  providers: [provideRouter(routes), { provide: ErrorHandler, useClass: ExceptionlessErrorHandler }],
}
```

### 3. Start the client

Call `startup` in `src/main.ts`, before you bootstrap the app:

```ts
import { bootstrapApplication } from "@angular/platform-browser"
import { Exceptionless } from "@exceptionless/browser"

import { App } from "./app/app" // AppComponent from "./app/app.component" in older projects
import { appConfig } from "./app/app.config"

Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3" // optional: your app's version, used for regressions
})

bootstrapApplication(App, appConfig).catch((err) => console.error(err))
```

`startup` sets the API key right away, so you don't need to wait for it before bootstrapping. Errors that happen during
bootstrap are queued and sent once the client finishes starting.

To submit errors you catch yourself, import `Exceptionless` from `@exceptionless/browser` in any service or component
and call `Exceptionless.submitException(toError(error))`.

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`.

## Verify

Add a button to a component that throws when clicked:

```ts
import { Component } from "@angular/core"

@Component({
  selector: "app-throw-test-error",
  template: `<button (click)="throwTestError()">Throw a test error</button>`,
})
export class ThrowTestError {
  throwTestError(): void {
    throw new Error("Exceptionless test error")
  }
}
```

Click it. Angular logs the error to the console, and within about 10 seconds it appears on the **Events** page in
Exceptionless. Remove the component when you're done.

If the console shows the error but Exceptionless doesn't, add `c.useDebugLogger()` to `startup` and look for client
messages. See [Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically

- **Errors Angular handles**: anything Angular passes to `ErrorHandler`, from templates, event bindings, lifecycle
  hooks, and change detection.
- **Unhandled errors and promise rejections** that Angular doesn't handle.
- Everything else the
  [browser client captures](/docs/clients/javascript/guides/browser/#what-is-captured-automatically), such as request
  details and ignoring errors from browser extensions.

## Next steps

- [Identify users and track sessions](/docs/user-sessions/) with `c.setUserIdentity(...)` and `c.useSessions()`.
- [Make minified stack traces readable](/docs/clients/javascript/source-maps/) for your production build.
- [Send feature usage and logs](/docs/clients/javascript/sending-events/) from services and components.

## Ask Exie

```prompt exie
Which new error stacks appeared since Monday?
```

```prompt exie
Mark this stack fixed in version 1.2.4.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most frequent new error in my Exceptionless project this week, read its latest event, and fix the Angular component or service that throws it.
```
