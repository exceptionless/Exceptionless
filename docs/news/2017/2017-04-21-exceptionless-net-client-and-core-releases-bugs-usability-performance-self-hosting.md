---
title: "Exceptionless.NET client, core, UI,  releases - Bugs, Usability, Performance, Self Hosting"
date: 2017-04-21
---

# Exceptionless.NET client, core, UI,  releases - Bugs, Usability, Performance, Self Hosting

![](/assets/img/news/new-releases-header3-1024x538.jpg)

This past week we released Exceptionless 4.0.2 and Exceptionless.NET 4.0.3, consisting of maintenance updates that fixed several usability issues for self hosters, various performance, issues, bug fixes, and some general improvements.

Below is a highlight of the release notes, and don't forget to let us know how we're doing or what can improve by following the relevant links to GitHub and posting an "issue."

Lastly, thanks to all of our contributors for helping us solve problems, add functionality, and improve Exceptionless!

## Exceptionless 4.0.2 Release Notes

* Both [@caesay](https://github.com/caesay) and [@edwardmeng](https://github.com/edwardmeng) submitted pull requests and helped us solve an issue with sending emails (issue [#290](https://github.com/exceptionless/Exceptionless/issues/290)). [MailKit](https://github.com/jstedfast/MailKit), a "cross-platform mail client library," has now been integrated, adding "fully featured and RFC-compliant SMTP, POP3, and IMAP client implementations" into Exceptionless.
  * There were also a few issues with email when hosting in different environments, such as Azure Functions. In this case, all email settings must be stored in settings and not web.config. So, we moved the MailKit implementation to the insulation project and cleaned up the main mailer class, among a few other tweaks, to further improve email sending in the app. Thanks again caesay and edwardmeng!
  * **If you are self hosting**, please update the email settings in `appSettings`
* Support for Azure Storage Queues has been added to the app.
* A bug that could cause an exception and make the stack work queue be abandoned when a stack of events was deleted has been fixed.
* The GeoIP database was being downloaded each time the app was restarted. That has also been fixed.
* Incorrect emails were being generated in some self hosted and dev environments because the BASE_URL didn't contain the proper hashbang (#!). Fixed that issue in this release, as well.
* [View the full changelog.](https://github.com/exceptionless/Exceptionless/compare/v4.0.1...v4.0.2)

### Upgrading to Exceptionless 4.0.2

You should only worry about upgrading if you are a self-hoster. If this is the case, please see the [Exceptionless self hosting documentation](/docs/self-hosting/). **Note that changes to the Elasticsearch configuration were made in this release**, so make sure to review the documentation for more information.

## Exceptionless.NET 4.0.3 Release Notes

### New Features

* log4net .NET Core support has been added.
* 404 reporting support in ASP.NET Core has been added.
* You can now set the min log level in configuration by calling `SetDefaultMinLogLevel`, allowing you to set a temporary min log level that is used until the server settings are retrieved.
* Client IP support for X-Forwarded-For has been added (thanks [@barankaynak](https://github.com/barankaynak)!), which enables us to properly identify individual users and also helps when using proxy servers.
* You can now more easily capture the HttpActionContext by adding `SetHttpActionContext` extension methods to web clients, allowing request and user info to be captured by default when manually submitting events.
* We added SetException overload so you can now submit any event type with an exception object. So, for instance, you can now submit a log message through with an exception object and the exception tab will show.

### Bug Fixes

* Fixed an issue where the ASP.NET Core 1.1 runtime was sometimes preventing clients from reporting any data.
* Fixed an issue where exceptions that converted to 404's were not running the event exclusion logic.
* Fixed an issue where the duplicate checker plugin could DOS itself if you had client logging enabled (disabled by default - only meant for diagnostic logging).
* Fixed an issue where the NLog logger wasn't setting event type.
* Fixed an issue with package configuration of signed web packages.
* Fixed an issue where adding our trace listener could blow up due to other invalid configured trace listeners.
* Upgraded to the latest version of BenchMarkDotNet and ensure benchmarks run (Contrib [@adamsitnik](https://github.com/adamsitnik))

### Upgrading

Just update your NuGet packages. For more info, check out the [upgrade guide](/docs/clients/dotnet/upgrading).

[View the full changelog.](https://github.com/exceptionless/Exceptionless.Net/compare/v4.0.2...v4.0.3)

## Exceptionless.UI 2.6.2 Release Notes

* Previously, long, single-line content could cause the user interface to go crazy while updating event lists. This has been fixed (thanks @caesay!).
* Also, tags now wrap to the next line, instead of overflowing the content area. @caesay strikes again!
* [View the full changelog.](https://github.com/exceptionless/Exceptionless.UI/compare/v2.6.1...v2.6.2)

## Feedback Requested

We **want** to know what you think about Exceptionless - what you think we should add, fix, streamline, improve, etc. Please let us know!

* [.NET Client Feedback](https://github.com/exceptionless/Exceptionless.Net/issues/new)
* [JavaScript Client Feedback](https://github.com/exceptionless/Exceptionless.JavaScript/issues/new)
* [Exceptionless Feedback](https://github.com/exceptionless/exceptionless/issues/new)
