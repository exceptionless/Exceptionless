---
title: "Core concepts"
description: "How Exceptionless organizes your data: organizations, projects, events, stacks and their statuses, versions, environments, saved views, and AI."
sources:
  - src/Exceptionless.Core/Models/Event.cs
  - src/Exceptionless.Core/Models/Stack.cs
  - src/Exceptionless.Core/Models/SavedView.cs
  - src/Exceptionless.Core/Pipeline/010_AssignToStackAction.cs
  - src/Exceptionless.Core/Pipeline/030_CheckForRegressionAction.cs
  - src/Exceptionless.Core/Jobs/StackStatusJob.cs
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Core/Serialization/EventEnvironmentConverter.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-status-dropdown-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/saved-views/components/save-view-dialog.svelte
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - src/Exceptionless.Web/Assistant/AssistantSystemPrompt.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
---

# Core concepts

Exceptionless takes in events from your apps, groups the ones that describe the same problem into stacks, and lets you
triage stacks instead of individual occurrences. This page explains the pieces in about five minutes and links to the
pages that cover each one in depth.

## Organizations and projects

An **organization** is your team's account. It has the plan, the billing details, and the users who can see its data.
You invite teammates to an organization, and they see every project in it. One person can belong to several
organizations.

A **project** is one app or service that sends events, such as a web API or a mobile app. Each project has its own API
keys, settings, and data. Clients use a project API key to send events, so the key decides which project an event lands
in.

Plan limits, such as monthly events, retention, and the number of projects and users, apply to the whole organization.
See [Organizations and billing](/docs/organizations/), [Projects](/docs/project-settings/), and
[Limits and deduplication](/docs/concepts/limits/).

## Events

An **event** is one thing that happened in your app: an error, a log message, a feature being used. Each event has a
type, a date, and optional details such as a message, source, tags, user, version, environment, request, and custom
data.

| Type      | What it records                                                                          |
| --------- | ---------------------------------------------------------------------------------------- |
| `error`   | An exception, with its type, message, and stack trace.                                   |
| `log`     | A log message, with an optional source and level.                                        |
| `usage`   | Feature usage: someone used a feature you want to count.                                 |
| `404`     | A request for a page or resource that wasn't found.                                      |
| `session` | The start of a user session. `heartbeat` events keep it open and `sessionend` closes it. |

Clients send most of these automatically. Unhandled exceptions become errors as soon as the client is installed, and you
send logs, feature usage, and handled errors with one line of code. Any other type name is a custom event.

See [Events](/docs/events/), [Logs and log levels](/docs/setting-log-levels/), [Feature usage](/docs/feature-usage/),
[Sessions](/docs/user-sessions/), and the [event format](/docs/reference/event-format/).

## Stacks

A **stack** groups every event that describes the same problem. Exceptionless computes a signature for each event: for
errors, the exception type and the method in your code that threw it; for other events, the type and source. Events with
the same signature in the same project join the same stack. A thousand occurrences of one bug are one stack with a count
of 1,000, not a thousand rows to read.

You triage at the stack level. A stack's status, critical flag, reference links, and fixed version apply to all of its
events. See [Stacking](/docs/concepts/stacking/) for how signatures work and how to change them.

### Stack statuses

| Status        | Meaning                                                                                                     |
| ------------- | ----------------------------------------------------------------------------------------------------------- |
| **Open**      | Needs attention. New stacks start here.                                                                     |
| **Fixed**     | You fixed it, optionally in a specific version. If it happens again, Exceptionless marks it regressed.      |
| **Regressed** | It was fixed and has happened again. Regressed stacks show up next to open ones.                            |
| **Snoozed**   | Hidden and silent for 6 hours, a day, a week, or a month. When the time is up, the stack goes back to open. |
| **Ignored**   | Events are still recorded, but you get no notifications for the stack.                                      |
| **Discarded** | Future events are dropped and don't count against your plan's event limit.                                  |

The built-in **Stacks** views show only open and regressed stacks, so fixed, snoozed, ignored, and discarded stacks drop
out of your daily list. See [Stacks](/docs/managing-stacks/).

## Versions and regressions

When your client sends your app's version with each event, you can mark a stack **Fixed** in a version, such as `2.4.0`.
Exceptionless then treats an event from version `2.4.0` or later as a regression, and reopens the stack as
**Regressed**. Events from older versions that are still running don't reopen it.

If you mark a stack fixed without a version, any new event after that moment is a regression. See
[Releases and regressions](/docs/versioning/).

## Environments

An event can name the **environment** it came from, such as `production`, `staging`, or `development`. You can filter
every list by environment, and saved views remember the filter. Environments don't split stacks: the same error in
staging and production is one stack with one status. See [Environments](/docs/environments/).

## Saved views

The **Stacks**, **Events**, and **Sessions** pages each show a **saved view**: a filter, a time range, a sort order, and
the columns and charts to show. Every organization starts with built-in views:

- **Stacks**: All, Most Frequent Errors, Most Frequent 404s, and Most Used Features.
- **Events**: All, Errors, and Logs.
- **Sessions**: All.

Each built-in view covers the last 7 days. Change the filter or time range, then save it as a new view. A view is shared
with everyone in the organization unless you mark it **Private**. Saved views appear in the sidebar under the page they
belong to. See [Saved views](/docs/saved-views/) and [Filtering and searching](/docs/filtering-and-searching/).

## Where AI fits

Exceptionless works with AI in two ways, and Exie uses a subset of the MCP server's tools:

- **Exie** is the assistant in the app. Ask it which errors matter, what changed after a release, or why a stack keeps
  happening. It searches stacks, looks up their events, counts events over time, and reads stack traces. When you ask it
  to, it also changes a stack's status, snoozes it, marks it critical, or adds a reference link. Exie is instructed to
  change data only when you ask for that exact change. Exie is on Medium plans and up. See [Exie](/docs/ai/exie/).
- **The MCP server** connects your own coding agent, such as Claude Code or Codex, to Exceptionless on every plan. The
  agent signs in with your account, you choose what it can access, and it can look up an error and fix the code in the
  same session. Changing stacks needs a separate `stacks:write` permission. See [MCP server](/docs/ai/mcp-server/).

For what these tools can see and how to limit them, see [AI privacy and security](/docs/ai/privacy/).
