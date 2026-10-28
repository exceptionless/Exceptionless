---
title: "Notifications"
metaTitle: "Exceptionless email and Slack notifications"
description: "Get an email or Slack message when a new error, critical error, or regression happens, and a daily summary of each project's errors."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/notifications/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/user-notification-settings-form.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/integration-notification-settings-form.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/integrations/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/manage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-options-dropdown-menu.svelte
  - src/Exceptionless.Core/Pipeline/070_QueueNotificationAction.cs
  - src/Exceptionless.Core/Jobs/EventNotificationsJob.cs
  - src/Exceptionless.Core/Jobs/DailySummaryJob.cs
  - src/Exceptionless.Core/Jobs/WebHooksJob.cs
  - src/Exceptionless.Core/Models/Stack.cs
  - src/Exceptionless.Core/Models/User.cs
  - src/Exceptionless.Core/Extensions/ProjectExtensions.cs
  - src/Exceptionless.Core/Services/SlackService.cs
  - src/Exceptionless.Web/Api/Handlers/ProjectHandler.cs
  - Exceptionless.Net:src/Exceptionless/EventBuilder.cs
  - Exceptionless.JavaScript:packages/core/src/EventBuilder.ts
---

# Notifications

Exceptionless can email you, or post to a Slack channel, when something happens in a project that needs attention: a new
error, a critical error, or a regression. It can also email you a summary of each project's errors once a day.

Notification settings are per user and per project. Each person in the organization chooses what they get.

## Which events notify

| Notification              | Sent when                                                                                  | Plan       |
| ------------------------- | ------------------------------------------------------------------------------------------ | ---------- |
| **New Errors**            | An error happens that starts a new stack.                                                  | Paid plans |
| **Critical Errors**       | An error marked critical happens.                                                          | Paid plans |
| **Error Regressions**     | An event happens in a stack marked fixed, so the stack [regresses](/docs/versioning/).     | Paid plans |
| **New Events**            | An event of any type starts a new stack, such as a new log message, 404, or feature usage. | Paid plans |
| **Critical Events**       | An event of any type marked critical happens.                                              | Paid plans |
| **Daily Project Summary** | Once a day, by email only.                                                                 | All plans  |

An event is critical when its stack has **Future Occurrences Are Critical** turned on (see
[Stacks](/docs/managing-stacks/)), or when your code marks it with `MarkAsCritical()` in .NET or `markAsCritical()` in
JavaScript.

To keep notifications useful when something breaks badly, Exceptionless limits them:

- After a stack's first two occurrences, that stack sends at most one notification every 15 minutes.
- A project sends notifications for at most 10 events every 30 minutes.
- Regressions are always sent.
- Events from stacks that are fixed, snoozed, ignored, or discarded don't notify.
- Events from bots don't notify. Bots are user agents that look like crawlers or match the project's
  [spam detection](/docs/project-settings/#spam-detection) list.

## Turn on email notifications

1. Open the user menu at the bottom of the sidebar (or press <kbd>U</kbd>) and click **Notifications**.
2. Make sure **Enable Email Notifications** is on. This switch turns all of your notification emails on or off.
3. Under **Project Notifications**, choose the project.
4. Turn on **Daily Project Summary** and the **Event Notifications** you want. Changes save right away.

![Project Notifications settings with the daily summary and event notification switches](/assets/img/docs/notifications-project-settings-light.webp)

You can also click **Notifications** on a project's **General** settings tab to open this page with that project
selected.

Some things to know:

- You must verify your email address first. Until you do, the page shows **Email notifications are currently disabled**
  with a link to resend the verification email.
- When you create a project, your **Daily Project Summary**, **New Errors**, **Critical Errors**, and **Error
  Regressions** notifications start on. Other members of the organization start with nothing turned on for it.
- On the Free plan, the **Event Notifications** switches are turned off, with an **Upgrade now** link.

## Daily project summary

The daily summary is an email for one project that covers the previous day, in UTC:

- the number of errors, how many unique stacks they came from, and how many were new,
- the most frequent error stacks and the newest ones,
- errors in stacks you've marked fixed,
- events that were blocked by a plan limit or rejected for being too big.

It's sent once a day, after the day it covers ends, to everyone who turned on **Daily Project Summary** for the project.
Projects with no errors that day still get a summary.

## Send notifications to Slack

Slack notifications post to one Slack channel per project:

1. Open the project's settings and click the **Integrations** tab.
2. Click **Connect Slack**, sign in to your Slack workspace, and choose the channel.
3. Back on the **Integrations** tab, use the switches to choose which notifications go to Slack: **New Errors**,
   **Critical Errors**, **Error Regressions**, **New Events**, and **Critical Events**.

New connections start with **New Errors**, **Critical Errors**, and **Error Regressions** on. Slack notifications follow
the same rules and limits as email, need a paid plan, and there's no daily summary in Slack. To stop them, click
**Remove Slack**.

If Slack keeps rejecting messages, for example because the Exceptionless app was removed from the workspace,
Exceptionless disconnects Slack from the project. Connect it again to resume.

For webhooks and Zapier, see [Integrations](/docs/integrations/).

## Self-hosting

A self-hosted instance needs email settings to send notifications and daily summaries, and a Slack app to show **Connect
Slack**. See [Self-hosting configuration](/docs/self-hosting/configuration/).

## Ask Exie

[Exie](/docs/ai/exie/) is on Medium plans and up.

```prompt exie
Which new errors appeared in this project in the last 24 hours?
```

```prompt exie
Mark this stack critical so its future occurrences send critical notifications.
```

```prompt exie
Which stacks regressed this week?
```
