---
title: "Real Time App Configuration and Event Settings with Exceptionless"
---

# Real Time App Configuration and Event Settings with Exceptionless

![exceptionless-project-settings-header](/assets/img/news/exceptionless-project-settings-header.png)

## Bet You Didn't Know Exceptionless Could Do This...

Have you ever needed to **cut through the noise and just focus on one type of event** _(in real time)_, such as only error logs, to track down a bug?

Do you want to **limit certain types of event reporting** _(in real time)_ to save your event quota and limit clutter?

What about **controlling your application's settings or features** _**in real time**_ via Exceptionless, without having to update your files and deploy your app!?

Well, with our client configuration settings, **you can do all that, and more**, in **real time**, on a per-project basis via your Exceptionless dashboard!

## How Could This Help Me?

We'll talk details, below, but first lets look at a few scenarios where the above could be useful.

### Scenario 1 - Way too many events!

Let's say you're on the small plan, and you've got a bunch of warning log events clogging your system that you know about and are working on, but they are pushing you over your plan limits.

#BOOM - set a minimum log level of error, and Exceptionless won't report those warnings anymore and they won't count against your plan limits! This is a great way to get the most out of your Exceptionless plan.

### Scenario 2 - This Authentication Issue is KILLING Me!

Maybe you're having major issues with an authentication bug, but you've already set minimum log levels to only include errors. Well, now you also want to see the trace values for those events without opening the flood gates for every event by removing your minimum log level.

#NOPROBLEM - Just add a key for authentication that just lets trace events through!

### Scenario 3 - You Said Something About Controlling My App's Features In Real Time?

Yup! Our client configurations are basically just a key value pair dictionary, but what makes them powerful and helps them control **your** application's features is that they get updated in nearly real-time, meaning you can build settings, features, etc into your app that react to value changes, and if you change that configuration setting in Exceptionless, your app will react almost instantly!

This can be super useful, especially if changing your app's settings would normally require you to deploy to production. No need - just use Exceptionless!

## Primer: How Project Settings Work

First, settings updates **do not count towards plan limits**. We say that because each time an event is submitted, we send a response header with the current configuration version to the client, and if a newer version is available, it is retrieved and the latest configuration is applied. That means that config changes are nearly **real time.**

When the client is idle, we also check for config changes, including five seconds after client startup if no events are submitted at startup, and every two minutes after the last event submission.

If the version hasn't changed, nothing is retrieved, **limiting data transfer,** and no user information is ever sent when checking.

### Turning Off Automatic Updating

If you do not want the configuration settings to update when idle, you can turn off automatic updates. To do so, please follow the respective [.NET](/docs/clients/dotnet/client-configuration-values#updating-client-configuration-settings) or [JavaScript/Node.js](/docs/clients/javascript/client-configuration-values#updating-client-configuration-settings) documentation.

## The Main Event: Client Configuration

![client-configuration](/assets/img/news/client-configuration-300x143.png)

Exceptionless client configurations are a dictionary of key value pairs that can be used to control the behavior of your app in real time by doing things like controlling data exclusions, protecting sensitive data, enabling and disable features, or disabling certain types of events (`error`, `usage`, `log`, `404`, or `session`).

We also have some built in configuration key naming conventions (`@@EVENT_TYPE:SOURCE`) that the clients recognize for ignoring events based on event type and event source. Just replace `EVENT_TYPE` part with the event type (E.G., `error`, `log`...) and the `SOURCE` (E.G., exception type or log source) you'd like the setting to apply to. Next, specify key value of `false` to discard matching events client side. It's worth noting that  `log` event types can also accept a log level value (E.G., `Trace`, `Debug`, `Info`, `Warn`, `Error`, or `Fatal`).

For example, we can use it to turn off all error events of type, lets say, `System.ArgumentNullException`, by using the key `@@error:System.ArgumentNullException` and the value `false`.

Or, we could turn off all error events entirely by using the `*` wildcard. So, the key would be `@@error:*` and the value would be `false` again.

### Examples for the Above Scenarios

#### Scenario 1

In scenario 1, above, we were trying to save our plan limits by limiting log events to only errors. So our Client Configuration key would simply be `@@log:*` with value `Error`.

#### Scenario 2

Here we already limited our log events to errors, but now we're troubleshooting a specific issue with authentication (let's say we're using the AuthController API), so we want to look at the trace messages coming through. We can override any general minimum log levels that we've defined by setting a level for a specific log source. So, all we would do is add the `@@log:*AuthController` key with value `Trace`! Then, when the bug's fixed, turn it off as needed.

#### Scenario 3

This is the cool one. Here you are wanting to, let's say, pass a value for a setting in your app that turns something on or off without having to re-deploy everything. This is super easy to accomplish all we need to do is create a setting which will control our feature! Let's assume we have have a feature flag to show a welcome screen. We will name this feature flag `enableWelcomeScreen` and create a new configuration setting respectively with a value of `true` (_You can change this value at any time_). These changes will be pushed based on the above "How Project Settings Work" section automatically, all we have to do is check the setting as shown below.

#### C#

```cs
using Exceptionless;
// Check the configuration settings for our enableWelcomeScreen feature flag with a default value of false.
if (ExceptionlessClient.Default.Configuration.Settings.GetBoolean("enableWelcomeScreen", false)) {
  // Show the welcome screen!
}
```

#### JavaScript

```js
// Check the configuration settings for our enableWelcomeScreen feature flag
if (exceptionless.ExceptionlessClient.default.config.settings['enableWelcomeScreen'] === true) {
  // Show the welcome screen!
}
```

Pretty cool, right!

For more details on client configuration, check out the [Client Configuration Project Settings documentation](/docs/project-settings#client-configuration). Specific usage examples can be found on the [.NET](/docs/clients/dotnet/client-configuration-values) and [JavaScript/Node.js](/docs/clients/javascript/client-configuration-values) documentation pages respectively.

## Other Project Settings You Might Find Useful

### General

![general](/assets/img/news/general-300x132.png)

If you go to Admin > Projects in Exceptionless, you can choose the project you would like to edit the settings for. Each project can have unique settings.

The default tab is "General," which simply houses the project name and attached organization. Nothing fancy here - pretty self explanatory.

### API Keys

![exceptionless api keys](/assets/img/news/api-keys-300x127.png)

This tab is where you can generate an API key for your project. Again, pretty self explanatory. Hit "New API Key" and one gets generated. For more details on API usage, check out the [API Usage documentation](/docs/api/) on GitHub.

### Settings

![exceptionless project settings](/assets/img/news/settings-300x281.png)

This is where you can set data exclusions, customize error stacking, and build in some spam detection to your project.

#### Data Exclusions

There are several use cases where you might not want to send some data to your Exceptionless project. This field allows you to enter a comma delimited list of field names that will be removed and not transferred to Exceptionless. The perfect example here is a password field, or other personal and sensitive data.

The `*` wildcard is supported in this field and can be used at the end (`password*`), beginning(`*password`), or on both sides (`*password*`) of the field name to further customize your data exclusion.

#### Error Stacking

Control over error stacking is another level of customization project settings allows. You can specify user namespaces or common methods to ignore. More details below.

**User Namespaces**

Here you can enter a comma delimited list of namespace names that own your application. With those in place, the only methods that will be considered stacking targets are ones inside those namespaces.

**Common Methods**

If your code has shared utility methods that may generate a bunch of errors, this could be useful. Enter a comma delimited list of common method names that shouldn't be used as stacking targets, and they will be ignored.

#### Spam Detection

Spam is the worst. So, we added a "Spam Detection" list of common user agents that should be ignored, which you can add to as you see fit. This eliminates a lot of noise, and can be customized to help trim even more depending on your application.

Along with the comma delimited list of user agents to ignore, you can also tick the box that says "Reduce noise by automatically hiding high volumes of events coming from a single client IP address." This can ward off large numbers of events being submitted by a spammer or attack on your app.

#### Integrations

![exceptionless integrations](/assets/img/news/integrations-300x143.png)

Integrations with tools like Slack, HipChat, JIRA, Basecamp, and others are very popular and can add a level of automated notifications, etc, to your workflow. So, on the integrations tab of your project's configuration you can create web hooks to integrate with your service or others as mentioned. Each web hook has a URL that it can call, and options for when it should be called. When a selected event occurs, a POST request is submitted with either event or stack data in JSON format. For more details and sample data, visit the [Exceptionless integrations documentation](/docs/integrations).

## That About Covers It!

We hope this will help you make the most out of your Exceptionless projects, allowing you to save some event submissions, get that customization you were looking for, etc.

Please let us know if you have any questions, comments, concerns, bugs, or anything else we can help with!
