---
title: "Client specification"
description: "What every Exceptionless client does: authentication, batching, the offline queue, retries and status codes, configuration sync, sessions, and data exclusions."
navTitle: "Client specification"
sources:
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Endpoints/ProjectEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/EventHandler.cs
  - src/Exceptionless.Web/Api/Filters/ConfigurationResponseEndpointFilter.cs
  - src/Exceptionless.Web/Utility/Headers.cs
  - src/Exceptionless.Web/Utility/Handlers/OverageMiddleware.cs
  - src/Exceptionless.Web/Utility/Handlers/ThrottlingMiddleware.cs
  - src/Exceptionless.Web/Utility/Handlers/ProjectConfigMiddleware.cs
  - src/Exceptionless.Web/Utility/Handlers/RecordSessionHeartbeatMiddleware.cs
  - src/Exceptionless.Web/Extensions/HttpExtensions.cs
  - src/Exceptionless.Web/Security/ApiKeyAuthenticationHandler.cs
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Core/Models/ClientConfiguration.cs
  - src/Exceptionless.Core/Models/Collections/SettingsDictionary.cs
  - src/Exceptionless.Core/Jobs/EventPostsJob.cs
  - src/Exceptionless.Core/Jobs/CloseInactiveSessionsJob.cs
  - src/Exceptionless.Core/Pipeline/001_CheckEventDateAction.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/05_CheckForDuplicateReferenceIdPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/07_SubmissionClientPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/70_SessionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Submission/DefaultSubmissionClient.cs
  - Exceptionless.Net:src/Exceptionless/Queue/DefaultEventQueue.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/ExceptionlessConfiguration.cs
  - Exceptionless.Net:src/Exceptionless/Configuration/SettingsManager.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/025_SessionIdManagementPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/100_HeartbeatPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/910_DuplicateCheckerPlugin.cs
  - Exceptionless.JavaScript:packages/core/src/submission/DefaultSubmissionClient.ts
  - Exceptionless.JavaScript:packages/core/src/queue/DefaultEventQueue.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/SettingsManager.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/SessionIdManagementPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/HeartbeatPlugin.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/DuplicateCheckerPlugin.ts
---

# Client specification

This page defines what an Exceptionless client does. It is the contract for new official clients and for community
clients, and it describes how the .NET and JavaScript clients behave today. "Must" marks behavior the server depends on;
"should" marks behavior users expect from every client.

The events themselves are described in [Event format](/docs/reference/event-format/). To send a few events from a script
without writing a client, see [Other languages and custom clients](/docs/clients/custom-clients/).

## Endpoints {#endpoints}

All paths are relative to the server URL, `https://api.exceptionless.io` for the hosted service.

| Method and path                                             | Purpose                                                                                        |
| ----------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| `POST /api/v2/events`                                       | Submit a batch of events.                                                                      |
| `GET /api/v2/projects/config?v={version}`                   | Get the project's [configuration values](#configuration-values).                               |
| `GET /api/v2/events/session/heartbeat?id={id}&close={bool}` | Keep a [session](#sessions) open, or close it.                                                 |
| `POST /api/v2/events/by-ref/{referenceId}/user-description` | Attach a user's email address and description to an event.                                     |
| `GET /api/v2/events/submit?type={type}&...`                 | Submit one event with query string parameters, for platforms that can only make a GET request. |

## Configuration {#configuration}

A client must let the app set:

- **API key.** A client-scoped key from the project's **API Keys** settings (see
  [Client-scoped tokens](/docs/api/project-tokens/)). Both official clients treat a key shorter than 10 characters as
  invalid and send nothing.
- **Server URL.** Defaults to `https://api.exceptionless.io`. Self-hosted users set it to their own URL.
- **Enabled.** A switch that turns off all submission.

A client should also let the app set default tags and data added to every event, the app version (`@version`), local
data exclusions, and whether to include private information.

## Authentication {#authentication}

Send the API key in the `Authorization` header on every request:

```http
Authorization: Bearer YOUR_API_KEY
```

The server also accepts the key in an `access_token` query parameter. Use that only for `GET /api/v2/events/submit` on
platforms that cannot set headers, because URLs end up in logs.

## User agent {#user-agent}

Identify the client with a `name/version` value:

```http
User-Agent: exceptionless-ruby/1.0.0
```

The server stores the name and version in each event's `@submission_client`. If the platform doesn't let you set
`User-Agent`, as in browsers, send the same value in `X-Exceptionless-Client`; the server prefers it when present.

Pick a name of your own. The official clients use `exceptionless` (.NET) and `exceptionless-js` (JavaScript), and the
server treats those two names differently when it picks which IP address belongs to the user.

## Submission and batching {#submission}

A client must not block the app while it sends events. Both official clients work the same way:

1. Run plugins on the event (defaults, exclusions, deduplication), then add it to an in-memory queue.
2. Every 10 seconds, take up to 50 events (the default batch size) from the queue.
3. `POST` them to `/api/v2/events` as a JSON array with `Content-Type: application/json`.
4. Remove the batch from the queue when the server responds `202 Accepted`.

```http
POST /api/v2/events HTTP/1.1
Host: api.exceptionless.io
Authorization: Bearer YOUR_API_KEY
User-Agent: exceptionless-ruby/1.0.0
Content-Type: application/json

[{"type":"log","message":"Order placed","date":"2026-10-27T14:03:12-05:00","data":{"@level":"info"}}]
```

Rules for the request body:

- Send `application/json`. The server also accepts `text/plain`, where each line becomes a log event. Other content
  types get `415 Unsupported Media Type`.
- Compress with `Content-Encoding: gzip` or `deflate` if you like. The .NET client compresses bodies over 4 KB.
- Don't send an empty body. A `POST` with `Content-Length: 0` gets `411 Length Required`.
- Keep the body under the server's size limit, 200,000 bytes by default (`MaximumEventPostSize` on self-hosted servers).
  A compressed body may expand to ten times the limit; larger payloads are dropped when processed.

`202 Accepted` means the server stored the payload for processing. Events are parsed and processed in the background, so
an accepted event can still be discarded later, for example when the organization is over its plan limit or the date is
too old.

## Offline queue {#offline-queue}

Events stay in the queue until the server accepts them, so a client keeps working while the network or the server is
down.

- **Cap the queue.** The JavaScript client keeps at most 250 events and drops the oldest. The .NET client deletes queued
  events after 7 days or 3 failed attempts.
- **Persist it where you can.** Both clients keep the queue in memory by default. The JavaScript client can store it in
  `localStorage` (`usePersistedQueueStorage`), and the .NET client can store it in a folder.
- **Don't hold events too long.** The server discards events dated more than three days in the past.
- **Flush on shutdown.** Process the queue once more when the app exits or the page is hidden.

## Responses and retries {#responses}

After each batch, the client decides whether to keep the events and how long to pause. The official clients do this:

| Status                         | Meaning                                                                      | What the client does                                                                                             |
| ------------------------------ | ---------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `202`                          | Accepted.                                                                    | Remove the batch.                                                                                                |
| `400`                          | The request was rejected, for example no project could be found for the key. | Drop the batch and pause for 4 hours.                                                                            |
| `401`, `403`                   | The key is missing, wrong, disabled, or its organization is suspended.       | Drop the batch and pause for 15 minutes.                                                                         |
| `402`                          | The organization has used its plan's events for the month.                   | Drop the batch, clear the queue, and discard new events until the pause ends.                                    |
| `404`                          | The project wasn't found, or the server URL is wrong.                        | Drop the batch and pause for 4 hours.                                                                            |
| `413`                          | The body is too large.                                                       | Retry with a smaller batch (both clients divide the batch size by 1.5). If a single event is too large, drop it. |
| `429`                          | Too many requests. See [rate limits](/docs/api/#rate-limits).                | Keep the batch and pause.                                                                                        |
| `503`                          | The server isn't accepting events right now.                                 | Keep the batch and pause.                                                                                        |
| Other errors, network failures | Anything else.                                                               | Keep the batch and pause.                                                                                        |

A pause stops queue processing for a while; events submitted during a pause still go into the queue, except after a
`402`. The .NET client pauses for 5 minutes by default, and the JavaScript client pauses until the next quarter hour. A
client should also slow down when `X-RateLimit-Remaining` reaches `0`. Never retry in a tight loop.

## Configuration values {#configuration-values}

Each project has a set of key-value settings, edited in the project's **Configuration** settings, that the server sends
to clients. Clients use them for data exclusions and event filtering, and apps can read their own values. See
[Projects](/docs/project-settings/#client-configuration).

```http
GET /api/v2/projects/config?v=3 HTTP/1.1
Authorization: Bearer YOUR_API_KEY
```

- If `v` matches the project's current version, the server responds `304 Not Modified`.
- Otherwise it responds `200` with the version and the settings:

```json
{
  "version": 4,
  "settings": {
    "@@DataExclusions": "*Password*,CreditCard",
    "@@log:*": "info",
    "FeatureFlagCheckout": "true"
  }
}
```

A client must keep settings current:

1. On startup, load the last saved settings and version, then fetch settings shortly after startup (the official clients
   wait 5 to 15 seconds).
2. Read `X-Exceptionless-ConfigVersion` from every `200` or `202` response to an event or user-description submission.
   When it's higher than the saved version, fetch settings.
3. While the app is idle, fetch settings on an interval: every 2 minutes by default, and never more often.
4. Save the settings and version so they apply on the next start. Replace the previous server settings; remove keys the
   server no longer sends.

The configuration endpoint and the heartbeat endpoint don't count toward the API rate limit.

### Reserved settings {#reserved-settings}

| Key                           | Value                                                                                         | Used by                                                                                                           |
| ----------------------------- | --------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `@@DataExclusions`            | Comma-separated patterns.                                                                     | Clients and the server. See [data exclusions](#data-exclusions).                                                  |
| `@@UserAgentBotPatterns`      | Comma-separated user agent patterns.                                                          | Clients drop events whose request user agent matches. The server marks matching requests as bots.                 |
| `@@log:{source}`              | Minimum log level: `trace`, `debug`, `info`, `warn`, `error`, `fatal`, or `off`, in any case. | Clients drop log events below the level for that source.                                                          |
| `@@{type}:{source}`           | `false` to drop events of that type and source.                                               | Clients, for types other than `log`. For errors, the source is the exception type, checked for every inner error. |
| `@@IncludePrivateInformation` | `false` to strip private information.                                                         | The server only. Clients don't need to read it.                                                                   |

`{source}` can be a wildcard pattern. When several keys match, the official clients use the longest key. A log event
with no `@level` is never dropped by level.

## Data exclusions {#data-exclusions}

Data exclusions remove sensitive values before an event leaves the app. A client must combine its local exclusions with
the `@@DataExclusions` setting and remove every matching key from:

- custom data and the `data` of errors and other reserved objects,
- request headers, cookies, query string parameters, and form data.

Patterns match keys without regard to case. `*` at the start, end, or both matches any text, so `*password*` matches
`UserPassword`. The server also applies the project's exclusions to request data it receives, but the client should not
rely on that.

## Private information {#private-information}

A client should let the app turn off sending private information: the user name, machine name, IP address, request
headers, cookies, post data, and query string. Both official clients have one `includePrivateInformation` switch for all
of these. When the project turns off private information in its settings, the server removes the IP address, machine
name, cookies, post data, and query string from events it receives.

## Deduplication {#deduplication}

A client should merge repeated events instead of sending each one, so a loop that throws the same error doesn't use up
the plan. Both official clients:

1. Compute a hash for each event. The .NET client hashes the whole event; the JavaScript client hashes error messages
   and stack traces and only deduplicates errors.
2. Remember the last 50 hashes and when they were seen.
3. When an event matches a hash seen within the window (60 seconds in .NET, 30 seconds in JavaScript), hold it instead
   of sending it, and add later matches to its `count`.
4. Send held events on a timer that runs once per window, with the total `count` and the latest `date`.

See [Limits and deduplication](/docs/concepts/limits/#deduplication) for how this looks in the app.

## Sessions {#sessions}

Sessions show what a user did between starting and leaving the app. See [Sessions](/docs/user-sessions/).

**Starting a session.** Submit an event with `type` `session`. There are two ways to tie events to it:

- **By user.** Set `@user.identity` on every event. The server groups each user's events into sessions automatically.
  The .NET client drops a session start event that has no user identity.
- **By session ID.** Generate a session ID (8 to 100 letters, digits, or hyphens; both clients use a GUID without
  hyphens). Put it in the session start event's `reference_id`, and add `"@ref:session": "<session id>"` to the `data`
  of every other event in the session.

**Heartbeats.** While the session is active, send a heartbeat every minute:

```http
GET /api/v2/events/session/heartbeat?id=SESSION_ID_OR_USER_IDENTITY HTTP/1.1
Authorization: Bearer YOUR_API_KEY
```

`id` is the session ID, or the user identity when sessions are tied to the user. The official clients send a heartbeat
one minute after the last event and every minute after that, and fall back to one minute if the app asks for an interval
under 30 seconds. The endpoint returns `200`.

**Ending a session.** Send the heartbeat with `close=true` when the app exits or the user signs out, or submit an event
with `type` `sessionend`. The server also closes a session after 5 minutes with no events or heartbeats.

## User descriptions {#user-descriptions}

To let users describe what they were doing when an error happened, show them the event's reference ID and send their
answer to the event later:

```http
POST /api/v2/events/by-ref/a1b2c3d4e5/user-description HTTP/1.1
Authorization: Bearer YOUR_API_KEY
Content-Type: application/json

{"email_address":"ana@example.com","description":"I clicked Pay twice."}
```

The server responds `202`. At least one of `email_address` and `description` is required. Clients that support this
should set a `reference_id` on every error event; both official clients can generate a 10-character ID.

## Environment {#environment}

A client should let the app set a default deployment environment and override it per event, and send it in the top-level
`environment` field. See [Event format](/docs/reference/event-format/#environment) for the rules and
[Environments](/docs/environments/) for how the app uses it.

## Errors in the client {#client-errors}

A client must never throw into the app. Catch errors in plugins, serialization, and network calls, log them to the
client's own log, and keep going.
