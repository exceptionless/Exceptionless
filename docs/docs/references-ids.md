---
title: "Reference Ids"
---

# Reference Ids

Reference Ids are unique identifiers that let you look up a submitted event. This is important because _event Ids are not created until after an event is processed, so there's no other way to look up an event_.

Reference Ids are also used to help deduplicate events on the server side.

## Uses

One example of using Reference Ids is to help support your users. For instance, we always include a Reference Id with every error message for our users, allowing them to contact us with that Reference Id and receive help faster because we can easily track it down.

## Reference Id Example

To attach Reference Ids to our errors in Exceptionless, we register a default Reference Id plugin that sets a Reference Id when the event is submitted and stores the Id in an implementation of ILastReferenceIdManager. With the default plugin, we enable this behavior by calling `UseReferenceIds()` on the configuration object.

## C# Example

```csharp
using Exceptionless;
ExceptionlessClient.Default.Configuration.UseReferenceIds();
```

## JavaScript Example

The JavaScript client will automatically manage reference ids.

You can also create your own plugin to create your own Reference Ids.

## Get Last Used Reference Id

Call `GetLastReferenceId()` on the `ExceptionlessClient` instance.

### C# Last Reference Id Example

```csharp
using Exceptionless;
// Get the last created Reference Id
ExceptionlessClient.Default.GetLastReferenceId();
```

### JavaScript Last Reference Id Example

```javascript
// Get the last created Reference Id
exceptionless.ExceptionlessClient.default.getLastReferenceId();
```

## Displaying Reference Ids to End Users

We do this for our end users because it allows them to better support their app users.

To do so, we add a custom `IExceptionHandler` and return a new error response to include the Reference Id as shown below:

```csharp
public class ExceptionlessReferenceIdExceptionHandler : IExceptionHandler {
    public Task HandleAsync(ExceptionHandlerContext context, CancellationToken cancellationToken) {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        var exceptionContext = context.ExceptionContext;
        var request = exceptionContext.Request;
        if (request == null)
            throw new ArgumentException($"{typeof(ExceptionContext).Name}.{"Request"} must not be null", nameof(context));

        context.Result = new ResponseMessageResult(CreateErrorResponse(request, exceptionContext.Exception, HttpStatusCode.InternalServerError));
        return TaskHelper.Completed();
    }

    private HttpResponseMessage CreateErrorResponse(HttpRequestMessage request, Exception ex, HttpStatusCode statusCode) {
        HttpConfiguration configuration = request.GetConfiguration();
        HttpError error = new HttpError(ex, request.ShouldIncludeErrorDetail());

        string lastId = ExceptionlessClient.Default.GetLastReferenceId();
        if (!String.IsNullOrEmpty(lastId))
            error.Add("Reference", lastId);

        // CreateErrorResponse should never fail, even if there is no configuration associated with the request
        // In that case, use the default HttpConfiguration to con-neg the response media type
        if (configuration == null) {
            using (HttpConfiguration defaultConfig = new HttpConfiguration()) {
                return request.CreateResponse(statusCode, error, defaultConfig);
            }
        }

        return request.CreateResponse(statusCode, error, configuration);
    }
}
```

Then we replace the existing `IExceptionFilter`

```csharp
Config.Services.Replace(typeof(IExceptionHandler), new ExceptionlessReferenceIdExceptionHandler());
```

Now you'll get a user friendly error response that contains a Reference Id, like:

`{
  "message": "An error has occurred.",
  “reference”: “411085622e”
}`

## Looking up Events by Reference Id

Link directly to an event by outputting a link in your UI or log files, like
`https://be.exceptionless.io/event/by-ref/YOUR_REFERENCE_ID)`

Or you can search via the api/ui with `reference:YOUR_REFERENCE_ID`

---

[Next > User Sessions](/docs/user-sessions)
