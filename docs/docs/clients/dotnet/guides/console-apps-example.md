---
title: "Console Apps Example"
---

# Console Apps Example

Exceptionless runs in all types of .NET aplications. Let's take a look at how to get started with Exceptionless in a console application. First, we'll some configuration out of the way.

To use Exceptionless, add the Exceptionless namespace like this: `using Exceptionless;`

Once you've done that, be sure to define the Exceptionless client:

`var client = new ExceptionlessClient("YOUR API KEY");`

Now you can send events to Exceptionless like this:

`client.SubmitLog("Hello World!");`

Or you can capture exceptions like this:

```csharp
try {
    throw new Exception("MyApp error");
} catch (Exception ex) {
    // submit the exception to the Exceptionless server
    client.SubmitException(ex);
}
```

Because Exceptionless is designed to process events asynchronously in the background via a queue, you may need to make sure the event is processed before the app exits. If this is a requirement for your app, you can handle this situation by telling Exceptionless about it up front with `client.Startup();`, which means Exceptionless knows to force process any events in the queue before allowing the app to exit, or by calling `await client.ProcessQueueAsync();` before your application exists.

There's one additional configuration option that doesn't require defining the client first. If you use the Exceptionless default client, it takes care of of most things for you. Simply load up the Exceptionless default client by calling `Startup` with your API Key, and you're ready to go:

`ExceptionlessClient.Default.Startup("Your API Key");`

When you go this route, you can send exceptions to Exceptionless just by calling a `ToExceptionless()` method on the default Exceptionless client. It looks like this:

```csharp
// configure the default instance
ExceptionlessClient.Default.Startup("Your API Key");

try {
    throw new Exception("MyApp ToExceptionless error");
} catch (Exception ex) {
    // use ToExceptionless extension method. Uses ExceptionlessClient.Default and requires it to be configured.
    ex.ToExceptionless().Submit();
    // don't forget to call Submit.
}
```

Exceptionless supports a wide range of platforms. For a full list, see the [supported platforms page here](/docs/clients/dotnet/supported-platforms).

---

[Next > Web Server Example](/docs/clients/dotnet/guides/web-server-example)
