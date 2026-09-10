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

Environment selections persist in URLs and saved views. Events, sessions, and stream tables have an optional Environment column. Event details show Environment in the overview; **Machine & runtime** contains the existing `data.@environment` diagnostics.

Environment searches and aggregations are available on every plan:

| Search | Meaning |
| --- | --- |
| `environment:production` | Production events |
| `(environment:production OR environment:staging)` | Either deployment |
| `_missing_:environment` | Historical or unspecified events |
| `_exists_:environment` | Events with an environment |
| `environment:"qa west"` | A custom name containing spaces |

Stack dashboard counts and charts use events matching the filter. Stack status remains shared, so changing status while viewing production also changes the same stack seen in staging. Automatic session detection separates the same user's activity by environment.

## Fixing stacks across deployments

Use [fixed in version](/docs/versioning/) when environments run different releases. For example, if a stack is fixed in `2.4.0`, occurrences from production running `2.3.0` do not represent a regression just because staging already has the fix. Continue sending the application version with events. Plain **Fixed**, without a version, retains its existing behavior across all environments.

## Rollout

Deploy the server before adopting SDK versions that expose the new setting. The server adds mappings to existing event indices without rewriting historical events. Clients that do not send an environment continue to work. `data.@environment` and custom `data.environment` values retain their existing meanings.
