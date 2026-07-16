---
title: "Sending Log Messages to Exceptionless"
---

# Sending Log Messages to Exceptionless

![Exceptionless Log Message Events](/assets/img/news/log-messages.png)We're all about exceptions, but sometimes, as developers, we run into bugs that don't throw them but still cause major havoc in the system. There are also times we just need to record an event with a custom message to help track down bottlenecks, etc.

That's where Exceptionless meets log messages.

In Exceptionless 2.0, you can now send custom log messages to a Log Messages dashboard where they can be tracked and view just like exceptions and other events.

Lets take a closer look...

## How to Submit a Log Message to Exceptionless

### Using the Client

For this example, we are assuming that you have already created an account and installed and [configured](/docs/clients/dotnet/configuration) the Exceptionless 2.0 client. If you are still using the 1.x client, you will need to [upgrade](/docs/self-hosting/upgrading-self-hosted-instance) to send us log messages. Please contact support via an in-app support message or our [contact page](/contact/) if you have any questions or need assistance in this area.

#### Let's submit a log message

```cs
using Exceptionless;
ExceptionlessClient.Default.SubmitLog("Application starting up");
```

That's your basic log message.

#### **Getting fancier**, you can also specify the log source and log level.

In the below example, "_typeof(MainWindow)__.FullName_" specifies the log source, and "_Info_" specifies the log level. If no log source is specified, the log messages will be stacked under a global log stack.

We recommend specifying one of the following log levels, all of which add a visual indicator to each log message (see below screenshot).

* Trace
* Debug
* Info
* Warn
* Error

```cs
ExceptionlessClient.Default.SubmitLog(typeof(MainWindow).FullName, "Info log example", "Info");
```

Here's a screenshot of what the visual indicators for the different types of log levels look like.

![Exceptionless Log Message Preview](/assets/img/news/log-messages-log-level.png)

#### You can also add additional information using our fluent API.

This is helpful wen you want to add contextual information, contact information, or a tag.

In the below example, we will use the "_CreateLog_" method to add a tag to the log message.

```cs
using Exceptionless;
ExceptionlessClient.Default.CreateLog(typeof(MainWindow).FullName, "Info log example", "Info").AddTags("Wpf").Submit();
```

There are a number of additional pieces of data you can use for your event. The below bullets include the current EventBuilder list, but we are always adding more that can be found on [GitHub](https://github.com/exceptionless/Exceptionless.Net/blob/master/src/Exceptionless/EventBuilder.cs). Also, view more examples here on our [Sending Events](/docs/api/posting-events) page.

* AddTags
* SetProperty
* AddObject
* MarkAsCritical
* SetUserIdentity
* SetUserDescription
* SetVersion
* SetSource
* SetSessionId
* SetReferenceId
* SetMessage
* SetGeo
* SetValue
* And more, depending on your client

### Using the REST API

You can also submit a log message with an HTTP post to our [events endpoint](https://api.exceptionless.io/docs/index.html#!/Event/Event_Post).

```
My log message
My second log message.
```

By default, any content that is submitted to the API post is a log message. The above example will be broken into two log messages because it automatically splits text content by the new line.

#### You can submit a log message via JSON, too.

See details in our [API Documentation](https://api.exceptionless.io/docs/index.html#!/Event/Event_Post). If you need an API key for simply posting events, you can find it in your [project settings](https://be.exceptionless.io/). Otherwise, please refer to the [auth login documentation](https://api.exceptionless.io/docs/index.html#!/Auth/Auth_Login) to get a user scoped api key.

Below is a JSON example of a log message, with source, message, and log level.

```json
{
  "type": "log",
  "source": "WpfApplication3.MainWindow",
  "message": "Application started",
  "data": {
    "@level": "Info",
  }
}
```

### Using NLog or Log4net Targets

We also have integrations with major logging frameworks. The benefits of using a logging framework is finer, more granular control over what is logged.

For example, you can log only warnings or errors project-wide, but then enable trace level messages for a specific class. We do this to keep our system from getting filled up with noise that doesn't add any value unless there is an issue.

This also allows you to quickly change what you want to log to. Maybe you want to turn off logging to Exceptionless, or log to Exceptionless and to disk.

#### NLog or Log4net Usage

To use the [NLog](https://www.nuget.org/packages/exceptionless.nlog)or [Log4net](https://www.nuget.org/packages/exceptionless.log4net) clients, you’ll just need to bring down the NuGet package and follow the detailed readme.

_
**Note on performance: Use in-memory event storage for high volumes**
_

_There are some performance considerations you should be aware of when you are logging very high numbers of log events and using our client. We've spent a lot of time to ensure Exceptionless doesn't degrade your applications performance. However, if you are logging thousands of messages a minute, you should use the in-memory event storage._

```cs
using Exceptionless;
ExceptionlessClient.Default.Configuration.UseInMemoryStorage();
```

This tells the client not to serialize the log events to disk before sending and thus is much faster (the client doesn't need to serialize the event to disk and read it from disk before sending). This comes at the expense that if the application dies, you will lose any unsent events that were in memory. When you use the NLog or Log4net targets and specify the API key as part of the target configuration, we will automatically create a second client instance that uses in-memory storage only for log messages. This way, any logged exceptions or feature usages still use disk storage, while log messages use in-memory storage, allowing maximum performance.

**Another Note:** We are also working on updating the [Serilog implementation](https://github.com/serilog/serilog/issues/381).

## Log Messages Dashboard

The Log Messages Dashboard makes it easy to see log occurrences. You can keep track of how active a component is, or how long code takes to execute using existing metrics.

For metrics, we have created an [open source metrics library called Foundatio](https://github.com/FoundatioFx/Foundatio). We use it for Exceptionless, and think you will find it extremely helpful as well. Check it out! We'll be posting an article on Foundatio soon, so check back.

## ![log-messages-dashboard](/assets/img/news/log-messages-dashboard.png)

How Are You Liking Exceptionless 2.0?

We think we've added some pretty cool features to Exceptionless 2.0, including logging, but you are the ultimate judge. What's good? What's bad? What's missing? Let us know via a comment on the blog, social media, in-app, or however else you want to get in touch!

If you're new to Exceptionless 2.0, make sure to [check out the launch article](/news/2015/2015-03-12-its-go-time-exceptionless-2-0-launched) for more details on the new features and enhancements.
