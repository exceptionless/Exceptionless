---
title: "Exceptionless.JavaScript 1.2 Release Notes"
date: 2015-12-03
---

# Exceptionless.JavaScript 1.2 Release Notes

![Exceptionless JavaScript Client](/assets/img/news/blog-header-image-post2b-300x106.png)We've got a quick release for the Exceptionless JavaScript Client that includes a few new additions to what it supports and a quick bug fix or two.

Shout out to [@frankebersoll](https://github.com/frankebersoll) for his help!

**Please download and update to the** [source code here](https://github.com/exceptionless/Exceptionless.JavaScript/releases/tag/v1.2.0), and view the full [change log here](https://github.com/exceptionless/Exceptionless.JavaScript/compare/v1.1.1...v1.2.0).

## JavaScript Client v1.2 Notes

### Errors

@frankebersoll added support for deduplicating JavaScript errors. Thanks!

### Data Collection

Frank's back at it, adding support for collecting extra exception data that was already in the .NET client like module info, custom exception properties, and everything else that was already displayed in the UI when using the .NET client.

### Node Info

Support for collecting Node module info has also been added (thanks again, @frankebersoll).

### Bug Fix - Invalid States

An issue where Data Exclusions could cause events to be submitted in an invalid state has been fixed.

### As always, let us know if you have any questions or feedback!
