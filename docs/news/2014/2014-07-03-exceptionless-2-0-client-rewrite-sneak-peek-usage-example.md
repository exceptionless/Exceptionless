---
title: "Exceptionless 2.0 Client Rewrite Sneak Peek Usage Example"
date: 2014-07-03
---

# Exceptionless 2.0 Client Rewrite Sneak Peek Usage Example

![new-client-header](/assets/img/news/new-client-header.jpg)

As Exceptionless 2.0 continues to become a reality, we thought we would give everyone a little taste of what you will be able to do with the new, rewritten client. Continue reading for a glimpse at the primary features, along with a complete usage example for adding extra data to events.

After you check it out, let us know if you have questions or suggestions. We're listening!

## New Client Features

* The Exceptionless client has been completely rewritten to be highly simplified and extensible.
* Will work with Mono and Project K.
* The base client is [PCL](https://www.nuget.org/packages/exceptionless.portable), and we will have platform specific clients that add additional functionality for each platform.
* Adding extra data to events is extremely easy.

**[View Client Source](https://github.com/exceptionless/Exceptionless.net)**

### Extended event data usage example

![Exceptionless Code Example](/assets/img/news/ex-client-1024x420.png)client source&lt;/a&gt; if you want to take a look at the complete code.

**First, set your API key.**

```cs
var client = new ExceptionlessClient(config => {
config.ApiKey = "API_KEY_HERE";
```

* * *

Then, send events to your own free Exceptionless server install.

```cs
config.ServerUrl = "https://exceptionless.myorg.com";
```

* * *

Now, read config settings from attributes.

```cs
config.ReadFromAttributes();
```

* * *

Read config settings from a config section in your app/web.config.

```cs
config.ReadFromConfigSection();
```

* * *

Store all client data including the offline queue in the store folder, by default isolated storage is used.

```cs
config.UseFolderStorage("store");
```

* * *

Exclude any form fields, cookies, query string parameters, and custom data properties containing "CreditCard".

```cs
config.AddDataExclusions("CreditCard");
```

* * *

Add the "SomeTag" to all events.

```cs
config.DefaultTags.Add("SomeTag");
```

* * *

Add the "MyObject" custom data object to every event.

```cs
config.DefaultData.Add("MyObject", new { MyProperty = "Value1" });
```

* * *

Add a custom event enrichment that will add a tag called "MyTag" to every event.

```cs
config.AddEnrichment(ev => ev.Tags.Add("MyTag"));
```

* * *

Register a custom log implementation that uses NLog.

```cs
config.Resolver.Register(new NLogExceptionlessLog());
```

* * *

The Startup method is specific for each platform and wires up to all relevant unhandled exception events so that they will be automatically sent to the server.

```cs
client.Startup();
```

* * *

Manually catch and report an error with a custom tag on it.

```cs
try {
    throw new ApplicationException("Boom!");
} catch (Exception ex) {
    ex.ToExceptionless().AddTags("MyTag").Submit();
}
```

* * *

Let users add their email address and a description of the error.

```cs
await client.UpdateUserEmailAndDescriptionAsync(client.GetLastReferenceId(), "me@me.com", "It broke!");
```

* * *

Create and submit a log message and add an extra "Order" object to the event.

```cs
client.CreateLog("Order", "New order created.")
    .AddObject(new { Total = 14.95 }, name: "Order")
    .Submit();
```

* * *

Submit a feature usage event that will let you see how much certain features of your app are being used.

```cs
client.SubmitFeatureUsage("FeatureA");
```

* * *

Submit a page not found event so you can keep track of your broken links and fix them.

```cs
client.SubmitNotFound("/badpage");
```

* * *

Listen to all events being sent and cancel any errors that are "IgnoredType".

```cs
client.SubmittingEvent += (sender, args) =>
args.Cancel = args.Event.IsError() && args.Event.GetError().Type.Contains("IgnoredType");
```

* * *

Settings data is synced in real-time with the project settings in your Exceptionless project on the server.

```cs
client.Configuration.Settings.Changed += (sender, args) =>
Trace.WriteLine(String.Format("Action: {0} Key: {1} Value: {2}", args.Action, args.Item.Key, args.Item.Value));
```

* * *

You can use those settings to control behavior in your app.

```cs
if (client.Configuration.Settings.GetBoolean("IncludeMyCustomData", false))
    Trace.WriteLine("Should include my custom data");
```

## That's All There Is To It!

After checking out the above example, we hope you agree that we've drastically simplified and improved the process of adding data to events, allowing for much more flexibility.

As always, if you have any questions, comments, suggestions, or concerns, let us know!

### Read more about Exceptionless 2.0

* [Exceptionless 2.0 - In the Making](/exceptionless-2-in-the-making/ "Exceptionless 2.0 – In the Making")
* [Event Based Reporting System](/event-based-reporting-system-coming-version-2-0/ "Event Based Reporting System Coming in Version 2.0")
* [Simplified API](/upcoming-exceptionless-2-0-simplified-api/ "More from the Upcoming Exceptionless 2.0: Simplified API")
* [A Pluggable System](/coming-exceptionless-2-0-pluggable-system/ "Coming in Exceptionless 2.0 – A Pluggable System")
