---
title: "Exceptionless 5.0 Release - ASP.NET Core & Localization Support, and more!"
---

# Exceptionless 5.0 Release - ASP.NET Core & Localization Support, and more!

![Exceptionless 5.0 Localization](/assets/img/news/exceptionless-5.0-localization.png)

[Exceptionless](https://github.com/exceptionless/Exceptionless) 5.0 is here and we wanted to make a quick blog post highlighting the new features, bug fixes, and of course upgrading.

In this release, we focused primarily on migrating the application to ASP.NET Core, localization, along with ongoing performance enhancements and bug fixes.

More details, below:

## Exceptionless 5.0 New Features

  1. Exceptionless now runs on ASP.NET Core! This brings in many performance advantages as well as cross platform support.
  2. Docker/Kubernetes based hosting is now the default hosting model. This will bring seamless and quick upgrades while reducing the amount of environmental related errors.
  3. Added Chinese localization support. Thanks [@Varorbc](https://github.com/Varorbc), [@edwardmeng](https://github.com/edwardmeng) for that contribution!
  4. Added support for using various different cloud hosted services (e.g., Aliyun, Minio, S3) and metric providers (e.g., InfluxDB). Thanks [@edwardmeng](https://github.com/edwardmeng) for that contribution!
  5. When viewing 404 event types, you will now see a grid column for IP addresses. This will allow you to quickly identify any bots or security scans that might be happening to your applications.
  6. In addition to client side plugins that will remove sensitive user data, we've added server side code as well to remove any missed sensitive user data.
  7. Added the ability to delete your account on the manage account page.

## Version 5.0 Bug Fixes

* Various user interface usability issues have been fixed in this release. Please view the UI release notes ([v2.8.0](https://github.com/exceptionless/Exceptionless.UI/releases/tag/v2.8.0) for more info).
* Fixed a bug where notifications and web hooks would be sent for fixed events.
* Updated [Foundatio](https://github.com/FoundatioFx/Foundatio) which uses a task queue to resolve dead locking and thread exhaustion.

## Upgrading to Exceptionless 5.0

If you are using our hosted service, you do not need to upgrade anything on your end. If you are self hosting Exceptionless and upgrading from version 4 or 5, a little work is needed to get up and running using the new docker images and configuration. See our [upgrade guide](/docs/self-hosting/upgrading-self-hosted-instance) for more information.

Check out the [official release notes](https://github.com/exceptionless/Exceptionless/releases/tag/v5.0.0) here, or view the [full changelog](https://github.com/exceptionless/Exceptionless/compare/v4.1.0...v5.0.0) if you are interested in a complete list of changes.

## How are we doing?

As always, we want to know what you think! If you have questions, concerns, or any feedback, please [submit an issue over on the GitHub repo](https://github.com/exceptionless/Exceptionless/issues/new).
