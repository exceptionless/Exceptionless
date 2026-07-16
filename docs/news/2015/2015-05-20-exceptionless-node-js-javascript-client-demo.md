---
title: "Exceptionless Node.js JavaScript Client Demo"
---

# Exceptionless Node.js JavaScript Client Demo

![Exceptionless Node.js JavaScript Client](/assets/img/news/node-js-header.jpg)

Last week we announced our full featured [JavaScript client](/news/2015/2015-05-13-javascript-client-available-for-preview-testing), and we're super excited about releasing a version 1.0 soon.

This week we'd like to put more details out there on the Node.js version of the JavaScript client, including installation, configuration, and usage. We've also set up an [Express.js sample app](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/express/app.js) that you can spin up locally to play with things.

Let's take a look.

## How the JavaScript Client was Built

Our javascript client is built in [typescript 1.5](https://github.com/Microsoft/TypeScript) and is transpiled to es5. We have a single client that works with both Node.js and JavaScript due to dependency injection and a [Universal Module Definition (UMD)](https://github.com/umdjs/umd). For capturing Node stack traces, we use [Felixge's Node Stack Trace](https://github.com/felixge/node-stack-trace) library.

## Installing Exceptionless for Node.js

  1. Install the package by running `npm install exceptionless --save-dev`
  2. Add the Exceptionless client to your app:

```js
var client = require('exceptionless.node').ExceptionlessClient.default;
```

## Configuring the Client

You can configure the Exceptionless client a few different ways for Node.js. The below is the most common way, but for more configuration options and documentation, visit the [Exceptionless.JavaScript GitHub repo](https://github.com/exceptionless/Exceptionless.JavaScript). _NOTE: The only required setting you need to configure is the client's apiKey._

Set the `apiKey` on the default ExceptionlessClient instance.

```js
var client = require('exceptionless.node').ExceptionlessClient.default;
client.config.apiKey = 'API_KEY_HERE';
```

## Sending Events

Once configured, the Exceptionless Node.js JavaScript client will automatically send your dashboard any unhandled exceptions that happen in your application. If you would like to send additional event types, such as log messages, feature usages, etc, take a look at the below examples.

Make sure to check out the [Exceptionless.JavaScript GitHub Repo](https://github.com/exceptionless/Exceptionless.JavaScript) for the latest examples and documentation.

### Sending Log Messages, Feature Usages, etc

```js
var client = require('exceptionless.node').ExceptionlessClient.default;

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

In addition to automatically sending all unhandled exceptions, you can also manually send events to Exceptionless using our fluent [event builder API](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.6.4/src/EventBuilder.ts).

The below example demonstrates sending a new error, "test," and setting the ReferenceID, Order and Quote properties, Tags, Geo, UserIdentity, and marking it as Critical.

```js
var client = require('exceptionless.node').ExceptionlessClient.default;

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

## Express.js Support

If you are using [Express.js](http://expressjs.com/) to develop a web application, you can add Exceptionless and start collecting unhandled errors and 404s very quickly. To start, just add the following middleware to the bottom of your middleware definitions.

```js
// This middleware processes any unhandled errors that may occur in your middleware.
app.use(function(err, req, res, next) {
 client.createUnhandledException(err, 'express').addRequestInfo(req).submit();
 res.status(500).send('Something broke!');
});

// This middleware processes 404’s.
app.use(function(req, res, next) {
 client.createNotFound(req.originalUrl).addRequestInfo(req).submit();
 res.status(404).send('Sorry cant find that!');
});
```

## What Data is Collected?

The JavaScript/Node.js client is full featured, will collect all the information our other clients collect, and has a fluent API as shown above.

By default, we wire up to the processes' [uncaught exception handler](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.6.4/src/exceptionless.node.ts#L29-L31) to automatically send any unhandled exceptions to your Exceptionless dashboard. We also submit a log message if your app doesn't shut down properly via inspecting the [exit code](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v1.6.4/src/exceptionless.node.ts#L33-L81), which is very useful and lets you know what your app is doing. Additionally, any queued up events are processed and sent before your app closes.

Each event contains environment and request information, as well, rounding out the complete list of Exceptionless features that we have made available via the JavaScript client, making it a great error and event reporting/logging solution for all your Node.js projects.


![Exceptionless Node.js Event Environment Details](/assets/img/news/node-js-event-environment-details-150x150.png)
_Environment Details_




## Sample Express.js App


We have built a quick[Express.js sample app](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/express/app.js) that you can play around with to get an idea of how the Node.js JavaScript client works with Exceptionless.

**Run the sample app by following the steps below:**

  1. Install [Node.js](https://nodejs.org/)
  2. [Clone or download our repository from GitHub](https://github.com/exceptionless/Exceptionless.JavaScript).
  3. Navigate to the example\express folder via the command line (e.g., cd example\express)
  4. Run npm install
  5. Open app.js in your favorite text editor and set the [apiKey](https://github.com/exceptionless/Exceptionless.JavaScript/blob/master/example/express/app.js#L5-L6)..
  6. Run node app.js.
  7. Navigate to <http://localhost:3000> in your browser to view the express app.
  8. To create an error, navigate to <http://localhost:3000/boom>

## Troubleshooting

We recommend enabling debug logging by calling `client.config.useDebugLogger();`. This will output messages to the console regarding what the client is doing. Please [contact us by creating an issue on GitHub](https://github.com/exceptionless/Exceptionless.JavaScript/issues) if you need assistance or have any feedback for the project.

## Feedback

As we move forward towards version 1.0 of our JavaScript client, we are looking for any and all feedback, so please don't hesitate to [let us know what you think, report a bug, etc](https://github.com/exceptionless/Exceptionless.JavaScript/issues).

Thanks!
