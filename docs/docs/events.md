---
title: "Events"
metaTitle: "Viewing events in Exceptionless"
description: "Browse Exceptionless events, read every tab of the event details, watch lists update live, and find an event by its reference ID."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/event/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/stream/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/event/by-ref/[referenceId]/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/api.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/events-overview.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/events-stats-dashboard.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/extended-data-item.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/overview.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/error.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/environment.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/request.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/views/session-events.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/table/options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/persistent-event.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/detail-sheet.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/streaming-indicator-button.svelte
  - src/Exceptionless.Core/Repositories/EventRepository.cs
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
---

# Events

An event is one thing that happened in your app: an error, a log message, a feature being used, a 404, or a session.
This page shows how to browse events, what each part of an event's details means, and how to find a specific event.

## Browse events

Open **Events** in the sidebar and pick a view:

- **All**: every event in the last 7 days.
- **Errors**: error events from open and regressed stacks.
- **Logs**: log messages from open and regressed stacks.

The view starts with stat boxes and a chart for the time range:

- **Events**: total occurrences that match the filters.
- **Stacks**: how many stacks those events belong to.
- **New Stacks**: stacks whose first occurrence is in the time range.
- **Events/hr**: average occurrences per hour.

Drag across the chart to zoom into a time range. Turn the stat boxes and chart off under **View** > **Display**.

The list shows **Summary**, **User**, and **Date** by default. Add **Project**, **Tags**, **Message**, **Type**,
**Environment**, **Version**, **Exception Type**, **Source**, **Name**, or **Level** with **View** > **Manage
Columns...**. See [Saved views](/docs/saved-views/) to keep your columns, and
[Filtering and searching](/docs/filtering-and-searching/) to narrow the list.

The **Stacks** lists work the same way, with one row per stack instead of one per event. See
[Stacks](/docs/managing-stacks/).

## Live updates

Lists update on their own. When new events arrive or a stack changes, the open list, stat boxes, and chart refresh
within a few seconds, without reloading the page. Select **Refresh results** to refresh right away.

To watch events arrive as they happen, open the **Event Stream** page at `/stream` in the app, for example
`https://be.exceptionless.io/stream`. It loads new events as they arrive. Select **Pause streaming updates** to stop the
list from changing while you read it, and **Resume streaming updates** to continue.

## Open an event

Select an event to open its details in a side panel. The panel header has a copy icon (**Copy event link**) and **Open
in new window**, which opens the event's own page.

The panel has two parts.

### Stack

The stack card at the top shows the stack the event belongs to: its status, totals, users affected, first and last
occurrence, and a 7-day chart. You can change the stack's status and options right there. See
[Stacks](/docs/managing-stacks/).

When Exie is available, **Fix** on the card asks [Exie](/docs/ai/exie/) for the likely root cause and next steps.

### Event

Above the tabs, **Occurred On** shows when the event happened. The buttons next to the **Event** heading are:

- **View Event JSON**: the raw event, with **Copy to Clipboard**.
- **Show all events**: filters the list behind the panel to this stack's events.
- **Older event** and **Newer event**: step through the other occurrences in the same stack.

## Event tabs

An event shows only the tabs it has data for.

| Tab                   | What it shows                                                                                                                                           |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Overview**          | The key facts: environment, reference ID, level, type, error type, source, value, message, version, location, tags, URL, user, and the stack trace.     |
| **Exception**         | Error type, message, code, and submission method; the full stack trace, including inner exceptions; additional error data; and loaded modules.          |
| **Machine & runtime** | Machine name, IP address, OS name and version, architecture, processor count, memory, process name and ID, command line, and runtime version.           |
| **Request**           | HTTP method, URL, referrer, client IP address, user agent, device, browser, browser OS, post data, headers, cookie values, and additional request data. |
| **Trace Log**         | The log messages the client recorded before the event.                                                                                                  |
| **Session Events**    | The other events in the same user session, with the session's duration. Sessions need a paid plan.                                                      |
| **Extended Data**     | Your custom data, one section per key.                                                                                                                  |

On the **Overview** tab:

- Select the filter icon next to a value, such as the type, source, version, environment, or user, to filter the list by
  it. Select a tag to filter by that tag.
- Select the reference ID to open the events that share it.
- **User Info** shows the user's email, identity, name, and any description they submitted.
- For errors, the stack trace has **Copy Stack Trace to Clipboard**. For JavaScript errors, it can also show whether a
  [source map](/docs/clients/javascript/source-maps/) was applied.

### Custom data tabs

Each key in the **Extended Data** tab has an options menu with:

- **Toggle View**: switch between a structured view and the raw value.
- **Copy to Clipboard**.
- **Promote to Tab**: show this key as its own tab on every event in the project that has it.

On a promoted tab, the options menu has **Demote Tab**, which moves the key back to **Extended Data**. Drag promoted
tabs to reorder them. Promoted tabs are saved for the project, so everyone sees the same tabs.

To send custom data, see the sending events page for [.NET](/docs/clients/dotnet/sending-events/) or
[JavaScript](/docs/clients/javascript/sending-events/).

## Find an event by reference ID

A reference ID is an ID your app assigns to an event, so a user or support agent can quote it later. See
[Reference IDs](/docs/references-ids/) to set one up. To find the event:

- Go to `https://be.exceptionless.io/event/by-ref/YOUR_REFERENCE_ID`. If one event has that reference ID, it opens. If
  several do, the **Event Reference** page lists them, with **View In Events** to see them in a list.
- Or add a **Reference** filter to any event list and enter the ID. In a raw filter, use `reference:YOUR_REFERENCE_ID`.

> [!NOTE]
> On a self-hosted instance, use your own app URL instead of `https://be.exceptionless.io`.

## Delete events

Select events in a list, then open **Actions** and select **Delete**. To delete every event in a stack, delete the stack
instead. See [Bulk actions](/docs/managing-stacks/#bulk-actions).

## Ask Exie

Open an event and ask about it. Exie uses the event you're viewing as context:

```prompt exie
Explain this error and suggest a fix.
```

```prompt exie
Which errors did jane@example.com run into in the last 7 days?
```

```prompt exie
How many events came in per hour today, and which stack caused the spike?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can read an event's stack trace and request
details and work on the fix in your code:

```prompt agent
Get Exceptionless event 54dbc16ca0f5c61398427b00, find the code in this repository that threw it, and propose a fix.
```
