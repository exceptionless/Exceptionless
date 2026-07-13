---
title: "Exceptionless for React Native and Expo"
date: 2026-07-12
---

# Exceptionless for React Native and Expo

Exceptionless.JavaScript now includes a dedicated React Native and Expo SDK. The new `@exceptionless/react-native`
package brings Exceptionless error and event reporting to mobile applications while keeping the same configuration and
event APIs used across our JavaScript clients.

## What it captures

The client automatically captures unhandled JavaScript errors and promise rejections, enriches events with React Native
and device context, persists queued events with AsyncStorage, and supports sessions, logs, feature usage, user identity,
and React error boundaries.

On iOS, the client also uses PLCrashReporter to capture native Objective-C and Swift exceptions, signals, and Mach
exceptions. Native crash reports are stored on the device and submitted when the application starts again.

## Expo support

Expo managed and bare applications can install the client with `npx expo install`. JavaScript reporting works in Expo
Go. Native iOS crash reporting uses the included Expo config plugin and requires a development or standalone build
because Expo Go cannot load custom native modules.

We maintain an
[Expo example application](https://github.com/exceptionless/Exceptionless.JavaScript/tree/master/example/expo) that
exercises caught and unhandled errors, promise rejections, logs, feature usage, sessions, identity, error boundaries,
and native iOS crash submission.

## Get started

Follow the [React Native and Expo guide](/docs/clients/javascript/guides/react-native-expo/) for installation, the Expo
config plugin, application startup, error boundaries, and support boundaries.

We also expanded the [client directory](/docs/clients/) and added a
[JavaScript supported platforms matrix](/docs/clients/javascript/supported-platforms/) covering Browser, Node.js, Deno,
React Native, Expo, and framework-specific packages and examples.

Questions or feedback are welcome in the
[Exceptionless.JavaScript repository](https://github.com/exceptionless/Exceptionless.JavaScript/issues).
