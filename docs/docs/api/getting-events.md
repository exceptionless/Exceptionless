---
title: "Querying events and stacks"
navTitle: "Querying events"
description: "Read events and stacks from the Exceptionless REST API: count, list, and filter them, get one by ID, and page through results with cursors."
sources:
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/EventHandler.cs
  - src/Exceptionless.Web/Api/Endpoints/StackEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/StackHandler.cs
  - src/Exceptionless.Web/Api/Infrastructure/Pagination.cs
  - src/Exceptionless.Web/Api/Infrastructure/TimeRangeParser.cs
  - src/Exceptionless.Web/Api/Results/PagedResult.cs
  - src/Exceptionless.Web/Api/Results/ApiResults.cs
  - src/Exceptionless.Core/Models/PersistentEvent.cs
  - src/Exceptionless.Core/Models/Stack.cs
---

# Querying events and stacks

Read your events and stacks into scripts, dashboards, and other tools. Reading needs a
[user token](/docs/api/authentication/#user-tokens) or an
[OAuth access token](/docs/api/authentication/#oauth-access-tokens) with the `events:read` or `stacks:read` scope.
Client API keys can only send events, and get `403` here.

Responses use `snake_case` property names, like the rest of the API.

## Count events {#count}

```bash
curl --get https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/events/count \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --data-urlencode "filter=type:error" \
  --data-urlencode "time=[now-24h TO now]"
```

```json
{ "total": 74 }
```

`GET /api/v2/events/count` counts across all your organizations, and
`GET /api/v2/organizations/{organizationId}/events/count` across one organization.

## List events {#list}

```bash
curl --get https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/events \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --data-urlencode "filter=type:error environment:production" \
  --data-urlencode "time=[now-7d TO now]" \
  --data-urlencode "limit=25"
```

The response is an array of events, newest first. This example shows one event, trimmed:

```json
[
  {
    "id": "6720f3b5c1f7a2d9e4b81c22",
    "organization_id": "537650f3b77efe23a47914f3",
    "project_id": "537650f3b77efe23a47914f4",
    "stack_id": "6720f3b5c1f7a2d9e4b81c20",
    "is_first_occurrence": false,
    "created_utc": "2026-10-27T19:03:13.104Z",
    "type": "error",
    "date": "2026-10-27T14:03:12.512-05:00",
    "message": "Payment provider timed out",
    "environment": "production",
    "reference_id": "a1b2c3d4e5",
    "tags": ["checkout"],
    "data": {
      "@simple_error": { "message": "Payment provider timed out", "type": "TimeoutError" },
      "@version": "2.4.1"
    }
  }
]
```

Every field is described in [Event format](/docs/reference/event-format/).

These routes return events and take the same parameters:

| Route                                               | Events from                                                               |
| --------------------------------------------------- | ------------------------------------------------------------------------- |
| `GET /api/v2/events`                                | All your organizations.                                                   |
| `GET /api/v2/organizations/{organizationId}/events` | One organization.                                                         |
| `GET /api/v2/projects/{projectId}/events`           | One project.                                                              |
| `GET /api/v2/stacks/{stackId}/events`               | One stack.                                                                |
| `GET /api/v2/events/by-ref/{referenceId}`           | Events with this reference ID.                                            |
| `GET /api/v2/events/sessions/{sessionId}`           | Events in one session.                                                    |
| `GET /api/v2/projects/{projectId}/events/sessions`  | Session start events in one project. Also by organization and across all. |

The session routes need a paid plan and return `426` on the free plan.

### Parameters {#parameters}

| Parameter         | Description                                                                                                                                          |
| ----------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `filter`          | A search query, such as `type:error environment:production`. See [Search syntax](/docs/reference/search-syntax/).                                    |
| `time`            | A date range on the event date, such as `[now-7d TO now]` or `[2026-10-01 TO 2026-10-31]`. Leave it out for all events within your plan's retention. |
| `offset`          | Your time zone offset, such as `-300m`, so ranges like `now/d` (today) start at your midnight.                                                       |
| `sort`            | A field to sort by, with `-` for descending. The default is `-date`.                                                                                 |
| `limit`           | Results per page, from 1 to 100. The default is 10.                                                                                                  |
| `before`, `after` | Cursors for the previous and next page. See [Paging](#paging).                                                                                       |
| `page`            | A page number, for page-based paging instead of cursors.                                                                                             |
| `include`         | `total` adds the total number of matches in the `X-Result-Count` header.                                                                             |
| `mode`            | `summary` returns short summaries instead of full events.                                                                                            |

On the free plan, `filter` can only use `type`, `environment`, `date`, `reference_id`, `status`, and organization,
project, and stack IDs. Other fields, such as tags and custom data, need a paid plan and return `426` on the free plan.

## Get one event {#get-event}

```bash
curl https://api.exceptionless.io/api/v2/events/6720f3b5c1f7a2d9e4b81c22 \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --include
```

The response is the full event. Its `Link` header points to the previous and next events in the same stack and to the
stack itself:

```http
Link: <https://api.exceptionless.io/api/v2/events/6720f3b5c1f7a2d9e4b81c21>; rel="previous", <https://api.exceptionless.io/api/v2/events/6720f3b5c1f7a2d9e4b81c23>; rel="next", <https://api.exceptionless.io/api/v2/stacks/6720f3b5c1f7a2d9e4b81c20>; rel="parent"
```

An event older than the organization's data retention returns `426`.

## Query stacks {#stacks}

A stack groups events with the same cause. To list a project's open stacks, most recent first:

```bash
curl --get https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/stacks \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --data-urlencode "filter=status:open type:error" \
  --data-urlencode "sort=-last" \
  --data-urlencode "limit=25"
```

The response is an array of stacks. This example is trimmed:

```json
[
  {
    "id": "6720f3b5c1f7a2d9e4b81c20",
    "organization_id": "537650f3b77efe23a47914f3",
    "project_id": "537650f3b77efe23a47914f4",
    "type": "error",
    "status": "open",
    "title": "Payment provider timed out",
    "total_occurrences": 31,
    "first_occurrence": "2026-10-20T08:11:02Z",
    "last_occurrence": "2026-10-27T19:03:12Z",
    "occurrences_are_critical": false,
    "references": [],
    "tags": ["checkout"],
    "created_utc": "2026-10-20T08:11:03Z",
    "updated_utc": "2026-10-27T19:03:13Z"
  }
]
```

- `GET /api/v2/stacks`, `GET /api/v2/organizations/{organizationId}/stacks`, and
  `GET /api/v2/projects/{projectId}/stacks` list stacks. They take `filter`, `sort`, `time`, `offset`, `mode`, `page`,
  and `limit`, and page by page number.
- `time` on stacks matches the last occurrence. Prefix it with `first|` to match the first occurrence instead, as in
  `first|[now-7d TO now]`, to find new stacks.
- Stack statuses are `open`, `fixed`, `regressed`, `snoozed`, `ignored`, and `discarded`.
- `GET /api/v2/stacks/{stackId}` returns one stack.

### Rank stacks by activity {#stack-modes}

To get the stacks with the most events in a time range, the way the app's stack lists work, query the events route with
a stack `mode`:

```bash
curl --get https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/events \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --data-urlencode "mode=stack_frequent" \
  --data-urlencode "time=[now-24h TO now]"
```

| Mode             | Orders stacks by                              |
| ---------------- | --------------------------------------------- |
| `stack_frequent` | Most occurrences in the range.                |
| `stack_recent`   | Most recent occurrence.                       |
| `stack_new`      | Stacks first seen in the range, newest first. |
| `stack_users`    | Most users affected.                          |

Each result is a stack summary with `title`, `status`, `first_occurrence`, `last_occurrence`, `total` (occurrences in
the range), and `users`. Stack modes don't accept `sort` and page by page number.

## Paging {#paging}

List responses are JSON arrays. Paging information is in the response headers:

- **`Link`** has the URL of the next page (`rel="next"`) when there are more results, and of the previous page
  (`rel="previous"`). Follow these URLs instead of building your own.
- **`X-Result-Count`** has the total number of matches when you ask for it with `include=total`.

Event lists use cursors. Leave out `page`, and the `Link` URLs carry an `after` or `before` cursor that marks your
place, so you can page through any number of events. Keep the other parameters the same between pages.

```bash
curl --get https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/events \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --data-urlencode "limit=100" \
  --dump-header - \
  --output events.json
```

```http
Link: <https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/events?limit=100&before=WzE3...>; rel="previous", <https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/events?limit=100&after=WzE3...>; rel="next"
```

Stacks, projects, tokens, and the stack modes page by number: send `page=2`, `page=3`, and so on. For events and stacks,
page-based paging stops at 1,000 results; past that, a request returns an empty array. Use cursors, or narrow the `time`
range or `filter`, to reach older results.

## Use a coding agent {#coding-agent}

```prompt agent
Write a script that uses my Exceptionless user token from an environment variable to export every error event from the last 7 days for project YOUR_PROJECT_ID to a JSON Lines file, following the Link header's next URL until there are no more pages.
```
