---
title: "React"
metaTitle: "Exceptionless error tracking for React"
description: "Install @exceptionless/react, start the client, and wrap your app in ExceptionlessErrorBoundary with a fallback to report React rendering errors."
order: 4
redirectFrom:
  - /docs/clients/javascript/react-example/
sources:
  - Exceptionless.JavaScript:packages/react/README.md
  - Exceptionless.JavaScript:packages/react/package.json
  - Exceptionless.JavaScript:packages/react/src/ExceptionlessErrorBoundary.tsx
  - Exceptionless.JavaScript:packages/react/src/index.ts
  - Exceptionless.JavaScript:packages/browser/src/plugins/BrowserErrorPlugin.ts
  - Exceptionless.JavaScript:packages/browser/src/BrowserExceptionlessClient.ts
  - Exceptionless.JavaScript:example/react/src/App.jsx
  - Exceptionless.JavaScript:example/react/src/index.jsx
  - Exceptionless.JavaScript:.agents/skills/exceptionless-javascript/references/client-react.md
---

# React

`@exceptionless/react` reports errors that crash your React components, and everything the
[browser client](/docs/clients/javascript/guides/browser/) reports: unhandled errors and promise rejections from event
handlers, effects, and async code. `ExceptionlessErrorBoundary` reports render errors and shows a fallback UI in place
of the broken part of the page.

It requires React 18 or later. For React Native and Expo, use the
[React Native guide](/docs/clients/javascript/guides/react-native/) instead. For Next.js, see the
[Next.js guide](/docs/clients/javascript/guides/nextjs/).

## Install with your coding agent

Paste this into Claude Code, Codex, Copilot, or another coding agent from the root of your React app:

```prompt agent
Add Exceptionless error monitoring to this React app. Read https://exceptionless.com/docs/clients/javascript/guides/react.md and follow it: install @exceptionless/react, start the client in the app's entry file, and wrap the root component in ExceptionlessErrorBoundary with a fallback. The agent skill at https://github.com/exceptionless/Exceptionless.JavaScript/tree/main/.agents/skills/exceptionless-javascript has source-checked notes for this package. Read the API key from the app's environment configuration instead of hard-coding it, and add a temporary button that throws a render error so I can confirm it shows up in Exceptionless.
```

## Install

```bash
npm install @exceptionless/react
```

## Configure

Start the client in your entry file, before you render, and wrap your app in `ExceptionlessErrorBoundary`. For a Vite
app that's `src/main.jsx`:

```jsx
import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import { Exceptionless, ExceptionlessErrorBoundary } from "@exceptionless/react"

import App from "./App.jsx"

Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "1.2.3" // optional: your app's version, used for regressions
})

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <ExceptionlessErrorBoundary fallback={<p>Something went wrong.</p>}>
      <App />
    </ExceptionlessErrorBoundary>
  </StrictMode>,
)
```

`startup` returns a promise, but you don't need to wait for it before rendering. It sets the API key right away, so
errors from the first render are queued and sent once the client finishes starting.

Import `Exceptionless` from `@exceptionless/react` anywhere else in your app. It's the same client, and it includes
everything from `@exceptionless/browser`.

> [!NOTE]
> Self-hosted instances use your own URL: add `c.serverUrl = "https://exceptionless.example.com"`.

### The error boundary

When a component inside `ExceptionlessErrorBoundary` throws while rendering, the boundary:

1. submits the error with React's component stack attached;
2. renders `fallback` in place of its children. With no `fallback`, it renders nothing.

You can wrap individual parts of the page in their own boundaries so one broken widget doesn't blank the whole app.

The boundary stays on the fallback until it remounts. To let people retry, change its `key`:

```jsx
import { useState } from "react"
import { ExceptionlessErrorBoundary } from "@exceptionless/react"

import Dashboard from "./Dashboard.jsx"

export default function DashboardPanel() {
  const [attempt, setAttempt] = useState(0)

  return (
    <ExceptionlessErrorBoundary
      key={attempt}
      fallback={<button onClick={() => setAttempt((value) => value + 1)}>Something went wrong. Try again</button>}
    >
      <Dashboard />
    </ExceptionlessErrorBoundary>
  )
}
```

React error boundaries don't catch errors in event handlers, effects that run later, or async code. Those reach the
browser's global handlers, which the client already listens to. Errors you catch yourself are not reported unless you
submit them:

```jsx
import { Exceptionless, toError } from "@exceptionless/react"

export function SaveButton({ profile }) {
  async function save() {
    try {
      await fetch("/api/profile", { method: "PUT", body: JSON.stringify(profile) })
    } catch (error) {
      await Exceptionless.submitException(toError(error))
    }
  }

  return <button onClick={save}>Save</button>
}
```

`toError` turns any caught value into an `Error`, since JavaScript lets code throw strings and other values.

## Verify

Render this component somewhere inside the boundary:

```jsx
import { useState } from "react"

export function ThrowTestError() {
  const [shouldThrow, setShouldThrow] = useState(false)

  if (shouldThrow) {
    throw new Error("Exceptionless test error")
  }

  return <button onClick={() => setShouldThrow(true)}>Throw a test error</button>
}
```

Click the button. The boundary shows its fallback, and within about 10 seconds the error appears on the **Events** page
in Exceptionless. Remove the component when you're done.

If nothing arrives, add `c.useDebugLogger()` to `startup` and check the browser console. See
[Troubleshooting](/docs/clients/javascript/troubleshooting/).

## What is captured automatically

- **Render errors** inside `ExceptionlessErrorBoundary`, with React's component stack stored on the error.
- **Unhandled errors and promise rejections** anywhere on the page, from event handlers, timers, effects, and async
  code.
- **Request details**: the browser, page URL, referrer, cookies, and query string.
- Everything else the
  [browser client captures](/docs/clients/javascript/guides/browser/#what-is-captured-automatically), such as ignoring
  errors from browser extensions and combining repeated errors.

## Next steps

- [Identify users and track sessions](/docs/user-sessions/) with `c.setUserIdentity(...)` and `c.useSessions()`.
- [Make minified stack traces readable](/docs/clients/javascript/source-maps/) for your production build.
- [Send feature usage and logs](/docs/clients/javascript/sending-events/) from components.
- [Remove private data](/docs/clients/javascript/client-configuration/#general-data-protection-regulation) such as
  cookies and query strings.

## Ask Exie

```prompt exie
What are the most frequent React errors this week, and which browsers do they happen in?
```

```prompt exie
Show me the stack trace of the latest event in this stack.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the newest error stack in my Exceptionless project from the last 24 hours, read its latest event, fix the React component that throws, and mark the stack fixed in the next version.
```
