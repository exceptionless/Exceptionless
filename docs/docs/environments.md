---
title: "Environments"
description: "Tag Exceptionless events with a deployment environment such as production or staging, then filter and compare every list by environment."
sources:
  - src/Exceptionless.Core/Models/Event.cs
  - src/Exceptionless.Core/Serialization/EventEnvironmentConverter.cs
  - src/Exceptionless.Web/Api/Handlers/EventHandler.cs
  - src/Exceptionless.Web/Api/Endpoints/EventEndpoints.cs
  - src/Exceptionless.Core/Repositories/Configuration/Indexes/EventIndex.cs
  - src/Exceptionless.Core/Repositories/Queries/Validation/PersistentEventQueryValidator.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/70_SessionPlugin.cs
  - src/Exceptionless.Core/Migrations/010_AddEventEnvironment.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/environment-faceted-filter.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/filters/models.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/overview.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/table/options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/sessions/components/session-table-columns.ts
  - tests/http/events.http
  - Exceptionless.Net:src/Exceptionless/Models/Client/Event.cs
  - Exceptionless.JavaScript:packages/core/src/models/Event.ts
---

# Environments

An environment names the deployment an event came from, such as `production`, `staging`, or `development`. With
environments, one project can hold every deployment of an app, and you can filter any list down to one of them.

Each event has at most one environment. The same error in staging and production is still one stack, with one status and
one fixed version.

## Set an event's environment

An event gets an environment from the `environment` property at the top level of the event:

```bash tab="POST"
curl -X POST "https://api.exceptionless.io/api/v2/events" \
  -H "Authorization: Bearer YOUR_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "type": "log",
    "message": "Deployment complete",
    "environment": "production"
  }'
```

```bash tab="GET"
curl "https://api.exceptionless.io/api/v2/events/submit?access_token=YOUR_API_KEY&type=log&message=Deployment+complete&environment=production"
```

The `POST` body can also be an array of events, each with its own environment. See
[Posting events](/docs/api/posting-events/) and the [event format](/docs/reference/event-format/).

> [!IMPORTANT]
> The current .NET (6.2.0) and JavaScript (3.2.1) clients don't set an environment yet, so their events show as
> **Unspecified**. Setting a custom data property named `environment`, for example with `SetProperty` or `setProperty`,
> adds custom data and doesn't set the event's environment.

> [!NOTE]
> On a self-hosted instance, use your own API URL instead of `https://api.exceptionless.io`.

### Naming rules

- Leading and trailing spaces are trimmed, and the name keeps its casing: `" Production "` is stored as `Production`.
- Names can be up to 64 characters. Custom names such as `qa-west` are fine.
- Filters ignore case, so `Production` and `production` are the same environment.
- A value that isn't a valid name, such as an empty string, a number, an object, or a name over 64 characters, leaves
  the event unspecified. The value is kept in the event's custom data under `environment` instead, or `environment1` if
  that key is already taken.

### Environment isn't machine information

The **Machine & runtime** tab of an event shows details the client collects about the machine and process, such as the
machine name, OS, and runtime version. Those aren't the deployment environment and don't set it.

## Filter by environment

On **Stacks**, **Events**, or **Sessions**, select **Manage filters** > **Environment** and pick one or more names.
**Unspecified** matches events without an environment. To filter by a name that hasn't sent events in the time range,
type it and select **Use** _name_.

Save the filter in a [saved view](/docs/saved-views/), such as "Production errors", so you don't set it each time. In a
raw filter or the API, use these queries:

| Query                                             | Finds                         |
| ------------------------------------------------- | ----------------------------- |
| `environment:production`                          | Production events             |
| `(environment:production OR environment:staging)` | Either environment            |
| `_missing_:environment`                           | Events without an environment |
| `_exists_:environment`                            | Events with an environment    |
| `environment:"qa west"`                           | A name with a space in it     |

Environment filters work on every plan. See the [search syntax](/docs/reference/search-syntax/).

## Where environments appear

- **Event details**: the **Overview** tab shows **Environment** when the event has one. Select the filter icon next to
  it to filter the list by that environment.
- **Columns**: the **Events** and **Sessions** lists have an **Environment** column, hidden by default. Add it with
  **View** > **Manage Columns...**. Events without an environment show **Unspecified**.
- **Stack lists**: there's no environment column, because a stack can span environments. With an environment filter, the
  event and user counts in the list include only that environment's events.

## Stacks and statuses across environments

Environments don't split stacks, so a stack's status is shared. Marking a stack fixed while you look at production also
marks it fixed in staging.

When environments run different releases, mark stacks fixed in a version. If a stack is fixed in `2.4.0` and staging
already runs it, events from production on `2.3.0` don't reopen the stack. Continue sending your app's version with
every event so this works. See [Releases and regressions](/docs/versioning/).

Some features don't look at environments yet:

- A stack is new once, when its first event arrives from any environment. An error first seen in development triggers
  the new error notification then, and isn't new again when it reaches production. **New Stacks** counts work the same
  way.
- Critical error and regression notifications are sent for events from every environment.

## Sessions

When Exceptionless builds sessions from a user's events, it keeps each environment's sessions apart. Session heartbeats
don't carry an environment, so a heartbeat keeps the user's open sessions active in every environment. See
[Sessions](/docs/user-sessions/).

## Self-hosting

The 9.0 upgrade adds the environment field to existing event indexes without reindexing. Let the migration job finish
before the new API and job instances process events. Events stored in an existing index before the migration ran keep
their environment, but you can't filter or count them by it. See
[Upgrading](/docs/self-hosting/upgrading-self-hosted-instance/).

## Ask Exie

```prompt exie
Which environments had the most errors in the last 7 days?
```

```prompt exie
What are the top errors in production since yesterday?
```

```prompt exie
Is the checkout timeout happening in staging as well as production?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can compare environments while it works:

```prompt agent
Using Exceptionless, list the errors that occur in staging but not in production over the last 7 days, and check
whether any of them come from code changed on this branch.
```
