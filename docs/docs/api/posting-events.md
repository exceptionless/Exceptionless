---
title: "Posting events"
description: "Send errors, logs, feature usage, and custom events to Exceptionless with the REST API, in batches, as plain text, or with a GET request."
sources:
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/EventHandler.cs
  - src/Exceptionless.Web/Utility/Handlers/OverageMiddleware.cs
  - src/Exceptionless.Web/Extensions/HttpExtensions.cs
  - src/Exceptionless.Core/Jobs/EventPostsJob.cs
  - src/Exceptionless.Core/Plugins/EventParser/Default/JsonEventParserPlugin.cs
  - src/Exceptionless.Core/Plugins/EventParser/Default/FallbackEventParserPlugin.cs
  - src/Exceptionless.Core/Pipeline/001_CheckEventDateAction.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/05_CheckForDuplicateReferenceIdPlugin.cs
---

# Posting events

Send events to Exceptionless with `POST /api/v2/events` and a [client API key](/docs/api/project-tokens/). The key
decides which project the events go to. This page shows the common kinds of events; every field is described in
[Event format](/docs/reference/event-format/).

## Send an event {#send-an-event}

```bash
curl https://api.exceptionless.io/api/v2/events \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data '{ "type": "log", "message": "Exceptionless is set up" }'
```

The server responds `202 Accepted` with an empty body. Events are processed in the background and appear in the app
within a few seconds.

Every field is optional, but send at least a `message`, an error, or a `source` so the event has something to show.
Without a `type`, the event is a log, or an error if it has error data. Without a `date`, it gets the time the server
processed it.

## Send several events {#batch}

Send an array to submit a batch in one request:

```bash
curl https://api.exceptionless.io/api/v2/events \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data '[
    { "type": "usage", "source": "Export to CSV" },
    { "type": "log", "message": "Nightly import finished", "data": { "@level": "info", "rows": 1204 } }
  ]'
```

Keep each request under 200,000 bytes. A larger body gets `413`; split it into smaller batches. You can compress the
body with `--header "Content-Encoding: gzip"` and gzipped data.

## Send a log message {#log}

Put the level in `@level` and the logger name in `source`. Levels are `trace`, `debug`, `info`, `warn`, `error`, and
`fatal`.

```bash
curl https://api.exceptionless.io/api/v2/events \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data '{
    "type": "log",
    "source": "Billing.InvoiceJob",
    "message": "Invoice run took longer than expected",
    "date": "2026-10-27T09:30:00-05:00",
    "data": {
      "@level": "warn",
      "@user": { "identity": "user-1042", "name": "Ana Lopez" }
    }
  }'
```

Log levels set in the project's settings are applied by the client libraries, not by the server, so an event you post
directly isn't filtered by level. See [Logs and log levels](/docs/setting-log-levels/).

## Send an error {#error}

When you only have a message and a stack trace as text, use `@simple_error`. Exceptionless groups simple errors into
stacks by `type` and stack trace.

```bash
curl https://api.exceptionless.io/api/v2/events \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data '{
    "type": "error",
    "reference_id": "a1b2c3d4e5",
    "data": {
      "@simple_error": {
        "message": "Payment provider timed out",
        "type": "TimeoutError",
        "stack_trace": "TimeoutError: Payment provider timed out\n    at charge (payments.py:42)\n    at checkout (orders.py:118)"
      },
      "@version": "2.4.1"
    }
  }'
```

For structured stack frames, use `@error` instead; see [Error](/docs/reference/event-format/#error). Set `@version` to
your app's version so a fixed stack can [regress](/docs/versioning/) when the error returns in a newer version.

## Send feature usage {#feature-usage}

```bash
curl https://api.exceptionless.io/api/v2/events \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data '{ "type": "usage", "source": "Export to CSV", "data": { "@user": { "identity": "user-1042" } } }'
```

See [Feature usage](/docs/feature-usage/).

## Send plain text {#plain-text}

With `Content-Type: text/plain`, each line of the body becomes a log event:

```bash
printf 'Backup started\nBackup finished\n' | curl https://api.exceptionless.io/api/v2/events \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: text/plain" \
  --data-binary @-
```

## Send an event with a GET request {#get}

For tools that can only make a GET request, such as a monitoring webhook, put the event in the query string of
`/api/v2/events/submit`:

```bash
curl "https://api.exceptionless.io/api/v2/events/submit?access_token=YOUR_API_KEY&type=usage&source=build&value=10"
```

These parameters set event fields: `type` (default `log`), `source`, `message`, `reference`, `date`, `count`, `value`,
`geo`, `tags` (comma-separated), `environment`, `identity`, and `identity.name`. Every other parameter is added to the
event's data, unless it matches the project's [data exclusions](/docs/project-settings/#data-exclusions). The server
responds `200`.

You can also put the type in the path, as in `/api/v2/events/submit/usage?source=build`.

## Post to a specific project {#project}

`POST /api/v2/projects/{projectId}/events` sends events to the project in the path. Use it with a token that isn't tied
to one project, such as a [user token](/docs/api/authentication/#user-tokens). With a client API key, the project in the
path must be the key's project, or the server responds `404`.

## Add a user description {#user-description}

To attach a user's email address and comments to an event you sent with a `reference_id`:

```bash
curl https://api.exceptionless.io/api/v2/events/by-ref/a1b2c3d4e5/user-description \
  --request POST \
  --header "Authorization: Bearer YOUR_API_KEY" \
  --header "Content-Type: application/json" \
  --data '{ "email_address": "ana@example.com", "description": "I clicked Pay twice." }'
```

The server responds `202`. See [Reference IDs](/docs/references-ids/).

## Responses {#responses}

| Status | What to do                                                                              |
| ------ | --------------------------------------------------------------------------------------- |
| `202`  | Nothing. The events were accepted.                                                      |
| `400`  | Check the request. A user token with no project needs the [project route](#project).    |
| `401`  | Check the API key. It may be wrong or disabled.                                         |
| `402`  | The organization has used its events for the month. Wait for the next month or upgrade. |
| `411`  | Send a body.                                                                            |
| `413`  | Send fewer events per request.                                                          |
| `415`  | Use `Content-Type: application/json` or `text/plain`.                                   |
| `429`  | Slow down until the next 15-minute window. See [Rate limits](/docs/api/#rate-limits).   |
| `503`  | The server isn't accepting events right now. Try again in a few minutes.                |

If you're writing a client library that retries and queues, follow the
[client specification](/docs/reference/client-spec/#responses).

## If an event doesn't show up {#troubleshooting}

A `202` means the server stored the request, not that every event was kept. Check these if an event is missing:

- **The date.** Events dated more than three days ago are discarded. Dates in the future are moved to now.
- **The reference ID.** A second event with the same `reference_id` in the same project within a day is discarded.
- **The project's usage.** Events over the plan's monthly limit are discarded. The project's **Usage** settings show
  blocked events.
- **The body.** A body that isn't a JSON object or array is stored as log events, one per line.

## Use a coding agent {#coding-agent}

```prompt agent
Add a small Exceptionless reporter to this project that posts unhandled errors to https://api.exceptionless.io/api/v2/events with the API key from the EXCEPTIONLESS_API_KEY environment variable, using @simple_error with the error type, message, and stack trace, and never blocking or crashing the app if the request fails.
```
