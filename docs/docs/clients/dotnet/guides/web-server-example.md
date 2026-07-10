---
title: "Web Server Example"
---

# Web Server Example

Exceptionless runs great in all sorts of environments. Let's take a look at how you might set up Exceptionless to work with your .NET web server.

To get started, be sure to include the Exceptionless namespace wherever you plan to use it. You can do that like this: `using Exceptionless;`

The simplest example of using Exceptionless in your web server is to include a try/catch block that leverages Exceptionless in the catch. It might look something like this:

```csharp
[HttpGet("{id}")]
public ActionResult<User> GetUser(string id)
{
    try {
        var user = userService.GetUser(id);
        return Ok(user);
    } catch (Exception ex) {
        ex.ToExceptionless().SetProperty("UserId", id).Submit();
        return NotFound();
    }
}
```

Should the request to `FetchUser()`, or whatever your method is, happen to throw, the Exceptionless client will pick it up and send the exception to your dashboard.

Of course, Exceptionless is more than just error handling. You can leverage any of the Exceptionless event methods [documented here](/docs/clients/dotnet/sending-events) through the client interface.

Exceptionless can be configured as a generic host for your web server. In your `Startup.cs` file, you would include the following within the `ConfigureServices` method:

```csharp
services.AddHttpContextAccessor();
```

By adding this helper method, Exceptionless is able to gather more information about the request including the API endpoint that threw the error, user-agent information, and more.

Then in your `Configure` method, you would add:

```csharp
app.UseExceptionless(Configuration);
```

To get access to your Exceptionless configuration (which we'll explain next), you'll need to do create a `builder` variable in your `Startup` method and build the configuration like this:

```csharp
var builder = new ConfigurationBuilder()
        .SetBasePath(env.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
Configuration = builder.Build();
```

This gives your server application access to any configuration you've set in your `appsettings.json` file. And that's exactly where we will configure Exceptionless. So, go ahead and open that file and we can create some configuration for your Exceptionless client:

```json
 "Exceptionless": {
    "ApiKey": "YOUR API KEY",
    "ServerUrl": "http://localhost:5200",
    "DefaultData": {
        "JSON_OBJECT": "{ \"Name\": \"Alice\" }",
        "Boolean": true,
        "Number": 1,
        "Array": "1,2,3"
    },
    "DefaultTags": [ "SOME_TAG" ],
    "Settings": {
        "FeatureXYZEnabled": false
    }
},
```

You will only pass in the `ServerUrl` if you are self-hosting Exceptionless. You'll use this to point to your correct URL. The `DefaultData` is metadata you'd like associated with every event you send to Exceptionless.

With this configured, you can now call the Exceptionless client from anywhere in your server application without first defining the client.

This is just one example of one platform Exceptionless supports. But Exceptionless supports a wide range of platforms. For a full list, see the [supported platforms page here](/docs/clients/dotnet/supported-platforms).

---

[Next > Logging With Generic Host](/docs/clients/dotnet/guides/logging-with-generic-host)
