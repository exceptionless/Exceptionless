---
title: "Client features"
description: "Compare what each Exceptionless client supports, from unhandled errors and logs to sessions, configuration values, data exclusions, and the offline queue."
sources:
  - Exceptionless.Net:src/Exceptionless/Plugins/EventPluginManager.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/910_DuplicateCheckerPlugin.cs
  - Exceptionless.JavaScript:packages/core/src/plugins/EventPluginManager.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/DuplicateCheckerPlugin.ts
---

# Client features

This table shows which features each Exceptionless client supports. The sections below say what each feature does and
where it's documented.

```clients features
```

- **Yes:** the client does it, usually without setup.
- **Opt-in:** the client supports it once you turn it on in its configuration.
- **Partial:** the client supports part of it; the note in the table says which part.
- **No:** the client doesn't support it yet.
- **Not applicable:** the feature doesn't apply to the language.

To send events from a language without a client, see
[Other languages and custom clients](/docs/clients/custom-clients/). For how a client implements each feature, see the
[client specification](/docs/reference/client-spec/).

## Unhandled error capture {#unhandled-errors}

The client hooks into the runtime or framework and reports errors your code doesn't catch, without any calls from you.
See the language's sending events page: [.NET](/docs/clients/dotnet/sending-events/) or
[JavaScript](/docs/clients/javascript/sending-events/).

## Manual error submission {#manual-errors}

You submit an error you caught and handled, with any extra data, tags, or user you want to add. The client converts it
to an error event with its stack trace. See [Event format](/docs/reference/event-format/#error).

## Log messages {#logs}

You send log events with a source and a level. The project's log level settings decide which levels the client keeps, so
you can turn logging up or down without redeploying. See [Logs and log levels](/docs/setting-log-levels/).

## Feature usage {#feature-usage}

You send an event each time a feature is used, to see which features people use. See
[Feature usage](/docs/feature-usage/).

## 404 tracking {#not-found}

The client reports requests for pages or resources that don't exist as `404` events, with the request details. See
[Event types](/docs/reference/event-format/#event-types).

## User identity {#user-identity}

You attach the current user's ID and name to events, so you can see how many users an error affects and find every event
for one user. See [User](/docs/reference/event-format/#user).

## Sessions and heartbeats {#sessions}

The client sends a session start event, heartbeats while the app is in use, and a session end when it closes, so you can
see what a user did before an error. See [Sessions](/docs/user-sessions/).

## Reference IDs {#reference-ids}

The client gives each error a short ID that a user can quote to support, and that you can look up in the app. See
[Reference IDs](/docs/references-ids/).

## Server-synced configuration values {#config-values}

The client downloads the project's configuration values, keeps them current, and tells your code when they change, so
you can change settings and feature flags without redeploying. See
[Projects](/docs/project-settings/#client-configuration).

## Plugins {#plugins}

You add code that runs on every event before it's sent, to add data, change the event, or cancel it. See
[.NET plugins](/docs/clients/dotnet/plugins/).

## Data exclusions {#data-exclusions}

The client removes fields whose names match exclusion patterns, such as `*password*`, before events leave your app.
Patterns come from your code and from the project's settings. See [Projects](/docs/project-settings/#data-exclusions).

## Offline queue persistence {#offline-queue}

Every client queues events in memory and retries when the network or server is down. With persistence, the queue is also
saved to storage, so queued events survive a restart and are sent when the app starts again. See the
[client specification](/docs/reference/client-spec/#offline-queue).

## Deduplication {#deduplication}

The client merges identical events that happen within a short window into one event with a `count`, so a tight loop of
errors doesn't use up your plan. The .NET client merges any identical event within 60 seconds; the JavaScript client
merges identical errors within 30 seconds. See [Limits and deduplication](/docs/concepts/limits/#deduplication).

## Source maps {#source-maps}

Stack traces from minified JavaScript are mapped back to your original files, functions, and line numbers. See
[Source maps](/docs/clients/javascript/source-maps/).
