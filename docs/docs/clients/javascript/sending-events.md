---
title: "Sending Events"
order: 4
---

# Sending Events

Once configured, Exceptionless automatically sends unhandled exceptions that happen in your application. To send different event types, as well as customize the data that is sent, continue reading.

You can send us log messages, feature usages, or other kinds of events easily with our fluent api.

```js
import { Exceptionless } from "@exceptionless/browser";

await Exceptionless.submitLog('Logging made easy');

// You can also specify the log source and log level.
// We recommend specifying one of the following log levels: Trace, Debug, Info, Warn, Error
await Exceptionless.submitLog('app.logger', 'This is so easy', 'Info');
await Exceptionless.createLog('app.logger', 'This is so easy', 'Info').addTags('Exceptionless').submit();

// Submit feature usages
await Exceptionless.submitFeatureUsage('MyFeature');
await Exceptionless.createFeatureUsage('MyFeature').addTags('Exceptionless').submit();

// Submit a 404
await Exceptionless.submitNotFound('/somepage');
await Exceptionless.createNotFound('/somepage').addTags('Exceptionless').submit();

// Submit a custom event type
await Exceptionless.submitEvent({ message = 'Low Fuel', type = 'racecar', source = 'Fuel System' });
```

### Manually Sending Errors

In addition to automatically sending all unhandled exceptions, you may want to manually send exceptions to the service. You can do so by using code like this:

```javascript
import { Exceptionless } from "@exceptionless/browser";

try {
  throw new Error('test');
} catch (error) {
  await Exceptionless.submitException(error);
}
```

### Sending Additional Information

You can easily include additional information in your error reports using our fluent [event builder API](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/packages/core/src/EventBuilder.ts).

```javascript
import { Exceptionless } from "@exceptionless/node";

try {
  throw new Error('Unable to create order from quote.');
} catch (error) {
  await Exceptionless.createException(error)
    // Set the reference id of the event so we can search for it later (reference:id).
    // This will automatically be populated by default with a unique id;
    .setReferenceId('random guid')
    // Add the order object (the ability to exclude specific fields will be coming in a future version).
    .setProperty("Order", order)
    // Set the quote number.
    .setProperty("Quote", 123)
    // Add an order tag.
    .addTags("Order")
    // Mark critical.
    .markAsCritical()
    // Set the coordinates of the end user.
    .setGeo(43.595089, -88.444602)
    // Set the user id that is in our system and provide a friendly name.
    .setUserIdentity(user.Id, user.FullName)
    // Submit the event.
    .submit();
}
```

---

[Next > Troubleshooting](/docs/clients/javascript/troubleshooting)
