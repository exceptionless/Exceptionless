---
title: "REST API"
description: "The Exceptionless REST API: base URL, the interactive API reference and OpenAPI document, response conventions, status codes, and rate limits."
navTitle: "Overview"
sources:
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Web/Api/ApiEndpoints.cs
  - src/Exceptionless.Web/Api/Results/ApiResultMapper.cs
  - src/Exceptionless.Web/Api/Results/PagedResult.cs
  - src/Exceptionless.Web/Api/Infrastructure/ApiValidation.cs
  - src/Exceptionless.Web/Utility/Headers.cs
  - src/Exceptionless.Web/Utility/Handlers/ThrottlingMiddleware.cs
  - src/Exceptionless.Web/Utility/Handlers/OverageMiddleware.cs
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/help-links.ts
---

# REST API

Everything the Exceptionless app does goes through a REST API, and you can call it too: send events from any language,
read events and stacks into your own tools, and manage projects and tokens.

To send events from a script or a language without a client library, start with
[Posting events](/docs/api/posting-events/) or [Other languages and custom clients](/docs/clients/custom-clients/).

## Base URL {#base-url}

```text
https://api.exceptionless.io/api/v2
```

Every route in these docs is under `/api/v2`.

> [!NOTE]
> Self-hosted instances serve the API from the same host as the app, for example
> `https://exceptionless.example.com/api/v2`.

## API reference {#api-reference}

The API documents itself:

- **Interactive reference:** [https://api.exceptionless.io/docs](https://api.exceptionless.io/docs) lists every route,
  parameter, and response, and lets you send requests with your token.
- **OpenAPI document:**
  [https://api.exceptionless.io/docs/v2/openapi.json](https://api.exceptionless.io/docs/v2/openapi.json), for code
  generators and API tools.

In the app, open the user menu and choose **Help** > **API Reference**. Self-hosted instances serve both at `/docs` and
`/docs/v2/openapi.json` on their own host.

## Authentication {#authentication}

Send a token in the `Authorization` header:

```bash
curl https://api.exceptionless.io/api/v2/projects \
  --header "Authorization: Bearer YOUR_API_KEY"
```

Which token you need depends on the route. A client-scoped API key can send events; reading data and managing your
account need a user token or an OAuth token. See [Authentication](/docs/api/authentication/).

## Conventions {#conventions}

- **JSON in and out.** Request and response bodies use `snake_case` property names, such as `project_id` and
  `created_utc`. Properties with no value are left out of responses.
- **Dates** are ISO 8601 strings. Fields that end in `_utc` are in UTC.
- **IDs** of organizations, projects, stacks, and events are 24-character hexadecimal strings. Several IDs in one path
  segment are separated by commas, as in `DELETE /api/v2/events/{id1},{id2}`.
- **Lists** return a JSON array. Paging information is in the `Link` and `X-Result-Count` headers; see
  [Paging](/docs/api/getting-events/#paging).
- **Errors** return a problem details object (`application/problem+json`) with a `title`, the `status`, and an
  `instance` naming the method and path. Validation errors add an `errors` object keyed by field name.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Project not found.",
  "status": 404,
  "instance": "GET /api/v2/projects/000000000000000000000000/events"
}
```

## Status codes {#status-codes}

| Code  | Meaning                                                                                                      |
| ----- | ------------------------------------------------------------------------------------------------------------ |
| `200` | Success. The body has the result.                                                                            |
| `201` | Created. The `Location` header points to the new resource.                                                   |
| `202` | Accepted for background processing. Event submissions, user descriptions, and bulk deletes return this.      |
| `304` | Not modified. Returned by `GET /api/v2/projects/config` when the `v` you sent is current.                    |
| `400` | Bad request, such as an invalid filter, or an event submitted with a user token that has no default project. |
| `401` | No token, or the token is invalid, expired, disabled, or belongs to a suspended organization.                |
| `402` | Payment required. The organization has used its plan's events for the month, so new events are rejected.     |
| `403` | The token is valid but not allowed to do this. For example, a client-scoped key can't read events.           |
| `404` | Not found, or you don't have access to it.                                                                   |
| `409` | Conflict, such as a duplicate.                                                                               |
| `411` | An event submission had an empty body.                                                                       |
| `413` | The request body is larger than the event size limit (200,000 bytes by default). Send smaller batches.       |
| `415` | Unsupported content type. Event submissions accept `application/json` and `text/plain`.                      |
| `422` | Validation failed. The `errors` object says which fields.                                                    |
| `426` | Upgrade required. The query uses a feature your plan doesn't include, or the organization is suspended.      |
| `429` | Too many requests. See [Rate limits](#rate-limits).                                                          |
| `500` | Server error. The response may include a `reference-id` to give to support.                                  |
| `503` | The server isn't accepting events right now. Retry later.                                                    |

## Rate limits {#rate-limits}

The API counts requests in 15-minute windows. Self-hosted instances allow 3,500 requests per window by default
(`ApiThrottleLimit`). Responses tell you the limit that applies to you:

| Header                  | Value                                   |
| ----------------------- | --------------------------------------- |
| `X-RateLimit-Limit`     | Requests allowed in the current window. |
| `X-RateLimit-Remaining` | Requests left in the current window.    |

Requests over the limit get `429 Too Many Requests` until the next window. The limit applies per organization for API
keys and OAuth tokens, per user for user tokens, and per IP address for requests without a token. These requests don't
count: `GET /api/v2/projects/config`, `GET /api/v2/events/session/heartbeat`, and the app's live update connection.

Event submissions also count against the organization's monthly event limit. When it's used up, submissions get `402`
until the plan resets or you upgrade. See [Limits and deduplication](/docs/concepts/limits/).

## Browser requests {#cors}

The API allows cross-origin requests from any site, so browser apps can call it directly. These response headers are
readable from JavaScript: `ETag`, `Link`, `X-Result-Count`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, and
`X-Exceptionless-ConfigVersion`.

## MCP server {#mcp}

AI tools connect to Exceptionless through the MCP server at `https://api.exceptionless.io/mcp`, which uses OAuth sign-in
and its own scopes. See [MCP server](/docs/ai/mcp-server/).
