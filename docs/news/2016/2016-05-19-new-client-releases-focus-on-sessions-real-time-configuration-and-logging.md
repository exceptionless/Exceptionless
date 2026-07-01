---
title: "New Client Releases Focus on Sessions, Real Time Configuration, and Logging"
date: 2016-05-19
---

# New Client Releases Focus on Sessions, Real Time Configuration, and Logging

![net-3.5.0-js-1.4.0](/assets/img/news/net-3.5.0-js-1.4.0.png)Exceptionless.NET v3.5.0 and Exceptionless.JavaScript v1.4.0 have been released and they are chock full of **new features and feature improvements!**

There weren't a whole lot of bug fixes with these releases. Could it be that Exceptionless is working and we've squelched most of the bugs? We think so!

Notably, we focused on sessions, real time configuration, and logging, all of which we think you'll find super helpful.

Read on for details.

## Exceptionless .NET Client V3.5.0 and JavaScript Client V1.4.0 Release Notes

### Session Heartbeats No Longer Count Towards Plan Limits!

We've updated Session Heartbeats and Session End events to be sent through an optimized API end point, and they will no longer count towards your plane's event limits.

### Real Time Project Configuration Improvements

You can now exclude events by type and source, along with setting minimum log levels, **in real time**. So, for example, if you accidentally disable logging completely, you can **simply update your settings** and the updates will go into affect, restoring functionality!

UI support will be expanded moving forward, but for the time being here is an example project configuration.

#### Source

**Key:** `@@EVENT_TYPE:SOURCE  

`

**Value:** `false`

Any event type or source can be used, and wildcards `*` are supported, so you can ignore whole event types and set the source to `*`.

#### Disable Submissions of Events with Type Name

**Key:** `@@error:*MyCustomException`

**Value: **`false`

This configuration will disable submission of any error event with a stack trace containing an exception with a type name that ends with `MyCustomException`. You could also remove the wildcard and specify the full type name with the namespace.

#### Log Message Settings

**Key:** `@@log:*`

**Value:** `off`

Even though log types are special cased, they still accept rue or false values, so this example completely turns off log messages.

Other known values are `Trace`, `Debug`, `Info`, `Warn`, `Error`, `Fatal`, and `Off`.

#### Minimum Log Level

**Key: **`@@log:MyNamespace.EventPostsJob`

**Value: **`Info`

This example is setting the minimum log level for `EventPostsJob` to `Info`.

### Automatically Check for Updated Configuration Settings on Client Idle

We've implemented an a**utomatic recurring check for updated configuration settings** that occurs **two minutes** after the last event submission.

A few notes:

* Each configuration check **does not** count towards your account's plan limits.
* No user information will be sent - only the current configuration version.
* Nothing will be retrieved if no settings have been changed.

The automatic recurring configuration settings check **can be disable** by calling:

#### .NET

```csharp
client.Configuration.UpdateSettingsWhenIdleInterval = TimeSpan.Zero;
```

#### JavaScript

```javascript
client.config.updateSettingsWhenIdleInterval = -1;`
```

### New Easy Way to Exclude Events from Being Submitted in .NET

You can now define a simple `Func&lt;Event,bool&gt;` callback to stop events from being submitted in the .NET client.

For example, if I wanted to ignore any event with a `value` property of `2` I could use `client.Configuration.AddEventExclusion(e => e.Value.GetValueOrDefault() == 2);`

### Bug Fix for SettingsCollection Boolean Values Support in .NET Client

`SettingsCollection.GetBoolean(name)` was not supporting `` or `1` as boolean values, but now it does. We are also now supporting `yes` and `no` as valid values.

### JavaScript Client Passing Settings Bug

[@csantero](https://github.com/csantero) fixed an issue with passing settings to a new isntance of ExceptionlessClient - thanks!

### Improved Stacking of Angular Response Errors in JavaScript Client

^^^ Nothing much to say here - the header says it all!

## Feedback

We hope these new features and feature improvements help your Exceptionless experience, and we plan to continue to listen to user feedback and improve the system where improvements need to be made. To do that, though, we need to hear what you want out of the app. If you have a favorite feature request, or something that just bothers you, please let us know by dropping us a line on GitHub Issues under the appropriate repository, listed below, or just comment here and we'll figure it out!

* [.NET Client Feedback](https://github.com/exceptionless/Exceptionless.Net/issues/new)
* [JavaScript Client Feedback](https://github.com/exceptionless/Exceptionless.JavaScript/issues/new)
* [Exceptionless Feedback](https://github.com/exceptionless/exceptionless/issues/new)

Thanks for reading and have an awesome day!
