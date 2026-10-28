---
title: "Legacy ASP.NET"
description: "Add Exceptionless to ASP.NET MVC, Web API, WebForms, and WCF apps on .NET Framework 4.6.2 or later, configured with web.config."
sources:
  - Exceptionless.Net:src/Platforms/Exceptionless.Mvc/ExceptionlessModule.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Mvc/ExceptionlessSendErrorsAttribute.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Web/ExceptionlessModule.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Web/ExceptionlessClientExtensions.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Web/ExceptionlessWebPlugin.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Web/ExceptionlessWCFHandleErrorAttribute.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.WebApi/ExceptionlessWebApiExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessSection.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessAttribute.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessSettingAttribute.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/NuGet/tools/exceptionless.psm1
  - Exceptionless.Net:samples/Exceptionless.SampleMvc/Web.config
  - Exceptionless.Net:samples/Exceptionless.SampleMvc/Global.asax.cs
  - Exceptionless.Net:samples/Exceptionless.SampleWeb/Web.config
  - Exceptionless.Net:samples/Exceptionless.SampleWcf/Service1.svc.cs
---

# Legacy ASP.NET

These packages report unhandled exceptions and request details from ASP.NET apps on .NET Framework. They require .NET
Framework 4.6.2 or later. For ASP.NET Core on .NET 8 or later, see [ASP.NET Core](/docs/clients/dotnet/aspnet-core/).

| App type                          | Package                | How it starts                                    |
| --------------------------------- | ---------------------- | ------------------------------------------------ |
| ASP.NET MVC 5                     | `Exceptionless.Mvc`    | An HTTP module in `web.config`                   |
| ASP.NET Web API 2                 | `Exceptionless.WebApi` | `RegisterWebApi()` in your Web API configuration |
| ASP.NET WebForms and WCF (on IIS) | `Exceptionless.Web`    | An HTTP module in `web.config`                   |

An app that uses both MVC and Web API installs both packages, registers the MVC module, and calls `RegisterWebApi()`.

## Install

Install from the Package Manager Console:

```powershell
Install-Package Exceptionless.Mvc
```

Use `Exceptionless.WebApi` or `Exceptionless.Web` for those app types.

In projects that use `packages.config`, the install adds the `exceptionless` config section and, for MVC and WebForms,
the HTTP module to `web.config` for you. In projects that use `PackageReference`, add them by hand as shown below.

## Configure

### MVC and WebForms

Add the config section, your API key, and the module to `web.config`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleMvc/Web.config -->

```xml
<configuration>
  <configSections>
    <section name="exceptionless" type="Exceptionless.ExceptionlessSection, Exceptionless" />
  </configSections>
  <exceptionless apiKey="YOUR_API_KEY" />
  <system.webServer>
    <modules>
      <remove name="ExceptionlessModule" />
      <add name="ExceptionlessModule" type="Exceptionless.Mvc.ExceptionlessModule, Exceptionless.Mvc" />
    </modules>
  </system.webServer>
</configuration>
```

For WebForms, the module type is `Exceptionless.Web.ExceptionlessModule, Exceptionless.Web`.

The module calls `Startup()`, handles `HttpApplication.Error`, and adds request details to events. The MVC module also
adds a global MVC exception filter.

### Web API

Add the config section and API key to `web.config` as above, without a module. Then register Exceptionless where you
configure Web API, usually `WebApiConfig.Register` or `Application_Start`:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleMvc/Global.asax.cs -->

```csharp
using System.Web.Http;
using Exceptionless;

public class WebApiApplication : System.Web.HttpApplication {
    protected void Application_Start() {
        ExceptionlessClient.Default.RegisterWebApi(GlobalConfiguration.Configuration);
        // ...
    }
}
```

When you host Web API yourself with OWIN, pass your own `HttpConfiguration`:
`ExceptionlessClient.Default.RegisterWebApi(config)`.

`RegisterWebApi()` calls `Startup()`, adds an `IExceptionLogger`, and wraps your existing exception filter.

### WCF

Install `Exceptionless.Web`, register its module as for WebForms, and add the attribute to each service class:

<!-- source: Exceptionless.Net:samples/Exceptionless.SampleWcf/Service1.svc.cs -->

```csharp
using Exceptionless.Web;

[ExceptionlessWcfHandleError]
public class OrderService : IOrderService {
    // ...
}
```

The attribute submits each unhandled service exception and returns a fault to the caller.

> [!NOTE]
> If you self-host Exceptionless, add `serverUrl="https://exceptionless.example.com"` to the `exceptionless` element.

## The exceptionless config section {#the-exceptionless-config-section}

On .NET Framework, `Startup()` reads the `exceptionless` section from `web.config` or `app.config`. Attribute names are
case-sensitive.

```xml
<exceptionless apiKey="YOUR_API_KEY"
               serverUrl="https://exceptionless.example.com"
               enabled="true"
               includePrivateInformation="false"
               tags="web,production"
               storagePath="C:\ProgramData\MyApp\Exceptionless"
               enableLogging="true"
               logPath="C:\ProgramData\MyApp\Exceptionless\client.log">
  <data>
    <add name="Region" value="us-east" />
  </data>
  <settings>
    <add name="IncludeOrderData" value="true" />
  </settings>
</exceptionless>
```

| Attribute or element        | What it sets                                                                                                                                                                      |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `apiKey`                    | The project API key. Required.                                                                                                                                                    |
| `serverUrl`                 | Your self-hosted Exceptionless URL.                                                                                                                                               |
| `enabled`                   | `false` turns the client off.                                                                                                                                                     |
| `includePrivateInformation` | `false` leaves out user names, machine names, IP addresses, headers, cookies, form data, and query strings. See [Private information](/docs/clients/dotnet/private-information/). |
| `tags`                      | Comma-separated tags added to every event.                                                                                                                                        |
| `storagePath`               | A folder for the queue, so unsent events survive restarts. The app pool identity needs write access.                                                                              |
| `storageSerializer`         | An assembly-qualified type that serializes the queue, such as `Exceptionless.MessagePack.MessagePackStorageSerializer, Exceptionless.MessagePack`.                                |
| `queueMaxAge`               | How long to retry an event before dropping it, as a `TimeSpan` such as `3.00:00:00`. Default: 7 days.                                                                             |
| `queueMaxAttempts`          | How many times to try sending an event. Default: 3.                                                                                                                               |
| `enableLogging`             | `true` writes the client's own diagnostic log.                                                                                                                                    |
| `logPath`                   | Where to write that log. If unset, it goes to `storagePath`, or to isolated storage.                                                                                              |
| `<data>`                    | Values added to every event as extended data.                                                                                                                                     |
| `<settings>`                | Local [client configuration values](/docs/clients/dotnet/client-configuration-values/). Values from the server override them.                                                     |

You can also set the API key, server URL, tags, and `enabled` in `appSettings`, which is easier to change with config
transforms:

```xml
<appSettings>
  <add key="Exceptionless:ApiKey" value="YOUR_API_KEY" />
  <add key="Exceptionless:ServerUrl" value="https://exceptionless.example.com" />
  <add key="Exceptionless:DefaultTags" value="web,production" />
  <add key="Exceptionless:Enabled" value="true" />
</appSettings>
```

`Startup()` reads assembly attributes, then the config section, then `appSettings`, then environment variables, so a
later source overrides an earlier one. Configure anything else in code, before the module or `RegisterWebApi()` runs,
for example in `Application_Start`.

### Assembly attributes {#assembly-attributes}

Instead of a config file, you can set the API key with an assembly attribute:

```csharp
using Exceptionless.Configuration;

[assembly: Exceptionless("YOUR_API_KEY", ServerUrl = "https://exceptionless.example.com", Enabled = true)]
[assembly: ExceptionlessSetting("IncludeOrderData", "true")]
```

`Startup()` looks for these attributes in the app's entry assembly. A web app has no entry assembly, so read them from
your app's assembly before the client starts:

```csharp
ExceptionlessClient.Default.Configuration.ReadFromAttributes(typeof(WebApiApplication).Assembly);
```

## Verify

Add an action that throws, run the app, and request it:

```csharp
public ActionResult Throw() {
    throw new InvalidOperationException("Exceptionless test error");
}
```

Open **Stacks** in the app. The error appears within a few seconds, with the request details.

## Submit handled exceptions

Submit exceptions you catch with `ToExceptionless()`. In MVC and WebForms, the current request is added automatically:

```csharp
try {
    // ...
} catch (Exception ex) {
    ex.ToExceptionless().AddTags("checkout").Submit();
}
```

Web API has no static HTTP context, so pass the action context to get request and user details:

```csharp
ex.ToExceptionless().SetHttpActionContext(ActionContext).Submit();
```

## What is captured automatically

- **Unhandled exceptions** from `HttpApplication.Error`, MVC exception filters, Web API's `IExceptionLogger` and
  exception filters, and WCF services, with the queue sent right away.
- **404 errors** that reach the error handler as `HttpException`, recorded as not-found events.
- **Request details**: HTTP method, URL, user agent, referrer, client IP address, headers, cookies, and query string.
  MVC and WebForms also include the request body for unhandled exceptions on non-GET requests.
- **The signed-in user's name**, from the request's identity.
- **Environment details**: machine name, Windows version, .NET Framework version, process, memory, and IP addresses.
- **Unhandled exceptions outside requests**, from `AppDomain.UnhandledException` and unobserved task exceptions.

Requests from bots are ignored using the project's user agent bot patterns. To leave out personal data, see
[Private information](/docs/clients/dotnet/private-information/).

## Next steps

- [Sending events](/docs/clients/dotnet/sending-events/): logs, feature usage, custom data, and user identity.
- [Upgrading](/docs/clients/dotnet/upgrading/): moving to ASP.NET Core.
- [Troubleshooting](/docs/clients/dotnet/troubleshooting/): client logging, proxies, and certificates.

## Ask Exie

```prompt exie
Show me the most frequent unhandled errors in this project this week.
```

```prompt exie
Which request paths have the most 404s today?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the top three open error stacks in my Exceptionless project for this ASP.NET MVC app, trace each to its controller action, and fix them.
```
