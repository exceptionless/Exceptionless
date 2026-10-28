---
title: "Integrations"
metaTitle: "Integrations: Slack, webhooks, and Zapier"
description: "Send Exceptionless notifications to Slack, call your own endpoint with a webhook when errors happen, and connect other apps with Zapier."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/integrations/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/webhooks/options.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/webhooks/components/dialogs/add-webhook-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/webhooks/components/table/options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/webhooks/components/table/webhook-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-options-dropdown-menu.svelte
  - src/Exceptionless.Core/Models/WebHook.cs
  - src/Exceptionless.Core/Models/WebHookEvent.cs
  - src/Exceptionless.Core/Models/WebHookStack.cs
  - src/Exceptionless.Core/Plugins/WebHook/Default/020_VersionTwoPlugin.cs
  - src/Exceptionless.Core/Pipeline/070_QueueNotificationAction.cs
  - src/Exceptionless.Core/Jobs/WebHooksJob.cs
  - src/Exceptionless.Core/Services/WebHookDelivery.cs
  - src/Exceptionless.Core/Serialization/JsonSerializerOptionsExtensions.cs
  - src/Exceptionless.Web/Api/Handlers/WebHookHandler.cs
  - src/Exceptionless.Web/Api/Handlers/StackHandler.cs
  - tests/Exceptionless.Tests/Plugins/WebHookData/v2.event.expected.json
  - tests/Exceptionless.Tests/Plugins/WebHookData/v2.stack.expected.json
---

# Integrations

Integrations send what happens in a project to other tools: Slack messages, webhook calls to your own endpoint, and
Zapier zaps. They're set up per project. Open the project's settings and click the **Integrations** tab.

![Integrations tab in project settings](/assets/img/docs/integrations-overview-light.webp)

Slack and webhooks need a paid plan. On the Free plan, adding one asks you to upgrade.

## Slack

Connect a Slack channel to get new errors, critical errors, and regressions as Slack messages:

1. On the **Integrations** tab, click **Connect Slack**.
2. Sign in to your Slack workspace and choose the channel.
3. Choose which notifications to send with the switches: **New Errors**, **Critical Errors**, **Error Regressions**,
   **New Events**, and **Critical Events**.

To disconnect, click **Remove Slack**. See [Notifications](/docs/notifications/) for what each notification means and
how often they're sent.

## Webhooks

A webhook calls a URL of yours whenever certain things happen in the project. Use it to create tickets, page someone, or
feed your own tools.

### Add a webhook

1. On the **Integrations** tab, click **+** (**Add Webhook**) in the **Webhooks** section.
2. Enter the **URL** to call.
3. Choose the **Event Types** that call it.
4. Click **Add Webhook**.

![Add New Webhook dialog with a URL and event types selected](/assets/img/docs/integrations-add-webhook-light.webp)

To remove a webhook, open its menu (**...**) in the list and click **Delete**.

### Event types

| Event type         | Value in the API  | Called when                                        | Payload |
| ------------------ | ----------------- | -------------------------------------------------- | ------- |
| **New Error**      | `NewError`        | An error happens that starts a new stack.          | Event   |
| **Critical Error** | `CriticalError`   | An error marked critical happens.                  | Event   |
| **Regression**     | `StackRegression` | An event happens in a stack marked fixed.          | Event   |
| **New Event**      | `NewEvent`        | An event of any type starts a new stack.           | Event   |
| **Critical Event** | `CriticalEvent`   | An event of any type marked critical happens.      | Event   |
| **Promoted**       | `StackPromoted`   | Someone clicks **Promote To External** on a stack. | Stack   |

An event is critical when its stack has **Future Occurrences Are Critical** turned on, or when your code marks it
critical. Events in stacks that are fixed, snoozed, ignored, or discarded don't call webhooks. Unlike email and Slack,
webhooks aren't rate limited: every matching event calls them.

**Promote To External** is in a stack's options menu. It sends the stack to every **Promoted** webhook in the project,
for example to open an issue in your tracker. If the project has no **Promoted** webhook, the app offers to take you to
the **Integrations** tab. See [Stacks](/docs/managing-stacks/).

### Delivery

- Exceptionless sends a `POST` request with a JSON body (`Content-Type: application/json`).
- Your endpoint has 5 seconds to respond with a `2xx` status code.
- Requests aren't signed. Use an HTTPS URL that's hard to guess, such as one with a secret token in the path, and check
  the IDs in the payload against the [API](/docs/api/) if you need to be sure.
- A `401`, `403`, or `410` response disables the webhook right away.
- After more than 10 failed deliveries in a row, Exceptionless tries at most once every 15 minutes and skips the calls
  in between. If deliveries keep failing for more than two days, it disables the webhook.

A disabled webhook stays in the list but isn't called. To turn it back on, delete it and add it again.

### Event payload

The five event types send the event that triggered the webhook, with its stack:

<!-- source: src/Exceptionless.Core/Models/WebHookEvent.cs, field order and names; null fields are omitted (JsonSerializerOptionsExtensions.cs) -->

```json
{
  "id": "6720f3c4e1b2a3c4d5e6f708",
  "url": "https://be.exceptionless.io/event/6720f3c4e1b2a3c4d5e6f708",
  "occurrence_date": "2026-10-27T14:37:02.739-05:00",
  "type": "error",
  "environment": "production",
  "message": "Object reference not set to an instance of an object.",
  "project_id": "537650f3b77efe23a47914f4",
  "project_name": "Store API",
  "organization_id": "537650f3b77efe23a47914f3",
  "organization_name": "Acme",
  "stack_id": "1ecd0826e447a44e78877ab1",
  "stack_url": "https://be.exceptionless.io/stack/1ecd0826e447a44e78877ab1",
  "stack_title": "Object reference not set to an instance of an object.",
  "stack_tags": ["checkout"],
  "total_occurrences": 42,
  "first_occurrence": "2026-10-02T16:11:45.102Z",
  "last_occurrence": "2026-10-27T19:37:02.739Z",
  "is_new": false,
  "is_regression": true,
  "is_critical": false
}
```

| Field                                                      | Description                                                                      |
| ---------------------------------------------------------- | -------------------------------------------------------------------------------- |
| `id`, `url`                                                | The event ID and its page in the app.                                            |
| `occurrence_date`                                          | When the event happened, with the offset the client sent.                        |
| `type`, `source`, `message`, `environment`, `tags`         | The event's type, source, message, [environment](/docs/environments/), and tags. |
| `project_id`, `project_name`                               | The project.                                                                     |
| `organization_id`, `organization_name`                     | The organization.                                                                |
| `stack_id`, `stack_url`, `stack_title`                     | The stack the event belongs to and its page in the app.                          |
| `stack_description`, `stack_tags`                          | The stack's description and tags.                                                |
| `total_occurrences`, `first_occurrence`, `last_occurrence` | The stack's occurrence count and first and last occurrence, in UTC.              |
| `date_fixed`                                               | When the stack was marked fixed, if it is.                                       |
| `is_new`                                                   | `true` when this event started the stack.                                        |
| `is_regression`                                            | `true` when this event regressed a fixed stack.                                  |
| `is_critical`                                              | `true` when the event is marked critical.                                        |

Fields without a value, and empty lists, are left out of the payload. For example, `source` is usually missing on
errors, and `tags` is missing when the event has none.

### Stack payload

**Promoted** webhooks send the stack:

<!-- source: src/Exceptionless.Core/Models/WebHookStack.cs and tests/Exceptionless.Tests/Plugins/WebHookData/v2.stack.expected.json -->

```json
{
  "id": "1ecd0826e447a44e78877ab1",
  "url": "https://be.exceptionless.io/stack/1ecd0826e447a44e78877ab1",
  "title": "Object reference not set to an instance of an object.",
  "tags": ["checkout"],
  "type": "System.NullReferenceException",
  "project_id": "537650f3b77efe23a47914f4",
  "project_name": "Store API",
  "organization_id": "537650f3b77efe23a47914f3",
  "organization_name": "Acme",
  "total_occurrences": 42,
  "first_occurrence": "2026-10-02T16:11:45.102Z",
  "last_occurrence": "2026-10-27T19:37:02.739Z",
  "is_regression": true,
  "is_critical": false
}
```

The stack payload can also include `description`, `request_path` (for 404 stacks), `target_method` (the method an error
stack is grouped on), `date_fixed`, and `fixed_in_version`.

## Zapier

Exceptionless has a [Zapier app](https://zapier.com/apps/exceptionless/integrations) that connects your projects to
thousands of other apps without code. Click **Connect Zapier** on the **Integrations** tab to open it, or search for
Exceptionless in Zapier. The Zapier app connects with a project's client API key, so each connection works with one
project. Create a key on the project's **API Keys** tab (see [Projects](/docs/project-settings/#api-keys)).

## Ask Exie

[Exie](/docs/ai/exie/) can attach links to a stack, such as the issue a webhook created. Exie is on Medium plans and up.

```prompt exie
Add https://github.com/acme/store/issues/412 as a reference link on this stack.
```

```prompt exie
Which new errors appeared in this project since yesterday?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can connect Exceptionless to your issue
tracker:

```prompt agent
Find the top new error in my Exceptionless project from the last 24 hours, open a GitHub issue for it with the stack trace and a link to the stack, and add the issue URL to the stack as a reference link.
```
