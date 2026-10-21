---
title: "Integrations"
metaTitle: "Exceptionless integrations: Slack, Zapier, webhooks"
description: "Connect Exceptionless to Slack, Zapier, and webhooks from project settings, pick which notifications to send, and see the webhook JSON payload."
---

# Integrations

To make managing events from your application easier, Exceptionless provides some useful integrations. To find them, go to **Settings** > **Projects** in the sidebar, click your project, then click the **Integrations** tab.

![Integrations tab in project settings](/assets/img/docs/integrations-overview-light.webp)

We provide three integrations:

1. Zapier
2. Slack
3. Webhooks

## Zapier

Exceptionless has built a [first-class Zapier application](https://zapier.com/apps/exceptionless/integrations) that allows you to connect your Exceptionless account to over 3,000 other apps. Click **Connect Zapier** on the Integrations tab to open the Exceptionless app on Zapier, or sign into your Zapier account and search for Exceptionless.

## Slack

Connecting to Slack is as simple as clicking the **Connect Slack** button. You'll be prompted to sign into your Slack workspace. Once signed in, you can choose the channel for your notifications to be sent to. Then, back on the Integrations tab, use the switches to choose which notifications are sent: **New Errors**, **Critical Errors**, **Error Regressions**, **New Events**, and **Critical Events**. To disconnect, click **Remove Slack**.

## Webhooks

By configuring a webhook, you are telling Exceptionless to post data to the specified endpoint whenever specific events happen. This is useful in creating custom, bespoke integrations for your Exceptionless account. Configuring a webhook is simple. Click the **+** (Add Webhook) button in the **Webhooks** section of the Integrations tab, enter the **URL** to call, choose the **Event Types** that should trigger it, and click **Add Webhook**.

![Add New Webhook dialog with a URL and event types selected](/assets/img/docs/integrations-add-webhook-light.webp)

You can choose from these event types:

- **New Error**: a new error that has never been seen before is reported to your project.
- **Critical Error**: an error that has been marked as critical is reported to your project.
- **Regression**: an event that has been marked as fixed has reoccurred in your project.
- **New Event**: a new event that has never been seen before is reported to your project.
- **Critical Event**: an event that has been marked as critical is reported to your project.
- **Promoted**: a stack is promoted to external systems.

Here's an example of the event payload (this applies to v2 of the Exceptionless API):

```json
{
  "id": "22cd0826e447a44e78877a22",
  "url": "http://localhost:5200/event/22cd0826e447a44e78877a22",
  "occurrence_date": "2014-01-17T14:37:02.739-06:00",
  "type": "error",
  "message": "A potentially dangerous Request.Path value was detected from the client (&).",
  "project_id": "537650f3b77efe23a47914f4",
  "project_name": "Disintegrating Pistol",
  "organization_id": "537650f3b77efe23a47914f3",
  "organization_name": "Acme",
  "stack_id": "1ecd0826e447a44e78877ab1",
  "stack_url": "http://localhost:5200/stack/1ecd0826e447a44e78877ab1",
  "stack_title": "A potentially dangerous Request.Path value was detected from the client (&).",
  "stack_tags": [
    "Test"
  ],
  "total_occurrences": 0,
  "first_occurrence": "2014-01-17T20:37:02.739Z",
  "last_occurrence": "2014-01-17T20:37:02.739Z",
  "is_new": false,
  "is_regression": false,
  "is_critical": false
}
```

To see both v1 and v2 models for events and stacks, [see this link](https://github.com/exceptionless/Exceptionless/tree/master/tests/Exceptionless.Tests/Plugins/WebHookData).

---

[Next > FAQ](/docs/FAQ)
