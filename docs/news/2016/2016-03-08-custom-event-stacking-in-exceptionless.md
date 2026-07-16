---
title: "Custom Event Stacking in Exceptionless"
---

# Custom Event Stacking in Exceptionless

![custom event stacking with exceptionless](/assets/img/news/custom-event-stacking-graphicl.png)Sometimes you just need things to be your way.

We get it... your morning coffee, folded towels, and how events stack (group) in your event reporting application should be controllable and customizable.

Well, thanks to a great suggestion by [@adamzolotarev](https://github.com/adamzolotarev), now they are! Well, the events, at least.

## Why Custom Event Stacking?

We do our best to group your events into relevant and smartly-named stacks, but there are cases where you may want to specifically name a stack and attribute certain events to it for organization, reporting, troubleshooting, or other reasons.

To facilitate this need, we created `SetManualStackingKey`, which both .NET and JavaScript client users can set.

## How Do I Create Custom Event Stacks?

Adding your own custom stacking to events in Exceptionless is super easy. Below are examples for both .NET and JavaScript.

In these examples, we are using `setManualStackingKey` and naming the custom stack "MyCustomStackingKey".

So, any events you use the below for will be a part of the custom stack, and all other events, exceptiones, logs, feature usages, etc will still be stacked automatically, like normal, by the app.

### .NET Custom Event Stack Example

```cs
try {
    throw new ApplicationException("Unable to create order from quote.");
} catch (Exception ex) {
    ex.ToExceptionless().SetManualStackingKey("MyCustomStackingKey").Submit();
}
```

Alternatively, you can set the stacking directly on an event (e.g., inside a plugin).

```cs
event.SetManualStackingKey("MyCustomStackingKey");
```

### JavaScript Custom Event Stack Example

```js
var client = exceptionless.ExceptionlessClient.default;
// Node.Js
// var client = require('exceptionless').ExceptionlessClient.default;

try {
  throw new Error('Unable to create order from quote.');
} catch (error) {
  client.createException(error).setManualStackingKey('MyCustomStackingKey').submit();
}
```

## How Do We Stack Up?

We're always interested in what you think of Exceptionless' features and functionality, so let us know if you find custom stacking useful, need help implementing it, or just want to chat over on [GitHub](https://github.com/exceptionless).

Thanks for reading!
