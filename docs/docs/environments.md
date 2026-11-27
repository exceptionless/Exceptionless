---
title: "Environments"
description: "Tag Exceptionless events with a deployment environment such as production or staging, then filter and compare every list by environment."
keywords:
  - "production"
  - "staging"
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
> The .NET 6.2.0 and JavaScript 3.2.1 clients don't set an environment, so their events show as **Unspecified**. Setting
> a custom data property named `environment`, for example with `SetProperty` or `setProperty`, adds custom data and
> doesn't set the event's environment.

> [!NOTE]
> On a self-hosted instance, use your own API URL instead of `https://api.exceptionless.io`.

### Naming rules

- Leading and trailing spaces are trimmed, and the name keeps its casing: `" Production "` is stored as `Production`.
- A name is 1 to 64 ASCII letters, digits, periods (`.`), underscores (`_`), or hyphens (`-`), and starts with a letter
  or digit. Custom names such as `qa-west`, `staging.eu`, or `dev_local` are fine.
- Filters ignore case, so `Production` and `production` are the same environment.
- A value that isn't a valid name, such as `Staging West`, `prod/eu`, an empty string, a number, an object, or a name over
  64 characters, leaves the event unspecified. The value is kept in the event's custom data under `environment` instead, or `environment1` if
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

**New Stacks** counts don't look at environments: a stack is dated from its first event in any environment.
Notifications do; see [Notifications by environment](#notifications-by-environment).

## Manage a project's environments

Open **Project Settings** > **Environments** to list the environments a project uses. A listed name is shown in the
casing you typed, such as `Production`, in the Environment filter, event details, and notification emails. Matching still
ignores case. A project can list up to 100 environments.

Adding or removing a name never changes or deletes events, and Exceptionless never adds names to the list on its own.
Environments that sent events in the last 30 days show up in the table with an **Add** action.

### Discard events from unlisted environments

Projects accept events from any environment by default. To accept only listed ones, set **Accept events from** to
**Only environments in this list**:

- Events from an unlisted environment are discarded before they're stored or stacked. They count as discarded events and
  don't count toward your plan limit, like events for a discarded stack.
- **Also accept events that don't send an environment** is on by default. Turn it off to discard those events too.
  Session heartbeats and session ends never carry an environment and are never discarded for this reason.
- Clients are told the events were accepted, so nothing looks wrong on their side.

The **Discarded** section lists the names rejected in the last 7 days, up to 50 per project, with an event count and an
**Allow** action that adds the name to the list. It's the quickest way to spot a misspelling such as `prodution`.

### Notifications by environment

Each listed environment has its own **Notifications** toggle. The **Other environments** row covers unlisted environments
and events without one. An event sends notifications (email, Slack, and web hooks) only from an environment that
notifies.

- **New stacks**: you're notified the first time a stack occurs in a notifying environment. An error first seen in a
  muted `dev` sends nothing; its first occurrence in `Production` sends the new error notification, once.
- **Regressions**: work the same way. A regression in a notifying environment notifies right away. Changing the stack's
  status drops a notification that's still waiting.
- **Critical events**: notify only from a notifying environment.
- Stacks created before environments could be managed are treated as already reported.
- Notification emails and Slack messages name the environment, for example "New error in Production". Daily summaries
  still include every environment.

## Sessions

When Exceptionless builds sessions from a user's events, it keeps each environment's sessions apart. Session heartbeats
don't carry an environment, so a heartbeat keeps the user's open sessions active in every environment. See
[Sessions](/docs/user-sessions/).

## Self-hosting

The 9.0 upgrade adds the environment field to existing event indexes without reindexing. Before you accept or process
events with the upgraded deployment, let that migration finish, using the
[ordered upgrade procedure](/docs/self-hosting/upgrading-self-hosted-instance/#upgrade). Events stored in an existing
index before the migration ran keep their environment, but you can't filter or count them by it.

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
