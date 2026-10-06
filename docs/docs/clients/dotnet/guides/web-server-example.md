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

Register Exceptionless with your ASP.NET Core application builder, then enable the exception handler and Exceptionless middleware:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddExceptionless();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseExceptionless();
```

`AddExceptionless` reads the `Exceptionless` settings from the application configuration and registers the services needed to capture unhandled exceptions and request information. Configure those settings in `appsettings.json`:

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
