---
title: "Releases and regressions"
metaTitle: "Versions and regression detection"
description: "Send your app's version with every event, mark stacks fixed in a release, and learn when Exceptionless reopens a fixed stack as regressed."
sources:
  - src/Exceptionless.Core/Pipeline/030_CheckForRegressionAction.cs
  - src/Exceptionless.Core/Utility/SemanticVersionParser.cs
  - src/Exceptionless.Core/Extensions/StackExtensions.cs
  - src/Exceptionless.Web/Api/Handlers/StackHandler.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/mark-stack-fixed-in-version-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/schemas.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-card.svelte
  - src/Exceptionless.Core/Repositories/Configuration/Indexes/EventIndex.cs
  - tests/Exceptionless.Tests/Pipeline/EventPipelineTests.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/ExceptionlessConfigurationExtensions.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/080_VersionPlugin.cs
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
---

# Releases and regressions

Send your app's version with every event, and Exceptionless can tell which release an error came from. When you mark a
stack fixed in a version, only events from that version or later reopen it, so older releases that are still running
don't undo your fix.

## Send your app's version

Set the version once when the client starts. Every event it sends then includes it.

```csharp tab=".NET"
// Program.cs (Exceptionless.AspNetCore)
builder.AddExceptionless(c => c.SetVersion("2.4.1"));
```

```ts tab="JavaScript"
import { Exceptionless } from "@exceptionless/browser"

await Exceptionless.startup((c) => {
  c.apiKey = "YOUR_API_KEY"
  c.version = "2.4.1"
})
```

If you don't set it, the .NET client uses the version of your app's entry assembly when it can find one. The JavaScript
client sends no version unless you set it. For other clients, put the version in the event's `@version` data. See the
configuration page for [.NET](/docs/clients/dotnet/configuration/) or
[JavaScript](/docs/clients/javascript/client-configuration/), and the [event format](/docs/reference/event-format/).

The version appears on the event's **Overview** tab, with a filter icon to filter by it. Add the **Version** column to
an event list with **View** > **Manage Columns...**.

## Mark a stack fixed in a version

1. Open the stack and select its status button, which shows the current status, such as **Open**.
2. Choose **Fixed**.
3. In **Mark Stack As Fixed**, enter the **Version** that contains the fix, such as `2.4.1`, or leave it empty.
4. Select **Mark Stack Fixed**.

![Mark Stack As Fixed dialog with an optional version](/assets/img/docs/versioning-mark-fixed-light.webp)

To fix several stacks at once, select them in a list and choose **Actions** > **Mark Fixed**. See
[Bulk actions](/docs/managing-stacks/#bulk-actions).

The version must be a semantic version, such as `1.2.3` or `1.2.3-beta.1`. The dialog rewrites two common formats for
you: a two-part version such as `1.2` becomes `1.2.0`, and a four-part version such as `1.2.3.4` becomes `1.2.3-4`.

The stack card then shows **Fixed** with the version and date, and the stack leaves the built-in views, which show only
open and regressed stacks.

## When a fixed stack regresses

When a new event arrives for a fixed stack, Exceptionless compares it with the fix:

| Fixed in   | New event's version               | Result                                         |
| ---------- | --------------------------------- | ---------------------------------------------- |
| No version | Any, or none                      | Regressed, if the event occurred after the fix |
| `2.4.1`    | `2.4.1` or later, such as `2.5.0` | Regressed                                      |
| `2.4.1`    | Earlier, such as `2.4.0`          | Stays fixed                                    |
| `2.4.1`    | None                              | Stays fixed                                    |

A regressed stack shows **Regressed**, appears in the built-in views again, and can send a regression notification. See
[Notifications](/docs/notifications/).

When a stack fixed in a version stays fixed, what happens to the event depends on your plan. On paid plans, the event is
dropped and not stored. On the Free plan, it's stored with the stack, which stays fixed.

Versions are compared as semantic versions, so `2.10.0` is later than `2.9.0`, and a prerelease such as `2.4.1-beta` is
earlier than `2.4.1`. A leading `v` in an event's version, as in `v2.4.1`, is ignored.

## Fixes across environments

A stack and its fixed version are shared across [environments](/docs/environments/). If staging already runs `2.4.1` and
production still runs `2.4.0`, mark the stack fixed in `2.4.1`. Production's errors from `2.4.0` then don't reopen it.
If you mark it fixed without a version, the next production error reopens it.

## Search by version

- `version:2.4` finds events from any `2.4.x` version, and `version:2.4.1` from exactly that one.
- `version_fixed:2.4.1` finds events in stacks marked fixed in `2.4.1`.
- Version ranges, such as `version:>2.4`, aren't supported.

See the [search syntax](/docs/reference/search-syntax/#versions).

## Ask Exie

```prompt exie
Which versions of my app had the most errors in the last 7 days?
```

```prompt exie
Which stacks regressed since we released 2.4.1?
```

```prompt exie
Mark this stack fixed in version 2.4.2.
```

With the [MCP server](/docs/ai/mcp-server/) connected and `stacks:write` granted, your coding agent can close the loop
after a fix:

```prompt agent
Read the version from this project's build file, then mark the Exceptionless stacks for the errors fixed on this branch
as fixed in that version.
```
