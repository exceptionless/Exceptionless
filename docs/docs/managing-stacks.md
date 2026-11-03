---
title: "Stacks"
metaTitle: "Managing stacks in Exceptionless"
description: "Triage Exceptionless stacks: open, fix, snooze, ignore, or discard them, mark them critical, add reference links, promote them, and act in bulk."
redirectFrom:
  - from: /docs/bulk-actions/
    anchor: bulk-actions
sources:
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-card.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-details.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-status-dropdown-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-options-dropdown-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stacks-bulk-actions-dropdown-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/mark-stack-fixed-in-version-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/mark-stack-discarded-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/add-stack-reference-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/requires-promoted-web-hook-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/dialogs/stacking-information-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/components/table/events-bulk-actions-dropdown-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/stacks/+page.svelte
  - src/Exceptionless.Web/Api/Handlers/StackHandler.cs
  - src/Exceptionless.Core/Models/Stack.cs
  - src/Exceptionless.Core/Extensions/StackExtensions.cs
  - src/Exceptionless.Core/Pipeline/010_AssignToStackAction.cs
  - src/Exceptionless.Core/Pipeline/020_MarkAsCriticalAction.cs
  - src/Exceptionless.Core/Pipeline/030_CheckForRegressionAction.cs
  - src/Exceptionless.Core/Pipeline/070_QueueNotificationAction.cs
  - src/Exceptionless.Core/Jobs/StackStatusJob.cs
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
---

# Stacks

A stack groups every event that describes the same problem, so you triage one stack instead of a thousand occurrences.
This page covers how to review a stack and change its status, and the other actions you can take on one or many stacks.
To learn how events are grouped, see [Stacking](/docs/concepts/stacking/).

## Review a stack

Open **Stacks** in the sidebar and pick a view: **All**, **Most Frequent Errors**, **Most Frequent 404s**, or **Most
Used Features**. Each lists stacks that are open or regressed and had events in the last 7 days.

![Most Frequent Errors view of the Stacks list](/assets/img/docs/managing-stacks-list-light.webp)

Select a stack to open it in a side panel. **Open in new window** in the panel header opens the full stack page, and the
copy icon copies a link to it. The stack card at the top shows:

- **Total Events**: how many times the stack has occurred. Select the calendar icon (**Open events for this stack**) to
  list its events.
- **Users Affected**: the share of the project's users in the last 7 days who hit this stack. Hover over the info icon
  for the counts.
- **First** and **Last**: the stack's first and last occurrence in the last 7 days.
- A chart of the stack's events over the last 7 days.
- The **Project**, **Tags**, when it was **Fixed** or how long it's **Snoozed Until**, and any **Reference** links.

![Stack details showing total events, users affected, and first and last occurrence](/assets/img/docs/managing-stacks-details-light.webp)

Below the card is the stack's most recent event. Use **Older event** and **Newer event** to step through its
occurrences. See [Events](/docs/events/) for the event tabs.

## Stack statuses

The status button in the stack card shows the current status. Select it to change it.

![Stack status menu with Open, Fixed, Snoozed, Ignored, and Discarded](/assets/img/docs/managing-stacks-status-menu-light.webp)

| Status        | New events                    | Notifications | In the built-in stack views |
| ------------- | ----------------------------- | ------------- | --------------------------- |
| **Open**      | Stored                        | Yes           | Yes                         |
| **Fixed**     | Stored; may regress the stack | No            | No                          |
| **Regressed** | Stored                        | Yes           | Yes                         |
| **Snoozed**   | Stored                        | No            | No                          |
| **Ignored**   | Stored                        | No            | No                          |
| **Discarded** | Dropped                       | No            | No                          |

Notifications here means the emails, Slack messages, and webhooks sent when an event is new, critical, or a regression.
See [Notifications](/docs/notifications/).

**Regressed** isn't in the menu. Exceptionless sets it when a fixed stack happens again.

### Open

New stacks start open. Choose **Open** to reopen a stack that's fixed, snoozed, ignored, or discarded. Reopening clears
its fixed version and snooze time.

### Mark a stack fixed

Choose **Fixed** when you've shipped a fix. In the **Mark Stack As Fixed** dialog, optionally enter the **Version** that
contains the fix, then select **Mark Stack Fixed**.

- **With a version**, only events from that version or later reopen the stack as regressed. Events from older versions
  that are still running don't, and on paid plans they're dropped instead of stored.
- **Without a version**, any event that occurs after you marked it fixed reopens it.

See [Releases and regressions](/docs/versioning/) for how versions are compared.

### Snooze a stack

Hover over **Snoozed** and pick **6 Hours**, **1 Day**, **1 Week**, or **1 Month**. Selecting **Snoozed** itself snoozes
for a month. A snoozed stack leaves the built-in views and sends no notifications. When the time is up, it goes back to
**Open**. The stack card shows when, under **Snoozed Until**.

### Ignore a stack

Choose **Ignored** for a stack you've decided not to fix but still want to record, such as noise from a third-party
script. Events keep arriving and counting against your plan, but the stack sends no notifications and leaves the
built-in views.

### Discard a stack

Choose **Discarded** when you don't want the stack's events at all. After you confirm in the **Discard Stack** dialog,
new events for the stack are dropped when they arrive and don't count against your plan's event limit. Events already
stored stay until you delete the stack or they age out.

### Find fixed, ignored, and discarded stacks

The built-in views show only open and regressed stacks. To see the others:

- On a stack list, select **Manage filters** > **Status** and pick the statuses you want. This opens the view's hidden
  **Status** filter if it has one. See [Filtering and searching](/docs/filtering-and-searching/).
- Open the project's settings (**Settings** > **Projects**, then the project) and select **Stack Management**. It lists
  the project's ignored and discarded stacks, so you can restore them.

## Mark future occurrences critical

Open **Stack options** (the gear next to the status button) and check **Future Occurrences Are Critical**. Every new
event in the stack gets the `Critical` tag, which you can filter on and be [notified](/docs/notifications/) about.
Uncheck it to stop. Existing events don't change.

![Stack Options menu with Future Occurrences Are Critical, Promote To External, Add Reference Link, View Stacking Information, and Delete](/assets/img/docs/managing-stacks-options-menu-light.webp)

## Add reference links

Link a stack to an issue, pull request, incident, or chat thread:

1. Open **Stack options** and select **Add Reference Link**.
2. Enter a URL and select **Save Reference Link**.

The link appears in the stack card under **Reference**. Select the trash icon next to it to remove it.

## Promote a stack to an external system

**Promote To External** sends the stack to every enabled webhook in the project that subscribes to promoted stacks, for
example to create an issue in your tracker. Set up the webhook first in the project's **Integrations**; if there isn't
one, Exceptionless offers to take you there. See [Integrations](/docs/integrations/).

Promoting a stack needs a paid plan.

## View stacking information

**View Stacking Information** shows the values Exceptionless used to group events into the stack, such as the exception
type and the method that threw. Use it to understand why two events did or didn't land in the same stack. See
[Stacking](/docs/concepts/stacking/).

## Delete a stack

**Delete** in **Stack options** removes the stack and all of its events after you confirm. Deleting runs in the
background. If the same error happens again, it starts a new stack.

To stop collecting a stack's events, discard it instead.

## Bulk actions {#bulk-actions}

To act on many stacks at once, select the check box on each row, or the check box in the header to select every row on
the page. Then open **Actions** above the list.

![Stacks list with three stacks selected and the Actions menu open](/assets/img/docs/bulk-actions-menu-light.webp)

In a stack list, **Actions** has **Mark Open**, **Mark Fixed**, **Mark Snoozed**, **Mark Ignored**, **Mark Discarded**,
and **Delete**. They work like the single-stack actions above. **Mark Fixed** asks for one version for all the selected
stacks.

In an event list, **Actions** has **Delete**, which deletes the selected events.

**Actions** stays disabled until you select at least one row.

## Ask Exie

Exie can find stacks and change them when you ask for that exact change:

```prompt exie
Which stacks regressed in the last 7 days?
```

```prompt exie
Mark the NullReferenceException stack in the Checkout project fixed in version 2.4.1.
```

```prompt exie
Snooze the "Connection reset by peer" stack for a day.
```

```prompt exie
Add https://github.com/acme/api/issues/42 as a reference link on the top error stack this week.
```

Exie can't promote or delete stacks. With the [MCP server](/docs/ai/mcp-server/) connected and the `stacks:write`
permission granted, your coding agent can change stacks too:

```prompt agent
Find the most frequent open error in my Exceptionless project this week and fix it in this repository. Then mark its
stack fixed in the version this fix will ship in, and add a link to the commit as a reference link on the stack.
```
