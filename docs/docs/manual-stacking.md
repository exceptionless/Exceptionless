---
title: "Manual Stacking"
---

# Manual Stacking

We try to group events into intuitive stacks, but sometimes you might want to create your own for organization, reporting, troubleshooting, etc. A good example use case might be when you are introducing a new feature. The errors or events you send to Exceptionless may ultimately look like or be the same as events linked to other features in your applications. However, you might want to see the events triggered by use of this new feature stacked together.

How might we do this? You can use the `SetManualStackingKey` method to facilitate this need.

## Creating Custom Stacks

In the below examples, we use `SetManualStackingKey` and are naming the custom stack "MyCustomStackingKey" and setting the value to "ANOTHER FEATURE. What this does is it ensures that any events, regardless of the type or reason for the event, will be grouped together.

![Example manual stacking on dashboard](img/Manual_Key.png)

In the example above, the events grouped under "My stack title" use custom stacking keys to group them together. Let's see how you would do this in practice.

## cURL Example

```shell
curl --location --request POST 'https://api.exceptionless.com/api/v2/events' \
--header 'Authorization: Bearer XUlBBdgFxAlmCsAZHDFTIacXpzYuZDuqDzzFYMlR' \
--header 'Content-Type: application/json' \
--data-raw '{   "type": "error",
    "date":"2030-01-01T12:00:00.0000000-05:00",
    "title": "NEW FEATURE WORK",
    "@simple_error": {
        "message": "Weird Exception",
        "type": "System.Exception",
        "stack_trace": " at Client.Tests.ExceptionlessClientTests.CanSubmitSimpleException() in ExceptionlessClientTests cs:line 99"
    },
    "@stack": { "title": "My stack title", "signature_data": { "mystackingkey": "ANOTHER FEATURE" }}
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
var client = exceptionless.ExceptionlessClient.default;
// Node.Js
// var client = require('exceptionless').ExceptionlessClient.default;

try {
  throw new Error('Unable to create order from quote.');
} catch (error) {
  client.createException(error).setManualStackingKey('MyCustomStackingKey').submit();
}
```

## When Should You Use This

Custom, manual stacking is certainly an advanced feature. We have tried to make your experience fantastic without needing this functionality, but sometimes, there's no avoiding it. So, let's think about some times when you might want to use manual stacking:

* Critical Functionality - if you have a piece of your app that you need see all errors for quickly, manual stacking would be perfect.
* Logs - we tend to think of stacks as errors, but they don't have to be, and you can easily use manual stacking to group logs for other purposes.
* Notifications - manual stacking makes it easy to set up [notifications](/docs/notifications) for errors or events that happen from a specific source that you define.

---

[Next > Filtering & Searching](/docs/filtering-and-searching)
