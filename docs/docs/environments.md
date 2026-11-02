---
title: "Environments"
---

# Environments

Use an environment to distinguish production, staging, development, or a custom deployment within one project. Each event can carry one optional top-level `environment` string. The same error still belongs to one stack, with one shared status and fixed version.

## Configure your client

Set a default during application startup. In the .NET client:

```csharp
client.Configuration.SetEnvironment("production");
client.CreateLog("Deployment complete").SetEnvironment("staging").Submit();
```

You can also set `Exceptionless:Environment` in .NET configuration, or the `Exceptionless__Environment` environment variable. The hosting integration uses `IHostEnvironment.EnvironmentName` when no explicit environment is configured. A per-event value overrides the default.

In the JavaScript client:

```javascript
await Exceptionless.startup((config) => {
  config.apiKey = "API_KEY_HERE";
  config.environment = "production";
});

await Exceptionless.createLog("Deployment complete")
  .setEnvironment("staging")
  .submit();
```

For direct API submissions:

```json
{
  "type": "log",
  "message": "Deployment complete",
  "environment": "production"
}
```

Names are trimmed, with a maximum of 64 characters. Events preserve the supplied casing: `" Production "` is stored and returned as `"Production"`. Filtering is case-insensitive, and aggregation keys are normalized to lowercase, so `Production` and `production` share one environment bucket. Custom names such as `qa-west` are supported. Empty, oversized, control-character, and non-string values are treated as unspecified. Historical events and older clients without this field remain unspecified; they are never assumed to be production.

## Filter your data

Choose **Manage filters → Environment** on Events, Stacks, Sessions, or Stream. Select one or more names, or **Unspecified** for events without an environment. Clear the selection to include all environments. The picker discovers names for the selected projects and time range; you can also enter a name that has no current events.

Environment selections persist in URLs and saved views. Events, sessions, and stream tables have an optional Environment column. Event details show Environment in the overview when the event has one; **Machine & runtime** contains the existing `data.@environment` diagnostics.

Environment searches and aggregations are available on every plan:

| Search | Meaning |
| --- | --- |
| `environment:production` | Production events |
| `(environment:production OR environment:staging)` | Either deployment |
| `_missing_:environment` | Historical or unspecified events |
| `_exists_:environment` | Events with an environment |
| `environment:"qa west"` | A custom name containing spaces |

Stack dashboard counts and charts use events matching the filter. Stack status remains shared, so changing status while viewing production also changes the same stack seen in staging. Automatic session detection separates the same user's activity by environment. Session heartbeats do not include an environment, so a heartbeat keeps the user's open sessions active in every environment.

The **New** view is not environment-aware. A stack is dated from its first event in any environment. For notifications, see [Manage environments](#manage-environments) below.

## Manage environments

Open **Project Settings → Environments** to list the environments a project uses. Listed names are shown in the casing you type (for example `Production`) in the Environment filter, event details, and notification emails, while matching stays case-insensitive. Adding or removing a name never changes or deletes events, and the server never adds names to the list on its own: environments that appear in your data show up in the table with an **Add** action. A project can list up to 100 environments, and a name follows the same rules as an event's environment: 1 to 64 characters after trimming, with no control characters.

Three project settings control what happens to events from environments that are not listed. Projects with no list and default settings behave exactly as before.

| Setting | Default | Meaning |
| --- | --- | --- |
| Discard events from environments that are not listed | Off | Events whose environment is not in the list are discarded before they are stored or stacked. They count as discarded events in your usage and do not count toward your plan limit, like events for a discarded stack. |
| Also discard events that have no environment | Off | Only applies when the setting above is on. Events without an environment are discarded too. Session heartbeats and session ends, which never carry an environment, are never discarded for this reason. |
| Send notifications for other environments | On | Applies to environments that are not listed and to events without an environment. Each listed environment has its own **Notifications** toggle. |

Ingestion acknowledges events before they are processed, so a client cannot see that an event was discarded. The **Discarded Events** table shows the names that were rejected in the last 7 days (up to 50 names per project), how many events each had, and an **Allow** action that adds the name to the list. This is the quickest way to find a misspelled environment such as `prodution`. Events without an environment are shown as their own row.

### Notifications by environment

An event can send notifications (email, Slack, and web hooks) only when its environment notifies: a listed environment with notifications on, or an unlisted environment or no environment while **Send notifications for other environments** is on.

- Critical error, critical event, and regression notifications are sent only for events from a notifying environment.
- A new error or new event notification is sent the first time a stack occurs in a notifying environment. If an error is first seen in `dev` and `dev` does not notify, nothing is sent; the first time the same error occurs in `Production`, you get the new error notification, and it is not reported as new again in other environments.
- Stacks that existed before environments could be managed are treated as already reported, so deploying this change does not send new notifications for old stacks.
- Notification emails and Slack messages mention the environment, for example "New error in Production".
- Daily summaries are unchanged and still include events from every environment.

## Fixing stacks across deployments

Use [fixed in version](/docs/versioning/) when environments run different releases. For example, if a stack is fixed in `2.4.0`, occurrences from production running `2.3.0` do not represent a regression just because staging already has the fix. Continue sending the application version with events. Plain **Fixed**, without a version, retains its existing behavior across all environments.

## Rollout

Deploy the server before adopting SDK versions that expose the new setting. The server adds mappings to existing event indices without rewriting historical events. Self-hosted installs should let the migration job finish before the new API and job instances process events: events written to an existing index before the mapping is added keep their environment but cannot be filtered or aggregated by it. Clients that do not send an environment continue to work. `data.@environment` and custom `data.environment` values retain their existing meanings.

Before this release, the server stored an unrecognized root `environment` property as custom data in `data.environment`. A valid root `environment` string is now stored only as the event's environment, trimmed, so read it from `environment` instead of `data.environment`. Any other root value, such as null, an object, a number, a blank string, or an oversized name, is still stored as `data.environment`. If `data.environment` already exists, that value uses the next available key (`environment1`, `environment2`, and so on).
