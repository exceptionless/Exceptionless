---
title: "Send events from JavaScript"
navTitle: "Sending events"
metaTitle: "Send events from the JavaScript client"
description: "Send handled errors, logs, feature usage, 404s, and custom events from JavaScript, and add tags, extended data, user identity, and references."
order: 3
sources:
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/EventBuilder.ts
  - Exceptionless.JavaScript:packages/core/src/models/Event.ts
  - Exceptionless.JavaScript:packages/core/src/Utils.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/ReferenceIdPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
  - Exceptionless.JavaScript:packages/core/test/DocumentationExamples.test.ts
  - Exceptionless.JavaScript:.agents/skills/exceptionless-javascript/references/sending-events.md
  - src/Exceptionless.Core/Pipeline/035_CopySimpleDataToIdxAction.cs
---

# Send events from JavaScript

Once the client is started, it reports unhandled errors on its own. Use the methods on this page to send errors you
catch, logs, feature usage, 404s, and your own event types, and to attach more information to any of them.

The examples import from `@exceptionless/browser`. The methods are the same in every package; import `Exceptionless`
from the package you installed.

## Send an error you caught

```js
import { Exceptionless, toError } from "@exceptionless/browser"

try {
  await placeOrder() // your own code
} catch (error) {
  await Exceptionless.submitException(toError(error))
}
```

`submitException` takes an `Error`. JavaScript lets code throw any value, so `toError` converts strings and other values
to an `Error` and passes real errors through unchanged.

## Send a log message

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.submitLog("Order placed")
await Exceptionless.submitLog("checkout", "Order placed")
await Exceptionless.submitLog("checkout", "Payment provider timed out", "warn")
```

The forms are message only, source and message, and source, message, and level. Use the levels `trace`, `debug`, `info`,
`warn`, `error`, and `fatal`. The source is usually the area of your app, such as a logger or module name.

You can turn log sources up or down from Exceptionless without redeploying. The client drops logs below the minimum
level for their source. See [Log levels](/docs/setting-log-levels/).

## Send feature usage

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.submitFeatureUsage("Export to CSV")
```

The feature name is the event's source. See [Feature usage](/docs/feature-usage/) for the views that use these events.

## Send a 404

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.submitNotFound("/products/old-sku")
```

The path is the event's source. The [Express guide](/docs/clients/javascript/guides/express/) reports 404s from
middleware.

## Send a custom event

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.submitEvent({ type: "racecar", message: "Low fuel", source: "Fuel System" })
```

An event with no type is sent as a log.

## Add more information

Every `submit*` method has a `create*` method that returns an event builder. Chain what you need and end with
`submit()`:

```js
import { Exceptionless } from "@exceptionless/browser"

const order = {
  id: "order-123",
  quoteId: 123,
  creditCardNumber: "4111111111111111",
}

try {
  throw new Error("Unable to create order from quote")
} catch (error) {
  await Exceptionless.createException(error)
    // Set your own reference ID so you can find this event later.
    .setReferenceId("order-12345678")
    // Add the order without its card number, up to 4 levels deep.
    .setProperty("Order", order, 4, ["creditCardNumber"])
    .setProperty("Quote", 123)
    .addTags("orders")
    // Adds the Critical tag.
    .markAsCritical()
    .setGeo(43.595089, -88.444602)
    .setUserIdentity("user-123", "Jane Doe")
    .submit()
}
```

The builder methods are `createException`, `createUnhandledException`, `createLog`, `createFeatureUsage`,
`createNotFound`, `createSessionStart`, and `createEvent`.

| Method                                                      | What it does                                                                                                                                                                                                                               |
| ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `addTags(...tags)`                                          | Adds tags.                                                                                                                                                                                                                                 |
| `setProperty(name, value, maxDepth?, excluded?)`            | Adds a value to the event's extended data. Objects are copied up to `maxDepth` levels (10 by default), without the property names in `excluded` or your [data exclusions](/docs/clients/javascript/client-configuration/#data-exclusions). |
| `markAsCritical(critical = true)`                           | Adds the `Critical` tag. `markAsCritical(false)` does nothing.                                                                                                                                                                             |
| `setUserIdentity(identity, name?)`                          | Sets the user for this event. Also accepts `{ identity, name, data }`.                                                                                                                                                                     |
| `setUserDescription(email, description)`                    | Adds the user's email address and their description of what happened.                                                                                                                                                                      |
| `setReferenceId(id)`                                        | Sets the event's reference ID. It must be 8 to 100 letters, digits, or hyphens, or the method throws. See [Reference IDs](/docs/references-ids/).                                                                                          |
| `setEventReference(name, id)`                               | Links this event to another event by that event's reference ID.                                                                                                                                                                            |
| `setManualStackingKey(key, title?)`                         | Groups events with the same key into one stack, instead of the default grouping. See [Stacking](/docs/concepts/stacking/).                                                                                                                 |
| `setManualStackingInfo(signatureData, title?)`              | Groups events by your own set of signature values.                                                                                                                                                                                         |
| `setGeo(latitude, longitude)`                               | Sets where the event happened. Throws if a value is out of range.                                                                                                                                                                          |
| `setValue(value)`                                           | Sets a numeric value on the event.                                                                                                                                                                                                         |
| `setSource(source)`, `setMessage(message)`, `setType(type)` | Override the event's source, message, or type.                                                                                                                                                                                             |
| `setContextProperty(name, value)`                           | Passes a value to plugins without sending it, such as an Express request.                                                                                                                                                                  |
| `submit()`                                                  | Sends the event to the queue and returns the plugin context. `context.cancelled` is `true` if a plugin or a setting stopped it.                                                                                                            |

To add the same tags or data to every event, use `defaultTags` and `defaultData` in the
[configuration](/docs/clients/javascript/client-configuration/).

### Make custom data searchable

On paid plans, Exceptionless indexes simple extended data values, such as strings, numbers, booleans, and dates, so you
can filter on them:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.createFeatureUsage("Checkout")
  .setProperty("plan", "enterprise")
  .setProperty("retryCount", 2)
  .submit()
```

These events match the filters `data.plan:enterprise` and `data.retrycount:2`. Field names are lowercased. Keep names
short and stable, and keep objects for detail you only read. See
[Filtering and searching](/docs/filtering-and-searching/).

## Let users describe an error

Errors get a reference ID automatically. Show it to the user, then attach what they tell you to the event:

```js
import { Exceptionless } from "@exceptionless/browser"

const referenceId = Exceptionless.getLastReferenceId()

// Later, when the user fills in your feedback form:
await Exceptionless.updateUserEmailAndDescription(referenceId, "jane@example.com", "The checkout page went blank.")
```

`getLastReferenceId()` returns the reference ID of the last queued event that had one. See
[Reference IDs](/docs/references-ids/).

## Send right away

`submit` puts the event in a queue that the client sends every 10 seconds. Before a short-lived process exits, or before
a hard page navigation, send the queue yourself:

```js
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.processQueue()
```

See [Node.js and serverless](/docs/clients/javascript/guides/node/#short-lived-processes-and-serverless).

## Ask Exie

```prompt exie
How many times was Export to CSV used each day this week?
```

```prompt exie
Count log events by level for the last 24 hours.
```

```prompt exie
Which stacks had events tagged Critical today?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can search the data you send:

```prompt agent
Search my Exceptionless project for Checkout feature usage events from the last 7 days where data.plan is enterprise, and summarize how often they retried.
```
