---
title: "Exceptionless.JavaScript 3.0.0 Released"
date: 2023-03-28
---

# Exceptionless.JavaScript 3.0.0 Released

We are excited to announce our [latest release](https://github.com/exceptionless/Exceptionless.JavaScript/releases/tag/v3.0.0), which not only clears the entire [JavaScript client](https://github.com/exceptionless/Exceptionless.JavaScript) GitHub backlog but also brings in a whole range of awesome new capabilities!

Our team has been hard at work, and we believe this update will greatly improve the developer experience while addressing critical issues and enhancing the overall functionality of our offering. Below are some of the highlights of this release.

## Graceful Termination

The improved client behavior now ensures a graceful termination when the last app statement executes, resulting in a significantly better CLI/Lambda experience for developers.

## New Features

This release includes a plethora of new features and fixes, such as:

- Support for serializing event data with a `maxDepth`. As part of this we did a lot of work to add a [prune implementation](https://github.com/exceptionless/Exceptionless.JavaScript/blob/v3.0.0/packages/core/src/Utils.ts#L193-L367) that handles all cases like circular references, Typed Arrays, Unsupported types (E.G., Buffers) and more
- Improved handling of different promise rejection error types
- Ignoring errors created by browser extensions
- Session management improvements

## Developer Experience Boost

We have now made it easier to access all transitive exports (from `@exceptionless/core`) in dependent packages. This resolves issues with browser bundles and `@exceptionless/core` imports, ultimately enhancing the developer experience.

## Updated Readme for Node --enable-source-maps

Our readme now includes updated information on Node `--enable-source-maps`, ensuring developers have the most up-to-date guidance for using this feature.

## Bug Fixes

We have addressed several bugs in this release, including:

- Fixing configuration default data not having exclusions applied
- Preventing timers from firing when the API key isn't configured
- Preserving event type if the event has an error
- Catching and logging storage API call errors

## Enhanced Error Handling

We now use the toError function for jQuery and Angular errors, fixing issues where the client may have thrown an exception due to an invalid error type. Additionally, we have added examples for various error browser integrations (e.g., jQuery).

## Breaking Changes

Our new release targets ES2021 and ESM Node 18 (fetch built-in). This allows us to reduce the size of our bundles by removing polyfills.

## We want to hear from you

This release reflects our commitment to continuously improving our product and offering the best possible experience for our users. We encourage you to explore the new features and enhancements, and as always, we welcome your feedback and suggestions.

- [.NET Client Feedback](https://github.com/exceptionless/Exceptionless.Net/issues/new)
- [JavaScript Client Feedback](https://github.com/exceptionless/Exceptionless.JavaScript/issues/new)
- [Exceptionless Feedback](https://github.com/exceptionless/exceptionless/issues/new)

The team at Exceptionless
