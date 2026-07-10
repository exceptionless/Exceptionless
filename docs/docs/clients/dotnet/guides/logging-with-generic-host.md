---
title: "Logging With Generic Host"
---

# Logging With Generic Host

Microsoft provides a useful tool for logging events called `Microsoft.Extensions.Logging`. You can read up on [how logging works with .NET Core here](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-5.0), but we'll cover how to set up Exceptionless as a logging provider to be used as a generic host.

To get started, you'll need to make sure you update your `appsettings.json` file for your project. Here's an example configuration that will allow you to use Exceptionless with .NET Core's generic host:

```json
    "Exceptionless": {
        "ApiKey": "YOUR API KEY"
    },
    "Logging": {
        "IncludeScopes": false,
        "LogLevel": {
            "Default": "Debug",
            "System": "Information",
            "Microsoft": "Information"
        }
    }
```

With that added, you can add the Exceptionless namespace to any file in your project with `using Exceptionless;`. This then allows you to utilize Exceptionless with dependency injection, or as we're covering here, as a generic host.

In your `Startup` method, you can read in your configuration file like this:

```csharp
var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
```

This tells .NET Core to use whatever settings you've provided in the `appsettings.json` file for the logger (it also sets up some other configurations). To ensure Exceptionless is used for logging, you will need to update the `ConfigureServices` method to this:

```csharp
services.AddLogging(b => b
                .AddConfiguration(Configuration.GetSection("Logging"))
                .AddDebug()
                .AddConsole()
                .AddExceptionless());
```

This is adding debugging capabilities, console logging, and Exceptionless to your generic host logging configuration.

If you are using a generic host in a web server application, you may want to capture more information about HTTP requests automatically. To do this, you'll need to edit the `ConfigureServices` method to include the following line:

`services.AddHttpContextAccessor();`

Finally, you'll need to tell the app itself to use Exceptionless. You can do this in your `Configure` method liket his:

`app.useExceptionless(Configuration);`

Now, you have access to send logs, exceptions, and messages to Exceptionless automatically through the generic host configuration. If you'd like to see a full, detailed example, [we have that here](https://github.com/exceptionless/Exceptionless.Net/blob/9e91a51c36d03fcc3bee79a8b6eaee3034ac78b4/samples/Exceptionless.SampleAspNetCore/Startup.cs).

### Exceptionless Configuration Options

One of the nice things about configuring Exceptionless through `appsettings.json` is you can set up some defaults that will apply to all events sent through to Exceptionless. Let's explore what that might look like. In your `appsettings.json` file, you can add the following to your `Exceptionless` property:

```json
"DefaultData": {
    "JSON_OBJECT": "{ \"Name\": \"John Doe\" }",
    "Boolean": true,
    "Number": 1,
    "Array": "1,2,3"
}
```

This is a very simple object that encapsulates default data that will be sent to Exceptionless with every event. The `DefaultData` property can take in any property keys you'd like to pass in. The property values must be strings, booleans, numbers, or arrays. As you can see in the example, a JSON object can simply be stringified.

In addition to the `DefaultData` property, you can include `DefaultTags` and `Settings`. To include `DefaultTags`, add the following:

```json
"DefaultTags": [ "MySpecialTag" ]
```

As you can probably tell, you can pass in as many tags as you'd like as an array of strings.

To add custom settings, you would do something like this:

```json
"Settings": {
    "FeatureXYZEnabled": false
}
```

The `Settings` property can take any keys you'd like. The values associated with those keys must be strings, numbers, or booleans.

You can, of course, customize the default logging, but that is outside the Exceptionless configuration. If you'd like to customize the way things are logged when using the generic host, [follow this guide](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-5.0#configure-logging).

---

[Next > Sending Events](/docs/clients/dotnet/sending-events)