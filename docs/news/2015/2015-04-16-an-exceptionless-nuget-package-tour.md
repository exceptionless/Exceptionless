---
title: "An Exceptionless NuGet Package Tour"
date: 2015-04-16
---

# An Exceptionless NuGet Package Tour

![Exceptionless NuGet Packages](/assets/img/news/nugetlogo.png)Giving back to the development community is important to us over here at Exceptionless. [We love open source!](https://github.com/exceptionless)

As such, the Exceptionless [NuGet assembly library](https://www.nuget.org/profiles/exceptionless?showAllPackages=True), with 31 assemblies and over 176,000 package downloads, is ever growing and expanding right along with our [GitHub repos](https://github.com/exceptionless).

We thought we would give everyone a quick tour, and at the same time perhaps provide a good resource and reference page.

Don't forget, though - this page may be out-dated by the time you view it, so please [view our full library on NuGet](https://www.nuget.org/profiles/exceptionless?showAllPackages=True).

## Exceptionless NuGet Clients

Exceptionless, our primary [open source](https://github.com/exceptionless/Exceptionless.net) application, provides real-time error, feature, and log reporting for ASP.NET, Web API, WebForms, WPF, Console, and MVC apps (and more, soon!).

Below you will find the NuGet assemblies for all the platforms we currently support.

[Exceptionless.Portable](https://www.nuget.org/packages/Exceptionless.Portable/)
Exceptionless client for portable (PCL) applications. This is the base library all the other implementations build on top of. It contains all the basic functionality that powers the Exceptionless clients! This library can be used on many different platforms. It’s worth noting that this is a very basic client and as such you won’t get all the bells and whistles as described [here](/docs/clients/dotnet/configuration) in the PCL configuration section. For those bells and whistles, see the Exceptionless package, below.
Frameworks: .NET 4, .NET 4.5, Silverlight 5, Windows 8, Windows Phone 8.1, Windows Phone Silverlight 8

[Exceptionless](https://www.nuget.org/packages/Exceptionless/)Exceptionless client for non visual (ie. Console and Services) applications.** **We recommend using this package if you are not using any other platform specific packages (E.G., Exceptionless.Mvc), as it provides all the bells and whistles that are missing in the portable package.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.Mvc](https://www.nuget.org/packages/Exceptionless.Mvc/)Exceptionless client for ASP.NET MVC 3+ applications.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.WebApi](https://www.nuget.org/packages/Exceptionless.WebApi/)Exceptionless client for ASP.NET Web API applications.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.Web](https://www.nuget.org/packages/Exceptionless.Web/)Exceptionless client for ASP.NET WebForms applications.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.Nancy](https://www.nuget.org/packages/Exceptionless.Nancy/)Exceptionless client for [Nancy](http://nancyfx.org/) applications.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.Wpf](https://www.nuget.org/packages/Exceptionless.Wpf/)
Exceptionless client for WPF applications.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.Windows](https://www.nuget.org/packages/Exceptionless.Windows/)Exceptionless client for Windows Forms applications.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.NLog](https://www.nuget.org/packages/Exceptionless.NLog/)NLog target that sends log entries to Exceptionless.
Frameworks: .NET 4.0, .NET 4.5

[Exceptionless.Log4net](https://www.nuget.org/packages/Exceptionless.Log4net/)Log4net appender that sends log entries to Exceptionless.
Frameworks: .NET 4.0, .NET 4.5

[Serilog.Sinks.ExceptionLess](https://www.nuget.org/packages/Serilog.Sinks.ExceptionLess/)Serilog sink that sends log entries to Exceptionless.
Frameworks: .NET 4.0, .NET 4.5
[View Source](https://github.com/serilog/serilog-sinks-exceptionless)

### Signed Assemblies

We also have signed versions of most assemblies on our [NuGet Profile](https://www.nuget.org/profiles/exceptionless?showAllPackages=True). See MSDN for additional info on signing assemblies ([Strong-Named Assemblies](https://msdn.microsoft.com/en-us/library/wd40t7ad%28v=vs.110%29.aspx)).

## Foundatio by Exceptionless

Foundatio (Requires .NET 4.5) is an open source library for building distributed applications that we built, use, and think you will find helpful. Foundatio provides in memory, redis, and azure implementations. This allows you to do development or testing using in-memory versions and switch them out for redis or azure implementations in production. This saves you time (setup and maintaining) and money (not paying for cloud resources) during development and testing! Foundatio source code can be found at [https://github.com/FoundatioFx/Foundatio](https://github.com/FoundatioFx/Foundatio).

Exceptionless was built using Foundatio and utilizes implementations for caching, queues, locks, messaging, jobs, file storage, and metrics.

[Foundatio](https://www.nuget.org/packages/Foundatio/)
Foundatio consists of pluggable foundation blocks for building distributed apps, including caching, queues, locks, messaging, jobs, file storage, and metrics.

[Foundatio Redis Implementations](https://www.nuget.org/packages/Foundatio.Redis/)
Contains the redis implementations of caching, queues, locks, messaging, jobs, file storage.

[Foundatio Azure ServiceBus Implementations](https://www.nuget.org/packages/Foundatio.AzureServiceBus/)
Contains the redis implementations of queues and messaging. The azure packages are split into different packages so you don't have to take extra azure dependencies on things you don't need.

[Foundatio Azure Storage Implementations](https://www.nuget.org/packages/Foundatio.AzureStorage/)
Contains the redis implementations of file storage. The azure packages are split into different packages so you don't have to take extra azure dependencies on things you don't need.

## Other

[Exceptionless LuceneQueryParser](https://www.nuget.org/packages/Foundatio.Parsers.LuceneQueries/)
Lucene Query Parser is a lucene style query parser that is extensible and allows additional syntax features. We use this in [Exceptionless](https://github.com/exceptionless/Exceptionless) to [ensure the query is valid before executing it, check to see if you are trying to a basic or premium search query and much more](https://github.com/exceptionless/Exceptionless/blob/master/src/Exceptionless.Core/Repositories/Queries/Validation/PersistentEventQueryValidator.cs)!
Frameworks: .NET 4.5
[View Source](https://github.com/FoundatioFx/Foundatio.Parsers)

[Exceptionless RandomData](https://www.nuget.org/packages/Exceptionless.RandomData/)
RandomData is a utility class for easily generating random data, making the creation of good unit test data a breeze.
Frameworks: .NET 4.0, .NET 4.5
[View Source](https://github.com/exceptionless/Exceptionless.RandomData)

[Exceptionless DateTimeExtensions](https://www.nuget.org/packages/Exceptionless.DateTimeExtensions/)
This package includes DateTimeRange, Business Day, and various DateTime, DateTimeOffset, and TimeSpan extension methods.
Frameworks: .NET 4.0, .NET 4.5, Windows 8, Windows Phone 8.1
[View Source](https://github.com/exceptionless/Exceptionless.DateTimeExtensions)

## We'll Keep Building!

We don't plan on stopping anytime soon, and will continue writing assemblies to make life easier for developers everywhere. Naturally, we could always use your help, so [fork us on GitHub](https://github.com/exceptionless) if you feel like chipping in. We always appreciate our contributors!

Otherwise, let us know what you think of Exceptionless, Foundatio, and all our other projects. Feedback is always welcomed and appreciated.
