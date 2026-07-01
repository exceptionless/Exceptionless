---
title: "Sending Events"
order: 4
---

# Sending Events

Once [configured](/docs/clients/dotnet/configuration), Exceptionless will automatically send any unhandled exceptions that happen in your application. The sections below will show you how to send us different event types as well as customize the data that is sent in.

You may also want to send us **log messages**, **feature usages** or other kinds of events. You can do this very easily with our fluent api. Find examples, below.

- [Log Messages, Feature Usages, 404, and Custom Event Types](#log-messages-feature-usages-404-and-custom-event-types)
- [Manually Sending Errors](#manually-sending-errors)
- [Sending Additional Information](#sending-additional-information)
- [Modifying Unhandled Exception Reports](#modifying-unhandled-exception-reports)
- [Using NLog or Log4net Targets](#using-nlog-or-log4net-targets)

***

## Log Messages, Feature Usages, 404, and Custom Event Types

```csharp
// Import the exceptionless namespace.
using Exceptionless;

// Submit logs
ExceptionlessClient.Default.SubmitLog("Logging made easy");

// You can also specify the log source and log level.
// We recommend specifying one of the following log levels: Trace, Debug, Info, Warn, Error
ExceptionlessClient.Default.SubmitLog(typeof(Program).FullName, "This is so easy", "Info");
ExceptionlessClient.Default.CreateLog(typeof(Program).FullName, "This is so easy", "Info").AddTags("Exceptionless").Submit();

// Submit feature usages
ExceptionlessClient.Default.SubmitFeatureUsage("MyFeature");
ExceptionlessClient.Default.CreateFeatureUsage("MyFeature").AddTags("Exceptionless").Submit();

// Submit a 404
ExceptionlessClient.Default.SubmitNotFound("/somepage");
ExceptionlessClient.Default.CreateNotFound("/somepage").AddTags("Exceptionless").Submit();

// Submit a custom event type
ExceptionlessClient.Default.SubmitEvent(new Event { Message = "Low Fuel", Type = "racecar", Source = "Fuel System" });
```

## Manually Sending Errors

In addition to automatically sending all unhandled exceptions, you may want to manually send exceptions to the service. You can do so by importing the Exceptionless namespace and using code like this:

```csharp
try {
    throw new ApplicationException(Guid.NewGuid().ToString());
} catch (Exception ex) {
    ex.ToExceptionless().Submit();
}
```

## Sending Additional Information

You can easily include additional information in your error reports using our fluent event builder API.

```csharp
try {
    throw new ApplicationException("Unable to create order from quote.");
} catch (Exception ex) {
    ex.ToExceptionless()
        // Set the reference id of the event so we can search for it later (reference:id).
        // This will automatically be populated if you call ExceptionlessClient.Default.Configuration.UseReferenceIds();
        .SetReferenceId(Guid.NewGuid().ToString("N"))
        // Add the order object but exclude the credit number property.
        .AddObject(order, "Order", excludedPropertyNames: new [] { "CreditCardNumber" }, maxDepth: 2)
        // Set the quote number.
        .SetProperty("Quote", 123)
        // Add an order tag.
        .AddTags("Order")
        // Mark critical.
        .MarkAsCritical()
        // Set the coordinates of the end user.
        .SetGeo(43.595089, -88.444602)
        // Set the user id that is in our system and provide a friendly name.
        .SetUserIdentity(user.Id, user.FullName)
        // Set the users description of the error.
        .SetUserDescription(user.EmailAddress, "I tried creating an order from my saved quote.")
        // Submit the event.
        .Submit();
}
```

## Modifying Unhandled Exception Reports

You can get notified, add additional information or ignore unhandled exceptions by wiring up to the `SubmittingEvent` event.

```csharp
// Wire up to this event in somewhere in your application's startup code.
ExceptionlessClient.Default.SubmittingEvent += OnSubmittingEvent;

private void OnSubmittingEvent(object sender, EventSubmittingEventArgs e) {
    // Only handle unhandled exceptions.
    if (!e.IsUnhandledError)
        return;

    // Ignore 404s
    if (e.Event.IsNotFound()) {
        e.Cancel = true;
        return;
    }

    // Get the error object.
    var error = e.Event.GetError();
    if (error == null)
        return;

    // Ignore 401 (Unauthorized) and request validation errors.
    if (error.Code == "401" || error.Type == "System.Web.HttpRequestValidationException") {
        e.Cancel = true;
        return;
    }

    // Ignore any exceptions that were not thrown by our code.
    var handledNamespaces = new List<string> { "Exceptionless" };
    if (!error.StackTrace.Select(s => s.DeclaringNamespace).Distinct().Any(ns => handledNamespaces.Any(ns.Contains))) {
        e.Cancel = true;
        return;
    }

    // Add some additional data to the report.
    e.Event.AddObject(order, "Order", excludedPropertyNames: new [] { "CreditCardNumber" }, maxDepth: 2);
    e.Event.Tags.Add("Order");
    e.Event.MarkAsCritical();
    e.Event.SetUserIdentity(user.EmailAddress);
}
```

## Using NLog or Log4net Targets

Using major logging frameworks like NLog or Log4net gives you more granular control over what's logged.

To use the [NLog](https://www.nuget.org/packages/exceptionless.nlog) or [Log4net](https://www.nuget.org/packages/exceptionless.log4net) clients, bring down the NuGet package and follow the detailed readme. You can also take a look at our [sample app](https://github.com/exceptionless/Exceptionless.Net/tree/master/samples/Exceptionless.SampleConsole), which uses both frameworks.

**Performance Note**
If you are logging thousands of messages a minute, you should use the in-memory event storage (below). This way, the client won't serialize the log events to disk, thus is much faster. This does mean, however, that if the application dies you will lose unsent events in memory. When you use the NLog or Log4net targets and specify the API key as part of the target configuration, we will automatically create a second client instance that uses in-memory storage only for log messages. This way, any logged exceptions or feature usages still use disk storage, while log messages use in-memory storage, allowing maximum performance.

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.UseInMemoryStorage();
```

---

[Next > Supported Platforms](/docs/clients/dotnet/supported-platforms)
