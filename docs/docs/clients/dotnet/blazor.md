---
title: "Blazor WebAssembly"
description: "Report errors from a Blazor WebAssembly app with the core Exceptionless .NET package, including exceptions caught by an error boundary."
sources:
  - Exceptionless.Net:samples/Exceptionless.SampleBlazorWebAssemblyApp/Program.cs
  - Exceptionless.Net:samples/Exceptionless.SampleBlazorWebAssemblyApp/Pages/Counter.razor
  - Exceptionless.Net:samples/Exceptionless.SampleBlazorWebAssemblyApp/Exceptionless.SampleBlazorWebAssemblyApp.csproj
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Submission/DefaultSubmissionClient.cs
  - Exceptionless.Net:src/Exceptionless/Dependency/DependencyResolver.cs
---

# Blazor WebAssembly

A Blazor WebAssembly app runs in the browser, so it uses the core `Exceptionless` package rather than the ASP.NET Core
integration. You submit errors from your components and from an error boundary, and the client sends them from the
browser.

Blazor Server and the server half of a Blazor Web App run in ASP.NET Core. Set those up with
[ASP.NET Core](/docs/clients/dotnet/aspnet-core/).

## Install

```bash
dotnet add package Exceptionless
```

## Configure

Start the client at the top of `Program.cs`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleBlazorWebAssemblyApp/Program.cs -->

```csharp
using Exceptionless;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyBlazorApp; // your app's root namespace, for App

ExceptionlessClient.Default.Startup("YOUR_API_KEY");

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await builder.Build().RunAsync();
```

The app is downloaded to the browser, so anyone can read the API key in it. Use a key for this project only, and see
[Projects](/docs/project-settings/) to create and disable keys.

> [!NOTE]
> If you self-host Exceptionless, set `ExceptionlessClient.Default.Configuration.ServerUrl` to your instance's URL
> before calling `Startup()`. Your instance must be reachable from your users' browsers.

## Report errors from components

Blazor catches exceptions thrown while rendering a component or handling an event, so they don't reach the client's
unhandled exception handler. Report them yourself.

Submit exceptions you catch:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleBlazorWebAssemblyApp/Pages/Counter.razor -->

```html
@page "/counter"
@using Exceptionless

<button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

@code {
    private int currentCount = 0;

    private void IncrementCount() {
        try {
            throw new InvalidOperationException("Exceptionless test error");
        } catch (Exception ex) {
            ex.ToExceptionless().AddTags("blazor").Submit();
        }

        currentCount++;
    }
}
```

To report everything else, wrap your layout's body in an error boundary that submits what it catches. Create
`ExceptionlessErrorBoundary.cs`:

```csharp
using Exceptionless;
using Microsoft.AspNetCore.Components.Web;

public class ExceptionlessErrorBoundary : ErrorBoundary {
    protected override Task OnErrorAsync(Exception exception) {
        exception.ToExceptionless().AddTags("blazor", "error-boundary").Submit();
        return base.OnErrorAsync(exception);
    }
}
```

Then use it in `MainLayout.razor` in place of the built-in `ErrorBoundary`:

```html
<main>
  <ExceptionlessErrorBoundary>
    @Body
  </ExceptionlessErrorBoundary>
</main>
```

## Verify

Run the app, open the counter page, and click the button. Open **Stacks** in the app. The error appears within a few
seconds.

## What is captured automatically

- **Exceptions you submit**, with the exception type, message, and stack trace.
- **Unhandled exceptions** that reach `AppDomain.UnhandledException` or go unobserved in tasks.

The client keeps its queue in memory, so events that haven't been sent yet are lost if the user closes the tab. Request
details, the browser, and the page URL are not captured. To record which user hit an error, call
`ExceptionlessClient.Default.Configuration.SetUserIdentity(userId, name)` after sign-in (see
[Sending events](/docs/clients/dotnet/sending-events/#user-identity)).

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): feature usage, custom data, and user identity.
- [JavaScript client](/docs/clients/javascript/): for JavaScript errors in the same page.

## Ask Exie

```prompt exie
Show me the open stacks tagged blazor, newest first.
```

```prompt exie
How many error-boundary errors happened each day this week?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the Exceptionless stacks tagged error-boundary in my project, read their stack traces, find the components in this Blazor app that throw them, and fix them.
```
