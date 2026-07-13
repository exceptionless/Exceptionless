---
title: "Deno"
order: 6
---

# Deno

Deno can use the runtime-neutral `@exceptionless/core` package through its npm compatibility layer. The core package
sends events with the built-in Fetch API and does not install Deno-specific global error handlers, so submit caught
errors manually or connect Deno's runtime error events in your application.

## Configure the client

```ts
import { ExceptionlessClient, toError } from "npm:@exceptionless/core";

const exceptionless = new ExceptionlessClient();

await exceptionless.startup((configuration) => {
  configuration.apiKey = Deno.env.get("EXCEPTIONLESS_API_KEY") ?? "";
  configuration.updateSettingsWhenIdleInterval = 0;

  const serverUrl = Deno.env.get("EXCEPTIONLESS_SERVER_URL");
  if (serverUrl) {
    configuration.serverUrl = serverUrl;
  }
});
```

## Send events

```ts
try {
  throw new Error("Something went wrong");
} catch (error) {
  await exceptionless.submitException(toError(error));
}

await exceptionless.submitLog("Deno task completed");
await exceptionless.submitFeatureUsage("DailyImport");

// Flush queued events before a short-lived command exits.
await exceptionless.processQueue();
await exceptionless.suspend();
```

Run the application with access to the API key and the Exceptionless collector:

```bash
deno run --allow-env=EXCEPTIONLESS_API_KEY,EXCEPTIONLESS_SERVER_URL --allow-net=collector.exceptionless.io app.ts
```

For a self-hosted instance, replace the `--allow-net` hostname with your Exceptionless server hostname. Long-running
services can keep the client active and call `suspend()` during graceful shutdown.
