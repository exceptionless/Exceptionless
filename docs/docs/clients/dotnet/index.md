---
title: ".NET Client"
---

# .NET Client

Exceptionless provides a simple-to-use .NET client for your convenience. To get started, you'll need to install the client. There are a variety of ways to install, but for brevity we'll focus on the dotnet cli. If you don't have that installed, [follow the guide here](https://docs.microsoft.com/en-us/dotnet/core/sdk).

Exceptionless provides packages for specific platform (listed toward the bottom of this page), but to get started quickly, you can install the root version of Exceptionless like so:

`dotnet add package Exceptionless`

If you want to install a specific version, you can pass in the `--version` flag like this:

`dotnet add package Exceptionless --version 4.5.0`

For more specific packages for your particular target platform, we have provided the following packages:

## [Exceptionless.Mvc](https://www.nuget.org/packages/Exceptionless.Mvc/)

Exceptionless client for ASP.NET MVC 3+ applications.

## [Exceptionless.WebApi](https://www.nuget.org/packages/Exceptionless.WebApi/)

Exceptionless client for ASP.NET Web API applications.

## [Exceptionless.Web](https://www.nuget.org/packages/Exceptionless.Web/)

Exceptionless client for ASP.NET WebForms applications.

## [Exceptionless.AspNetCore](https://www.nuget.org/packages/Exceptionless.AspNetCore/)

Exceptionless client for ASP.NET Core applications.

## [Exceptionless.Nancy](https://www.nuget.org/packages/Exceptionless.Nancy/)

Exceptionless client for [Nancy](http://nancyfx.org/) applications.

## [Exceptionless.Wpf](https://www.nuget.org/packages/Exceptionless.Wpf/)

Exceptionless client for WPF applications.

## [Exceptionless.Windows](https://www.nuget.org/packages/Exceptionless.Windows/)

Exceptionless client for Windows Forms applications.

## [Exceptionless.NLog](https://www.nuget.org/packages/Exceptionless.NLog/)

NLog target that sends log entries to Exceptionless.

## [Exceptionless.Log4net](https://www.nuget.org/packages/Exceptionless.Log4net/)

Log4net appender that sends log entries to Exceptionless.

## [Serilog.Sinks.ExceptionLess](https://www.nuget.org/packages/Serilog.Sinks.ExceptionLess/)

Serilog sink that sends log entries to Exceptionless.

<!-- Got it installed? Great, let's get started.

## Topics

* [Configuration](/docs/clients/dotnet/configuration)
* [Client Configuration Values](/docs/clients/dotnet/client-configuration-values)
* [Sending Events](/docs/clients/dotnet/sending-events)
* [Supported Platforms](/docs/clients/dotnet/supported-platforms)
* [Settings](/docs/clients/dotnet/settings)
* [Plugins](/docs/clients/dotnet/plugins)
* [Private Information](/docs/clients/dotnet/private-information)
* [Troubleshooting](/docs/clients/dotnet/troubleshooting)
* [Upgrading](/docs/clients/dotnet/upgrading)

## Related Topics

* [Reference Ids](/docs/references-ids)
* [User Sessions](/docs/user-sessions)
* [Manual Stacking](/docs/manual-stacking) -->

---

[Next > Platform Guides](/docs/clients/dotnet/guides/)
