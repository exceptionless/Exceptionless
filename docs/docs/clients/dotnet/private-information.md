---
title: "Private information in the .NET client"
navTitle: "Private information"
description: "What personal data the Exceptionless .NET client collects, the IncludePrivateInformation and Include* settings, and data exclusions."
metaTitle: "Private data in the .NET client"
sources:
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/DataDictionaryExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ToErrorModelExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Services/DefaultEnvironmentInfoCollector.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/016_SetEnvironmentUserPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/015_ConfigurationDefaultsPlugin.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/RequestInfoCollector.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessAspNetCorePlugin.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.Web/RequestInfoCollector.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.WebApi/RequestInfoCollector.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/40_RequestInfoPlugin.cs
---

# Private information in the .NET client

By default, the .NET client sends everything it can collect, which can include personal data such as user names, IP
addresses, and cookies. This page lists what each setting controls. For how Exceptionless handles your data, see
[Security and privacy](/docs/security/).

## Turn off all private information

Set `IncludePrivateInformation` to `false` to turn off every setting in the next section.

```json tab="appsettings.json"
{
  "Exceptionless": {
    "IncludePrivateInformation": false
  }
}
```

```csharp tab="Code"
ExceptionlessClient.Default.Configuration.IncludePrivateInformation = false;
```

```xml tab="web.config"
<exceptionless apiKey="YOUR_API_KEY" includePrivateInformation="false" />
```

## Individual settings

Leave `IncludePrivateInformation` on and turn off only what you need. Set these in code, before the client starts:

```csharp
var config = ExceptionlessClient.Default.Configuration;
config.IncludeIpAddress = false;
config.IncludeCookies = false;
config.IncludePostData = false;
```

| Setting              | Default | What it controls                                                                                                                                             |
| -------------------- | ------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `IncludeUserName`    | `true`  | The user name added for you: the signed-in user in web apps, and the Windows user in WPF and Windows Forms. A user identity you set yourself is always sent. |
| `IncludeMachineName` | `true`  | The machine name in the environment details.                                                                                                                 |
| `IncludeIpAddress`   | `true`  | The machine's IP addresses in the environment details, and the client IP address of web requests.                                                            |
| `IncludeHeaders`     | `true`  | Request headers in web apps.                                                                                                                                 |
| `IncludeCookies`     | `true`  | Request cookies in web apps.                                                                                                                                 |
| `IncludePostData`    | `true`  | The request body or form fields, collected only for unhandled exceptions on non-GET requests.                                                                |
| `IncludeQueryString` | `true`  | Query string values in web apps.                                                                                                                             |

Setting `IncludePrivateInformation` sets all seven at once. Set it first, then change individual settings.

## What's never collected

Even with everything turned on, the web packages leave out:

- the `Authorization`, `Cookie`, and `Proxy-Authorization` headers;
- cookies named `.ASPX*`, `__*`, or `*SessionId*`, and form fields named `__*`;
- header, cookie, query string, and form values of 1,000 characters or more;
- request bodies over 50 KB in ASP.NET Core, and request bodies on GET requests.

## Data exclusions {#data-exclusions}

Data exclusions remove fields by name. A pattern such as `*Password*` or `CardNumber` removes matching:

- request header names, cookie names, query string keys, and form field names;
- properties on custom exception types and keys in `Exception.Data`;
- property names in objects you add with `SetProperty`, `AddObject`, or `DefaultData`.

Patterns are case-insensitive and accept `*` at the start, the end, or both.

Add exclusions for every app in a project from the project's settings in the app, so you can change them without
redeploying (see [Projects](/docs/project-settings/#data-exclusions)). The client downloads them as the
`@@DataExclusions` [client configuration value](/docs/clients/dotnet/client-configuration-values/).

Add exclusions for one app in code:

```csharp
ExceptionlessClient.Default.Configuration.AddDataExclusions("*Password*", "*Token*", "CardNumber");
```

The client uses both lists, plus two built-in patterns, `__Restricted*` and `Restricted*`. Exceptionless applies the
project's exclusions to request data again when it processes an event.

Exclusions match names, not values. A password in a log message or an exception message is still sent. Don't log
secrets, and use a plugin for anything that isn't a named field.

## Remove data with a plugin

For data that exclusions can't reach, remove it in a [plugin](/docs/clients/dotnet/plugins/). Give it a priority above
90 so it runs after the client has added default data and request details:

```csharp
using System.Linq;
using Exceptionless;

ExceptionlessClient.Default.Configuration.AddPlugin("remove-secrets", 100, context => {
    foreach (string key in context.Event.Data.Keys.ToList()) {
        if (key.Contains("Secret") || key.Contains("Token"))
            context.Event.Data.Remove(key);
    }
});
```
