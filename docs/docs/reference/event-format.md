---
title: "Event format"
description: "The JSON format of an Exceptionless event: top-level fields, known event types, and the reserved @ data keys for errors, requests, users, and more."
sources:
  - src/Exceptionless.Core/Models/Event.cs
  - src/Exceptionless.Core/Models/PersistentEvent.cs
  - src/Exceptionless.Core/Models/Data/Error.cs
  - src/Exceptionless.Core/Models/Data/InnerError.cs
  - src/Exceptionless.Core/Models/Data/StackFrame.cs
  - src/Exceptionless.Core/Models/Data/Method.cs
  - src/Exceptionless.Core/Models/Data/Module.cs
  - src/Exceptionless.Core/Models/Data/SimpleError.cs
  - src/Exceptionless.Core/Models/Data/RequestInfo.cs
  - src/Exceptionless.Core/Models/Data/EnvironmentInfo.cs
  - src/Exceptionless.Core/Models/Data/UserInfo.cs
  - src/Exceptionless.Core/Models/Data/UserDescription.cs
  - src/Exceptionless.Core/Models/Data/ManualStackingInfo.cs
  - src/Exceptionless.Core/Models/Data/SubmissionClient.cs
  - src/Exceptionless.Core/Models/Data/Location.cs
  - src/Exceptionless.Core/Serialization/EventDataNormalizer.cs
  - src/Exceptionless.Core/Serialization/EventEnvironmentConverter.cs
  - src/Exceptionless.Core/Serialization/JsonSerializerOptionsExtensions.cs
  - src/Exceptionless.Core/Plugins/EventParser/EventParserPluginManager.cs
  - src/Exceptionless.Core/Plugins/EventParser/Default/JsonEventParserPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/40_RequestInfoPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/07_SubmissionClientPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/45_EnvironmentInfoPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/90_RemovePrivateInformationPlugin.cs
  - src/Exceptionless.Core/Jobs/EventUserDescriptionsJob.cs
  - src/Exceptionless.Core/Pipeline/001_CheckEventDateAction.cs
  - src/Exceptionless.Core/Pipeline/006_TruncateFieldsAction.cs
  - src/Exceptionless.Core/Extensions/PersistentEventExtensions.cs
  - Exceptionless.JavaScript:packages/core/src/models/Event.ts
  - Exceptionless.Net:src/Exceptionless/Models/Client/Event.cs
---

# Event format

Every Exceptionless event is a JSON object. Clients send one object or an array of them to `POST /api/v2/events`, and
the API returns stored events in the same shape with a few extra fields. This page describes each field and the reserved
data keys the server understands.

Property names are `snake_case`. Dates are ISO 8601 strings with a UTC offset.

```json
{
  "type": "error",
  "source": "CheckoutController.PlaceOrder",
  "date": "2026-10-27T14:03:12.512-05:00",
  "message": "Payment provider timed out",
  "tags": ["checkout", "payments"],
  "reference_id": "a1b2c3d4e5",
  "environment": "production",
  "count": 1,
  "data": {
    "@simple_error": {
      "message": "Payment provider timed out",
      "type": "System.TimeoutException",
      "stack_trace": "   at Payments.Client.ChargeAsync() in Client.cs:line 42"
    },
    "@user": { "identity": "user-1042", "name": "Ana Lopez" },
    "@version": "2.4.1",
    "order_id": "ORD-55812"
  }
}
```

## Event fields {#event-fields}

| Field          | Type             | Description                                                                                                                                                                                                                                            |
| -------------- | ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `type`         | string           | The [event type](#event-types), 1 to 100 characters. When it's missing, the server uses `error` if `data` has `@error` or `@simple_error`, and `log` otherwise.                                                                                        |
| `source`       | string           | Where the event came from: a logger name, a feature name, a request path, or a machine name. Values longer than 2,000 characters are truncated.                                                                                                        |
| `date`         | string           | When the event happened. When it's missing, the server uses the time it parsed the event. A date in the future is moved to the current time. Events dated more than three days in the past, or older than the organization's retention, are discarded. |
| `message`      | string           | A short description. Values longer than 2,000 characters are truncated. For errors without a message, the server copies the error's message.                                                                                                           |
| `tags`         | array of strings | Labels for the event. The server keeps up to 50 tags and drops empty tags and tags longer than 100 characters. The `Critical` tag marks an occurrence as critical.                                                                                     |
| `geo`          | string           | Coordinates as `"latitude,longitude"`, for example `"32.85,-96.96"`. When it's missing or invalid, the server looks up a location from the IP addresses on the event.                                                                                  |
| `value`        | number           | A number to attach to the event, such as a duration. Session start events store the session length in seconds here.                                                                                                                                    |
| `count`        | integer          | How many times this event happened. See [count](#count).                                                                                                                                                                                               |
| `reference_id` | string           | An identifier you choose, 8 to 100 letters, digits, or hyphens. See [reference IDs](#reference-ids).                                                                                                                                                   |
| `environment`  | string           | The deployment environment, such as `production` or `staging`. See [environment](#environment).                                                                                                                                                        |
| `data`         | object           | Custom data and the [reserved data keys](#reserved-data-keys).                                                                                                                                                                                         |

Unknown top-level properties are moved into `data`, so `{"message": "Hi", "@user": {"identity": "42"}}` is the same as
nesting `@user` under `data`.

### Count {#count}

`count` is the number of occurrences one event stands for. Clients set it when they merge duplicate events before
sending (see [client-side deduplication](/docs/reference/client-spec/#deduplication)). A missing `count` means one
occurrence. The occurrence counts in stack lists add up `count` across events.

### Reference IDs {#reference-ids}

A reference ID lets you find an event later, for example from an error page shown to a user. It must be 8 to 100
characters long and contain only letters, digits, and hyphens. The server replaces an invalid value with
`invalid-reference-id` and keeps the original in `data.InvalidReferenceId`.

Within a project, the server discards a second event with the same reference ID that arrives within a day. Clients use
this to make retries safe. See [Reference IDs](/docs/references-ids/) for the support workflow.

### Environment {#environment}

`environment` names the deployment the event came from. The server trims it and keeps the casing you send. A value that
is empty, longer than 64 characters, or contains control characters is not used as the environment. It is kept as custom
data instead. See [Environments](/docs/environments/).

Don't confuse it with the `@environment` data key, which describes the machine and runtime.

## Event types {#event-types}

`type` can be any string, but these types have built-in behavior.

| Type         | Meaning                                                                                                                                                                          |
| ------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `error`      | An error or exception. Put the details in `@error` or `@simple_error`; errors are stacked by their stack trace.                                                                  |
| `log`        | A log message. Put the level in `@level` and the logger name in `source`.                                                                                                        |
| `usage`      | Feature usage. Put the feature name in `source`. See [Feature usage](/docs/feature-usage/).                                                                                      |
| `404`        | A resource that wasn't found. Include `@request`; when `source` is empty the server sets it to the request method and path.                                                      |
| `session`    | The start of a session. Its `reference_id` is the session ID. See [Sessions](/docs/user-sessions/).                                                                              |
| `sessionend` | The end of a session.                                                                                                                                                            |
| `heartbeat`  | Keeps a session open. The server applies it to the session and discards the event. Clients normally use the [heartbeat endpoint](/docs/reference/client-spec/#sessions) instead. |

## Reserved data keys {#reserved-data-keys}

Keys in `data` that start with `@` are reserved for Exceptionless. Everything else in `data` is custom data that appears
on the event's **Extended Data** tab and can be searched (see [Search syntax](/docs/reference/search-syntax/)).

If a reserved key that expects an object holds something else, such as a plain string in `@user`, the server keeps the
value as custom data under the same key prefixed with `_`, for example `_@user`.

| Key                  | Shape                                   | Set by                                                                            |
| -------------------- | --------------------------------------- | --------------------------------------------------------------------------------- |
| `@error`             | [Error](#error)                         | Clients that can read the full exception.                                         |
| `@simple_error`      | [Simple error](#simple-error)           | Clients and scripts that only have a message and a trace.                         |
| `@request`           | [Request](#request)                     | Clients running in a web request or a browser.                                    |
| `@environment`       | [Environment info](#environment-info)   | Clients that can read the machine and process.                                    |
| `@user`              | [User](#user)                           | The app, to identify the user an event happened to.                               |
| `@user_description`  | [User description](#user-description)   | The app, with feedback from the user.                                             |
| `@version`           | string                                  | The app version, such as `2.4.1`. Used for regressions.                           |
| `@level`             | string                                  | Log events: `trace`, `debug`, `info`, `warn`, `error`, or `fatal`.                |
| `@submission_method` | string                                  | Clients, to record how an event was captured, such as an unhandled error handler. |
| `@stack`             | [Manual stacking](#manual-stacking)     | The app, to choose which stack an event joins.                                    |
| `@trace`             | array of strings                        | Clients that attach recent log lines to an event.                                 |
| `@ref:<name>`        | string                                  | A reference to another event's `reference_id`, such as `@ref:session`.            |
| `@submission_client` | [Submission client](#submission-client) | The server. Anything a client sends here is replaced.                             |
| `@location`          | [Location](#location)                   | The server, from `geo` or the IP address.                                         |
| `sessionend`         | string (date)                           | The server, on a session start event once the session has ended.                  |

### Error {#error}

`@error` holds a structured exception. Inner exceptions nest in `inner`.

| Field           | Type                            | Description                                                               |
| --------------- | ------------------------------- | ------------------------------------------------------------------------- |
| `message`       | string                          | The exception message.                                                    |
| `type`          | string                          | The full exception type name, such as `System.InvalidOperationException`. |
| `code`          | string                          | An error code, if the platform has one.                                   |
| `data`          | object                          | Extra values. `@ext` holds additional exception properties.               |
| `inner`         | object                          | The inner error, with the same fields except `modules`.                   |
| `stack_trace`   | array of [frames](#stack-frame) | The stack frames.                                                         |
| `target_method` | [method](#stack-frame)          | The method that threw.                                                    |
| `modules`       | array of [modules](#module)     | Loaded assemblies or scripts. Only on the outermost error.                |

#### Stack frame {#stack-frame}

A frame is a method plus a location. Methods (`target_method`) have the same fields without `file_name`, `line_number`,
and `column`.

| Field                 | Type             | Description                                                                     |
| --------------------- | ---------------- | ------------------------------------------------------------------------------- |
| `name`                | string           | The method or function name.                                                    |
| `declaring_namespace` | string           | The namespace or module.                                                        |
| `declaring_type`      | string           | The class or type.                                                              |
| `parameters`          | array            | Objects with `name`, `type`, `type_namespace`, `generic_arguments`, and `data`. |
| `generic_arguments`   | array of strings | Generic type arguments.                                                         |
| `is_signature_target` | boolean          | Marks the frame used to build the stack signature.                              |
| `module_id`           | integer          | The `module_id` of the module the frame belongs to.                             |
| `file_name`           | string           | The source file or script URL.                                                  |
| `line_number`         | integer          | The line number.                                                                |
| `column`              | integer          | The column number.                                                              |
| `data`                | object           | Extra values.                                                                   |

#### Module {#module}

| Field           | Type    | Description                             |
| --------------- | ------- | --------------------------------------- |
| `module_id`     | integer | An ID that stack frames refer to.       |
| `name`          | string  | The assembly, package, or script name.  |
| `version`       | string  | The module version.                     |
| `is_entry`      | boolean | Whether this is the app's entry module. |
| `created_date`  | string  | When the module file was created.       |
| `modified_date` | string  | When the module file was last modified. |
| `data`          | object  | Extra values, such as `PublicKeyToken`. |

### Simple error {#simple-error}

`@simple_error` is for errors where you only have text. The server stacks simple errors by `type` and a hash of
`stack_trace`.

| Field         | Type   | Description                                       |
| ------------- | ------ | ------------------------------------------------- |
| `message`     | string | The error message.                                |
| `type`        | string | The error type or class name.                     |
| `stack_trace` | string | The stack trace as one string.                    |
| `data`        | object | Extra values. `@ext` holds additional properties. |
| `inner`       | object | The inner error, with the same fields.            |

### Request {#request}

`@request` describes the HTTP request or browser page the event happened in.

| Field               | Type    | Description                                                         |
| ------------------- | ------- | ------------------------------------------------------------------- |
| `user_agent`        | string  | The request's user agent.                                           |
| `http_method`       | string  | `GET`, `POST`, and so on.                                           |
| `is_secure`         | boolean | Whether the request used HTTPS.                                     |
| `host`              | string  | The host name.                                                      |
| `port`              | integer | The port.                                                           |
| `path`              | string  | The path, without the query string.                                 |
| `referrer`          | string  | The referring URL.                                                  |
| `client_ip_address` | string  | The client's IP address. Several addresses are separated by commas. |
| `headers`           | object  | Header names mapped to arrays of values.                            |
| `cookies`           | object  | Cookie names mapped to values.                                      |
| `post_data`         | any     | The form or body data.                                              |
| `query_string`      | object  | Query parameters mapped to values.                                  |
| `data`              | object  | Extra values, including the browser and OS keys below.              |

The server applies the project's [data exclusions](/docs/project-settings/#data-exclusions) to headers, cookies, query
parameters, and post data, and truncates long values. When the project excludes private information, it removes the
client IP address, cookies, post data, and query string.

#### Browser and OS keys {#browser-and-os-keys}

The server parses `user_agent` and adds these keys to `@request.data`. They are what the browser, OS, and device filters
search.

| Key                      | Example      |
| ------------------------ | ------------ |
| `@browser`               | `Chrome`     |
| `@browser_version`       | `130.0.6723` |
| `@browser_major_version` | `130`        |
| `@device`                | `iPhone`     |
| `@os`                    | `Windows`    |
| `@os_version`            | `10`         |
| `@os_major_version`      | `10`         |
| `@is_bot`                | `false`      |

`@is_bot` is `true` when the user agent is a known crawler or matches the project's bot patterns.

### Environment info {#environment-info}

`@environment` describes the machine and process. Every field is optional.

| Field                       | Type    | Description                                             |
| --------------------------- | ------- | ------------------------------------------------------- |
| `processor_count`           | integer | Logical processors.                                     |
| `total_physical_memory`     | integer | Total memory in bytes.                                  |
| `available_physical_memory` | integer | Free memory in bytes.                                   |
| `command_line`              | string  | The process command line.                               |
| `process_name`              | string  | The process name.                                       |
| `process_id`                | string  | The process ID.                                         |
| `process_memory_size`       | integer | Memory used by the process, in bytes.                   |
| `thread_name`               | string  | The current thread name.                                |
| `thread_id`                 | string  | The current thread ID.                                  |
| `architecture`              | string  | The CPU architecture, such as `x64`.                    |
| `o_s_name`                  | string  | The operating system name.                              |
| `o_s_version`               | string  | The operating system version.                           |
| `ip_address`                | string  | The machine's IP addresses, separated by commas.        |
| `machine_name`              | string  | The host name.                                          |
| `install_id`                | string  | An ID for this installation of the app.                 |
| `runtime_version`           | string  | The runtime version, such as a .NET or Node.js version. |
| `data`                      | object  | Extra values.                                           |

The OS fields are spelled `o_s_name` and `o_s_version`. When the project excludes private information, the server
removes `ip_address` and `machine_name`.

### User {#user}

`@user` identifies the user an event happened to. Events with a user identity are grouped into
[sessions](/docs/user-sessions/) automatically. When the project excludes private information, the server removes
`@user`.

| Field      | Type   | Description                                                       |
| ---------- | ------ | ----------------------------------------------------------------- |
| `identity` | string | A stable ID for the user, such as an account ID or email address. |
| `name`     | string | A display name.                                                   |
| `data`     | object | Extra values.                                                     |

### User description {#user-description}

`@user_description` holds feedback from the user. You can also add it after the fact with
`POST /api/v2/events/by-ref/{referenceId}/user-description`. When the project excludes private information, the server
removes `email_address` from a user description sent with the event, but not from one added later with that endpoint.

| Field           | Type   | Description               |
| --------------- | ------ | ------------------------- |
| `email_address` | string | The user's email address. |
| `description`   | string | What the user was doing.  |
| `data`          | object | Extra values.             |

### Manual stacking {#manual-stacking}

`@stack` overrides how the server groups the event into a stack. See
[Stacking](/docs/concepts/stacking/#manual-stacking).

| Field            | Type   | Description                                                                                      |
| ---------------- | ------ | ------------------------------------------------------------------------------------------------ |
| `title`          | string | The title for the stack.                                                                         |
| `signature_data` | object | String keys and values that identify the stack. Events with the same values join the same stack. |

### Submission client {#submission-client}

The server sets `@submission_client` from the request that delivered the event.

| Field        | Type   | Description                                                                            |
| ------------ | ------ | -------------------------------------------------------------------------------------- |
| `user_agent` | string | The client name, lowercased, from the `X-Exceptionless-Client` or `User-Agent` header. |
| `version`    | string | The client version, when the header has the form `name/version`.                       |
| `ip_address` | string | The IP address the request came from, unless the project excludes private information. |

### Location {#location}

The server sets `@location` from `geo` or from the event's IP addresses.

| Field      | Type   | Description                                        |
| ---------- | ------ | -------------------------------------------------- |
| `country`  | string | The country.                                       |
| `level1`   | string | The first administrative level, such as a state.   |
| `level2`   | string | The second administrative level, such as a county. |
| `locality` | string | The city.                                          |

## Stored events {#stored-events}

Events returned by the API have these extra fields, set by the server. Clients don't send them; an `id` sent with a new
event is ignored, and is used as the `reference_id` if that's empty.

| Field                 | Type    | Description                               |
| --------------------- | ------- | ----------------------------------------- |
| `id`                  | string  | The event ID.                             |
| `organization_id`     | string  | The organization the event belongs to.    |
| `project_id`          | string  | The project the event belongs to.         |
| `stack_id`            | string  | The stack the event was grouped into.     |
| `is_first_occurrence` | boolean | Whether this event created its stack.     |
| `created_utc`         | string  | When the server stored the event, in UTC. |
| `idx`                 | object  | Search index values copied from `data`.   |
