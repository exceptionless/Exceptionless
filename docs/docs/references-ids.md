---
title: "Reference IDs"
metaTitle: "Look up errors by reference ID"
description: "Show users a short reference ID when something fails, then find that exact event in Exceptionless. Includes an ASP.NET Core problem details example."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/event/by-ref/[referenceId]/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/overview.svelte
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Utility/Constraints/IdentifierRouteConstraint.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/05_CheckForDuplicateReferenceIdPlugin.cs
  - src/Exceptionless.Core/Pipeline/006_TruncateFieldsAction.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/90_RemovePrivateInformationPlugin.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/040_ReferenceIdPlugin.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/EventBuilder.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExceptionHandler.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExtensions.cs
  - Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Program.cs
  - Exceptionless.JavaScript:packages/core/src/plugins/default/ReferenceIdPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/EventPluginManager.ts
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
---

# Reference IDs

A reference ID is a short ID that a client gives an event before sending it. Show it to your users when something goes
wrong, and when they contact support with it, you can open the exact event in Exceptionless.

You need a reference ID for this because an event's own ID only exists after the server processes it, so your app can't
show it to the user.

## The support workflow

1. Your app turns on reference IDs. Each error event gets a 10-character ID, such as `9f4a1c2b3d`.
2. When a request or action fails, your app shows the reference ID in the error message or response.
3. The user sends the reference ID to your support team.
4. Support opens `https://be.exceptionless.io/event/by-ref/9f4a1c2b3d`, or searches for `reference:9f4a1c2b3d`, and sees
   the error with its stack trace, request, and user.

Optionally, the user can add their email address and a description of what they were doing, which shows on the event as
**User Description**. See [Let users describe what happened](#let-users-describe-what-happened).

## Turn on reference IDs

- **.NET:** call `UseReferenceIds()` on the configuration. It gives every error event a reference ID.
- **JavaScript:** error events get a reference ID by default.

Both clients only give **error** events a reference ID. To get the last one your app sent, call `GetLastReferenceId()`
in .NET or `getLastReferenceId()` in JavaScript.

You can also set your own with `SetReferenceId()` or `setReferenceId()` on the event builder, for example to reuse an ID
your app already logs. A reference ID must be 8 to 100 letters, digits, or dashes, and it must be unique: when two
events arrive with the same reference ID within a day, the server keeps the first and discards the second.

## Return the reference ID from ASP.NET Core

In an ASP.NET Core app, `builder.AddExceptionless()` registers an `IExceptionHandler` that reports unhandled exceptions.
This example adds the event's reference ID to the problem details response that `UseExceptionHandler()` returns. It
reads the reference ID from the event itself, so each response gets its own request's ID even under load.

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleAspNetCore/Program.cs, adapted. Verified against Exceptionless.AspNetCore 6.2.0. -->

```csharp
using Exceptionless;

var builder = WebApplication.CreateBuilder(args);

// Reads the API key from the "Exceptionless" section of appsettings.json.
builder.AddExceptionless(c =>
{
    // Give every error event a reference ID.
    c.UseReferenceIds();

    // Keep the reference ID on the current request so the error response can include it.
    c.AddPlugin("RequestReferenceId", 50, context =>
    {
        if (!String.IsNullOrEmpty(context.Event.ReferenceId)
            && context.ContextData.TryGetValue("HttpContext", out object? value)
            && value is HttpContext httpContext)
        {
            httpContext.Items["ExceptionlessReferenceId"] = context.Event.ReferenceId;
        }
    });
});

// Add the reference ID to the problem details response for unhandled exceptions.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.HttpContext.Items.TryGetValue("ExceptionlessReferenceId", out object? referenceId))
            context.ProblemDetails.Extensions["referenceId"] = referenceId;
    };
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseExceptionless();

app.MapGet("/", () =>
{
    throw new InvalidOperationException("Something went wrong.");
});

app.Run();
```

The plugin runs after the built-in reference ID plugin, which has priority 40. A request to `/` now returns:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "00-1e00f1e67addf9cabd5a4c15024de614-18b7dfcc2181ee03-00",
  "referenceId": "aa0310f620"
}
```

Your front end can show `referenceId` to the user, for example: "Something went wrong. If you contact support, include
reference aa0310f620."

For the rest of the ASP.NET Core setup, see [ASP.NET Core](/docs/clients/dotnet/aspnet-core/).

## Show the reference ID in a browser app

<!-- source: Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts (submitException, getLastReferenceId) -->

```ts
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup("YOUR_API_KEY")

try {
  await placeOrder()
} catch (error) {
  await Exceptionless.submitException(error as Error)
  const referenceId = Exceptionless.getLastReferenceId()
  showError(`Something went wrong. If you contact support, include reference ${referenceId}.`)
}
```

`placeOrder` and `showError` stand for your own code.

## Let users describe what happened {#let-users-describe-what-happened}

After an error, you can ask the user what they were doing and attach their answer to the event by its reference ID. The
email address and description show on the event's **Overview** tab under **User Description**.

<!-- source: Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs (UpdateUserEmailAndDescriptionAsync) and Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts (updateUserEmailAndDescription) -->

```csharp tab=".NET"
using Exceptionless;

await ExceptionlessClient.Default.UpdateUserEmailAndDescriptionAsync(
    referenceId, "jane@example.com", "I clicked Place order twice.");
```

```ts tab="JavaScript"
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.updateUserEmailAndDescription(referenceId, "jane@example.com", "I clicked Place order twice.")
```

```bash tab="curl"
curl -X POST "https://api.exceptionless.io/api/v2/events/by-ref/9f4a1c2b3d/user-description" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"email_address":"jane@example.com","description":"I clicked Place order twice."}'
```

If the project removes user identifiable information, the server drops the email address and keeps the description.

## Find an event by reference ID

- **Link:** open `https://be.exceptionless.io/event/by-ref/YOUR_REFERENCE_ID`. If one event matches, the app opens it.
  If several match, it lists them with **View In Events**. Your app or logs can print this link next to the error.
- **Search:** filter the **Events** view with `reference:YOUR_REFERENCE_ID`. See
  [Filtering and searching](/docs/filtering-and-searching/).
- **Command palette:** press <kbd>/</kbd> and type the reference ID to search events from the last 7 days.
- **API:** `GET /api/v2/events/by-ref/{referenceId}`. See [Querying events and stacks](/docs/api/getting-events/).

On an event's **Overview** tab, the **Reference** row shows its reference ID and links to the same lookup.

## Ask Exie

[Exie](/docs/ai/exie/) is on Medium plans and up.

```prompt exie
Find the error with reference ID 9f4a1c2b3d and explain what went wrong.
```

```prompt exie
How many times has the error behind reference 9f4a1c2b3d happened this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can take a support ticket from reference ID to
fix:

```prompt agent
A customer reported Exceptionless reference ID 9f4a1c2b3d. Find that event, read its stack trace and request, find the cause in this repository, and propose a fix.
```
