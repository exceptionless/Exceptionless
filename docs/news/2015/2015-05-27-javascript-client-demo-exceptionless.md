---
title: "JavaScript Client Demo - Exceptionless"
---

# JavaScript Client Demo - Exceptionless

![Exceptionless JavaScript Client](/assets/img/news/blog-header-javascript.jpg)

We're getting closer and closer to version 1.0 of our [JavaScript client](/javascript-client-available-for-preview-testing/), and we wanted to give everyone a demo of installation, configuration, and usage.

If you're using Node.js, make sure to check out last week's blog post for [Node specific examples](/news/2015/2015-05-20-exceptionless-node-js-javascript-client-demo). Otherwise, continue reading for JavaScript examples.

As you read and begin playing with the Exceptionless JavaScript client, please make note of any feedback, bugs, etc, and [submit a GitHub issue](https://github.com/exceptionless/Exceptionless.JavaScript/issues) so we can fast track version 1.0 - we surely appreciate it!



## How the JavaScript Client was Built

We built our JavaScript client in [typescript 1.5](https://github.com/Microsoft/TypeScript) transpiled it to es5. Our single client works with both [Node.js](/news/2015/2015-05-20-exceptionless-node-js-javascript-client-demo) and JavaScript due to dependency injection and a [Universal Module Definition (UMD)](https://github.com/umdjs/umd).

## Installing the Exceptionless JavaScript Client

### Via Bower

  1. Install the package by running `bower install exceptionless`
  2. Add the Exceptionless script to your HTML page. We recommend placing the script at the top of the document to ensure Exceptionless picks up and reports the absolute most potential exceptions and events.

```html
``<script src="bower_components/exceptionless/dist/exceptionless.min.js">``</script>``
```

## Configuring the Client

Configuration of the Exceptionless JavaScript client can be accomplished a variety of ways. We list the common ways below, but make sure to check the [Exceptionless.JavaScript GitHub repo](https://github.com/exceptionless/Exceptionless.JavaScript) for the most up to date documentation if you run into any problems using this example code.
_NOTE: The only required setting you need to configure is the client's apiKey._

### Configuration Options

**1.** Configure the `apiKey` as part of the script tag. This method will be applied to all new instances of the ExceptionlessClient

```html
``<script src="bower_components/exceptionless/dist/exceptionless.min.js?apiKey=API_KEY_HERE">``</script>``
```

**2.** Set the `apiKey` on the default ExceptionlessClient instance.

```js
var client = exceptionless.ExceptionlessClient.default;
client.config.apiKey = 'API_KEY_HERE';
```

**3.** Create a new instance of the ExceptionlessClient and specify the `apiKey` or [configuration object](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.6.4/src/configuration/IConfigurationSettings.ts). _Note that the configuration object is optional._

```js
var client = new exceptionless.ExceptionlessClient('API_KEY_HERE'); // Required

// or with a configuration object
//var client = new exceptionless.ExceptionlessClient({
  //apiKey: 'API_KEY_HERE',
  //submissionBatchSize: 100
//});
```

## Sending Events

Unhandled exceptions will automatically be sent to your Exceptionless dashboard once the JavaScript client is configured properly. In order to send additional events, including log messages, feature usages, and more, you can use the code samples below and check the [Exceptionless.JavaScript GitHub Repo](https://github.com/exceptionless/Exceptionless.JavaScript) for the latest examples and documentation.

### Sending Log Messages, Feature Usages, etc

```js
var client = exceptionless.ExceptionlessClient.default;

client.submitLog('Logging made easy');

// You can also specify the log source and log level.
// We recommend specifying one of the following log levels: Trace, Debug, Info, Warn, Error
client.submitLog('app.logger', 'This is so easy', 'Info');
client.createLog('app.logger', 'This is so easy', 'Info').addTags('Exceptionless').submit();

// Submit feature usages
client.submitFeatureUsage('MyFeature');
client.createFeatureUsage('MyFeature').addTags('Exceptionless').submit();

// Submit a 404
client.submitNotFound('/somepage');
client.createNotFound('/somepage').addTags('Exceptionless').submit();

// Submit a custom event type
client.submitEvent({ message = 'Low Fuel', type = 'racecar', source = 'Fuel System' });
```

### Manually Sending Errors

To manually send events other than the automatically reported unhandled exceptions, you can use our fluent [event builder API](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.6.4/src/EventBuilder.ts).

The below example demonstrates sending a new error, "test," and setting the ReferenceID, Order and Quote properties, Tags, Geo, UserIdentity, and marking it as Critical.

```js
var client = exceptionless.ExceptionlessClient.default;

try {
  throw new Error('test');
} catch (error) {
  client.createException(error)
    // Set the reference id of the event so we can search for it later (reference:id).
    // This will automatically be populated if you call client.config.useReferenceIds();
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

## What Data is Collected?

We built the JavaScript client to be full featured and allow you to report and log all the data our other clients do. It has a fluent API, as mentioned above, and is ready to rock and roll.

We wire up to the window.onerror handler by default, in order to send unhandled exceptions to your Exceptionless dashboard automatically.

Finishing off the Exceptionless JavaScript client features, every event also includes request information.

A few screenshots of an individual event can be found below.


![Exceptionless JavaScript Event Request Details](/assets/img/news/javascript-client-event-request-info-150x150.png)
_Request Details_




## Sample


We have put together an [example](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example) that you can use to get an idea of how everything works. It is available on the [GitHub Repo](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example).

### To Get the Example Running...

  1. Clone or download the [GitHub Repo](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example)
  2. Edit the HTML file in the root example folder and replace the [existing API Key](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/index.html#L8) with yours. Also, comment out the [serverUrl](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/index.html#L16).
  3. Open the HTML file in your browser
  4. Open the console so that you can see the debug messages that the example generates
  5. Click the buttons on the page to submit an event


## Troubleshooting


Calling `client.config.useDebugLogger();` to enable debug logging is recommend and will output messages to the console regarding what the client is doing. Please [contact us by creating an issue on GitHub](https://github.com/exceptionless/Exceptionless.JavaScript/issues) if you need help or have any feedback regarding the JavaScript client.

## Feedback

As we move forward towards version 1.0 of our JavaScript client, we are looking for any and all feedback, so please don't hesitate to [let us know what you think, report a bug, etc](https://github.com/exceptionless/Exceptionless.JavaScript/issues).

Happy coding!
