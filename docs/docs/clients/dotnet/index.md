---
title: ".NET client"
description: "Exceptionless.Net packages for ASP.NET Core, worker services, console, desktop, Blazor, serverless, and logging, with target frameworks and setup guides."
redirectFrom:
  - /docs/clients/dotnet/supported-platforms/
  - /docs/clients/dotnet/guides/
sources:
  - Exceptionless.Net:src/Exceptionless/Exceptionless.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/Exceptionless.AspNetCore.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Hosting/Exceptionless.Extensions.Hosting.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Extensions.Logging/Exceptionless.Extensions.Logging.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Mvc/Exceptionless.Mvc.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.WebApi/Exceptionless.WebApi.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Web/Exceptionless.Web.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Wpf/Exceptionless.Wpf.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Windows/Exceptionless.Windows.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.NLog/Exceptionless.NLog.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.Log4net/Exceptionless.Log4net.csproj
  - Exceptionless.Net:src/Platforms/Exceptionless.MessagePack/Exceptionless.MessagePack.csproj
  - Exceptionless.Net:.github/workflows/build-windows.yml
  - Exceptionless.Net:.agents/skills/exceptionless-dotnet/SKILL.md
---

# .NET client

Exceptionless.Net reports errors, logs, feature usage, and sessions from .NET apps. The current release is 6.2.0. Pick
your app type below to set it up, or let your coding agent do it.

## Install with your coding agent

Paste this into Claude Code, Codex, Copilot, or another coding agent from the root of your app:

```prompt agent
Add Exceptionless error reporting to this .NET app. First work out what kind of app it is: ASP.NET Core, a worker service, a console app, WPF or Windows Forms, Blazor WebAssembly, a serverless function, or ASP.NET on .NET Framework. For ASP.NET Core, read https://exceptionless.com/docs/clients/dotnet/aspnet-core.md and follow it exactly. For anything else, find the matching page under https://exceptionless.com/docs/clients/dotnet/ and read its Markdown copy (the page URL with .md instead of the trailing slash). If the exceptionless-dotnet agent skill from the Exceptionless.Net repository (.agents/skills/exceptionless-dotnet) is installed, use it. Read the API key from configuration instead of hard-coding it, then tell me how to trigger a test error so I can confirm it shows up.
```

The Exceptionless.Net repository includes the
[`exceptionless-dotnet`](https://github.com/exceptionless/Exceptionless.Net/tree/main/.agents/skills/exceptionless-dotnet)
agent skill, which covers package choice, setup, configuration, and privacy for every app type. See
[Coding agents](/docs/ai/coding-agents/) to install it.

## Choose your app type

| App type                                                  | Package                                                                                                          | Guide                                                    |
| --------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| ASP.NET Core apps and APIs, Blazor Server                 | `Exceptionless.AspNetCore`                                                                                       | [ASP.NET Core](/docs/clients/dotnet/aspnet-core/)        |
| Worker services and the generic host                      | `Exceptionless.Extensions.Hosting`                                                                               | [Worker services](/docs/clients/dotnet/worker-services/) |
| Console apps and CLI tools                                | `Exceptionless`                                                                                                  | [Console apps](/docs/clients/dotnet/console/)            |
| WPF, Windows Forms, and .NET MAUI                         | `Exceptionless.Wpf`, `Exceptionless.Windows`, `Exceptionless`                                                    | [Desktop apps](/docs/clients/dotnet/desktop/)            |
| Blazor WebAssembly                                        | `Exceptionless`                                                                                                  | [Blazor WebAssembly](/docs/clients/dotnet/blazor/)       |
| AWS Lambda and Azure Functions                            | `Exceptionless`, `Exceptionless.AspNetCore`, or `Exceptionless.Extensions.Hosting`                               | [Serverless](/docs/clients/dotnet/serverless/)           |
| ASP.NET MVC, Web API, WebForms, and WCF on .NET Framework | `Exceptionless.Mvc`, `Exceptionless.WebApi`, `Exceptionless.Web`                                                 | [Legacy ASP.NET](/docs/clients/dotnet/legacy-aspnet/)    |
| Microsoft.Extensions.Logging, NLog, log4net, Serilog      | `Exceptionless.Extensions.Logging`, `Exceptionless.NLog`, `Exceptionless.Log4net`, `Serilog.Sinks.Exceptionless` | [Logging integrations](/docs/clients/dotnet/logging/)    |

Use the most specific package for your app. Platform packages add request details, framework error hooks, and shutdown
handling that the core package can't.

## Packages and requirements

All packages are on NuGet. Install with `dotnet add package <name>`, or `Install-Package <name>` in the Visual Studio
Package Manager Console.

| Package                                                                                                | Target frameworks                       | What it's for                                      |
| ------------------------------------------------------------------------------------------------------ | --------------------------------------- | -------------------------------------------------- |
| [`Exceptionless`](https://www.nuget.org/packages/Exceptionless/)                                       | .NET Standard 2.0, .NET Framework 4.6.2 | The core client. Every other package builds on it. |
| [`Exceptionless.AspNetCore`](https://www.nuget.org/packages/Exceptionless.AspNetCore/)                 | .NET 8, 9, 10                           | ASP.NET Core apps and APIs                         |
| [`Exceptionless.Extensions.Hosting`](https://www.nuget.org/packages/Exceptionless.Extensions.Hosting/) | .NET 8, 9, 10                           | Generic host apps and worker services              |
| [`Exceptionless.Extensions.Logging`](https://www.nuget.org/packages/Exceptionless.Extensions.Logging/) | .NET 8, 9, 10                           | A Microsoft.Extensions.Logging provider            |
| [`Exceptionless.Wpf`](https://www.nuget.org/packages/Exceptionless.Wpf/)                               | .NET 8 on Windows, .NET Framework 4.6.2 | WPF apps                                           |
| [`Exceptionless.Windows`](https://www.nuget.org/packages/Exceptionless.Windows/)                       | .NET 8 on Windows, .NET Framework 4.6.2 | Windows Forms apps                                 |
| [`Exceptionless.NLog`](https://www.nuget.org/packages/Exceptionless.NLog/)                             | .NET Standard 2.0, .NET Framework 4.6.2 | An NLog target                                     |
| [`Exceptionless.Log4net`](https://www.nuget.org/packages/Exceptionless.Log4net/)                       | .NET Standard 2.0, .NET Framework 4.6.2 | A log4net appender                                 |
| [`Exceptionless.MessagePack`](https://www.nuget.org/packages/Exceptionless.MessagePack/)               | .NET Standard 2.0, .NET 8               | MessagePack serialization for the stored queue     |
| [`Exceptionless.Mvc`](https://www.nuget.org/packages/Exceptionless.Mvc/)                               | .NET Framework 4.6.2                    | ASP.NET MVC 5 on .NET Framework                    |
| [`Exceptionless.WebApi`](https://www.nuget.org/packages/Exceptionless.WebApi/)                         | .NET Framework 4.6.2                    | ASP.NET Web API 2 on .NET Framework                |
| [`Exceptionless.Web`](https://www.nuget.org/packages/Exceptionless.Web/)                               | .NET Framework 4.6.2                    | ASP.NET WebForms and WCF on .NET Framework         |

In short:

- **ASP.NET Core, worker services, and the logging provider** need .NET 8 or later.
- **The core client, NLog, and log4net** run anywhere .NET Standard 2.0 does, including .NET 8 and later and .NET
  Framework 4.6.2 and later.
- **WPF and Windows Forms** need .NET 8 or later on Windows, or .NET Framework 4.6.2 or later.

Serilog support is in the separate `Serilog.Sinks.Exceptionless` package (see
[Logging integrations](/docs/clients/dotnet/logging/#serilog)).

## Get your API key

Every app needs your project's API key. In the app, open your project's settings and select **API Keys**. Each app type
guide shows where the key goes. Keep it in configuration or a secret store rather than in code.

## Next steps

- [Configuration](/docs/clients/dotnet/configuration/): every setting, in `appsettings.json`, environment variables, and
  code.
- [Sending events](/docs/clients/dotnet/sending-events/): handled exceptions, logs, feature usage, and custom data.
- [Client configuration values](/docs/clients/dotnet/client-configuration-values/): change app behavior from
  Exceptionless without redeploying.
- [Plugins](/docs/clients/dotnet/plugins/): add data to events or drop them.
- [Private information](/docs/clients/dotnet/private-information/): what's collected and how to leave it out.
- [Troubleshooting](/docs/clients/dotnet/troubleshooting/): when events don't arrive.
- [Upgrading](/docs/clients/dotnet/upgrading/): changes in 6.2 and earlier versions.

The source code, samples, and release notes are in the
[Exceptionless.Net repository](https://github.com/exceptionless/Exceptionless.Net).

## Ask Exie

```prompt exie
Help me set up Exceptionless in my .NET worker service. Which package do I need?
```

```prompt exie
Is my .NET app sending events to this project? Show me what arrived in the last hour.
```
