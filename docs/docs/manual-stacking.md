---
title: "Manual Stacking"
description: "Group related Exceptionless events into a custom stack with a manual stacking key, with examples for the REST API, the .NET client, and JavaScript."
---

# Manual Stacking

We try to group events into intuitive stacks, but sometimes you might want to create your own for organization, reporting, troubleshooting, etc. A good example use case might be when you are introducing a new feature. The errors or events you send to Exceptionless may ultimately look like or be the same as events linked to other features in your applications. However, you might want to see the events triggered by use of this new feature stacked together.

How might we do this? You can use the `SetManualStackingKey` method to facilitate this need.

## Creating Custom Stacks

In the examples below, the cURL request sets a stacking key named "mystackingkey" to the value "ANOTHER FEATURE" and titles the stack "My stack title". The .NET and JavaScript examples call `SetManualStackingKey("MyCustomStackingKey")`, which stores "MyCustomStackingKey" as the event's `ManualStackingKey` value. Any events that share the same key are grouped into the same stack, regardless of the type or reason for the event.

To check which values grouped a stack, open the stack, click the gear button next to the status button, and choose **View Stacking Information**. For a manually stacked event, it lists your stacking key and value. Let's see how you would do this in practice.

## cURL Example

```shell
curl --location --request POST 'https://api.exceptionless.com/api/v2/events' \
--header 'Authorization: Bearer YOUR_PROJECT_TOKEN' \
--header 'Content-Type: application/json' \
--data-raw '{   "type": "error",
    "date":"2030-01-01T12:00:00.0000000-05:00",
    "title": "NEW FEATURE WORK",
    "@simple_error": {
        "message": "Weird Exception",
        "type": "System.Exception",
        "stack_trace": " at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests cs:line 99"
    },
    "@stack": { "title": "My stack title", "signature_data": { "mystackingkey": "ANOTHER FEATURE" } }
}'
```

## C# Example

```csharp
try {
    throw new ApplicationException("Unable to create order from quote.");
} catch (Exception ex) {
    ex.ToExceptionless().SetManualStackingKey("MyCustomStackingKey").Submit();
}
```

Or, you can set the stacking directly on an event (e.g., inside a plugin).

```csharp
event.SetManualStackingKey("MyCustomStackingKey");
```

## JavaScript Example

```javascript
import { Exceptionless } from "@exceptionless/browser";
// For Node.js, import from "@exceptionless/node" instead.

try {
  throw new Error("Unable to create order from quote.");
} catch (error) {
  await Exceptionless.createException(error).setManualStackingKey("MyCustomStackingKey").submit();
}
```

## When Should You Use This

Custom, manual stacking is certainly an advanced feature. We have tried to make your experience fantastic without needing this functionality, but sometimes, there's no avoiding it. So, let's think about some times when you might want to use manual stacking:

* Critical Functionality - if you have a piece of your app that you need see all errors for quickly, manual stacking would be perfect.
* Logs - we tend to think of stacks as errors, but they don't have to be, and you can easily use manual stacking to group logs for other purposes.
* Notifications - manual stacking makes it easy to set up [notifications](/docs/notifications) for errors or events that happen from a specific source that you define.

---

[Next > Filtering & Searching](/docs/filtering-and-searching)
