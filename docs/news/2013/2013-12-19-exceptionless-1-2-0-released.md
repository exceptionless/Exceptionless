---
title: "Exceptionless 1.2.0 Released"
---

# Exceptionless 1.2.0 Released

## Server Features

* Added ability to copy extended data to the clipboard.
* Added ability to copy stack traces to the clipboard. This is very useful when you have an obfuscated stack trace and wish to decode it.
* Added ability to purchase yearly billing plans.
* Added the ability to consume the rest api with an `apikey` query string parameter.
* We now show friendly error messages when an error occurs while downgrading or upgrading billing plans.
* Numerous improvements to the project configuration page.
* Numerous bug fixes and performance enhancements.

## Client Features

Update your NuGet packages to take advantage of these improvements!

* Added support for MVC5 and Web API 2.0.
* Improved the detection and ignoring of duplicate errors to prevent them from being reported.
* The client now excludes dynamic assemblies from the modules error report section.
* AddObject now serializes objects to a depth of 5 by default.
* Fixed a couple bugs that may occur when multiple client instances are running on the same machine concurrently.
* Fixed a bug with the ExceptionlessWcfHandleErrorAttribute where it wouldn't catch errors when aspNetCompatibilityEnabled was set to false.
* The client now submits errors to [collector.exceptionless.com/api/v1/error](https://collector.exceptionless.com/api/v1/error).
* Fixed a bug that would prevent the Windows Form client from showing the Crash Report dialog.
* Fixed a bug where multiple HttpModule sections could be added by the NuGet installer.
