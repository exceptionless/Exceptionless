---
title: "JavaScript Client V1.5 Release Details & Notes"
date: 2017-08-15
---

# JavaScript Client V1.5 Release Details & Notes

![exceptionless.javascript 1.5](/assets/img/news/js-client-1.5-release-1024x538.jpg)
Exceptionless.JavaScript v1.5 Release Notes

### Unversal App Support

The major update for 1.5 is that we have added support for universal apps (React Universal).

To view the exact changes required to get everything working and set up, [click here](https://github.com/niemyjski/react-redux-universal-hot-example/commit/7f7c01ca1b328f3389c3919a53376bccbbfe1f08).

### Other 1.5 Updates



* Added support for exceptions that are passed in and are just strings.
* Updated TraceKit to the latest version, which adds support for parsing PhantomJS errors and greatly improves native stack traces.
* Thanks [@caesay](https://github.com/caesay) for updating TypeScript to the latest version, as well!

### Bug Fixes

* Fixed a bug where exceptionless would fail to load when used with webpack.
* Fixed a bug where browser module info was not being populated.
* Fixed a bug where ILog.trace was piped to the wrong console function which would error under IE (thanks [@srijken](https://github.com/srijken){.user-mention}!)
* Fixed a few bugs with angular integration
* Fixed a bug with submitLog not respecting log message and log level

For a full change log, select an applicable release on the [Exceptionless.JavaScript GitHub Release page](https://github.com/exceptionless/Exceptionless.JavaScript/releases).
