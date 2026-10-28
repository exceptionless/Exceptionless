---
title: "Administration"
metaTitle: "Administer a self-hosted Exceptionless instance"
description: "Use the System pages to run migrations, stop event intake, control Exie, review AI usage, manage OAuth apps, and post banner notifications."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/overview/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/elasticsearch/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/exie/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/settings/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/actions/[[category]]/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/notifications/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/saved-views/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/oauth-applications/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/system/migrations/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/components/assistant-settings.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/components/table/migration-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/components/oauth-applications/oauth-application-form.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/components/dialogs/run-maintenance-job-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/admin/models.ts
  - src/Exceptionless.Core/Services/SystemSettingsService.cs
  - src/Exceptionless.Core/Migrations/MigrationRerunService.cs
  - src/Exceptionless.Job/JobRunnerOptions.cs
  - src/Exceptionless.Web/Api/Handlers/AuthHandler.cs
---

# Administration

Global administrators manage a self-hosted instance from the **System** pages: migrations, runtime settings, Exie, OAuth
apps, banner notifications, and maintenance jobs.

## Open the System pages

The first user who signs up on a new instance is a global administrator. In the sidebar, open **Settings** and select
**System**. Other users don't see it.

The pages are tabs across the top: **Overview**, **Elasticsearch**, **Exie**, **Settings**, **Actions**,
**Notifications**, **Saved Views**, **OAuth Apps**, and **Migrations**.

## See the state of the system

- **Overview** shows totals for organizations, users, projects, stacks, open stacks, and events, with charts of event
  volume by month, organization growth, and stack status by event type.
- **Elasticsearch** shows cluster health and storage on its **Overview** tab, every index on **Indices**, and snapshots
  on **Backups**.

## Change settings at runtime {#runtime-settings}

**Settings** holds switches that take effect without a restart. Each one shows a **Deployment default** badge when it
uses the value from your configuration, or **Runtime override** after you change it here. A runtime override wins over
the configuration until you select **Reset**.

| Setting                          | What it does                                                                                                                             |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| **Exie availability**            | Turns Exie on or off for every organization. Exie needs an API key in the configuration before it can be turned on.                      |
| **Conversation sharing default** | Whether people share their Exie messages and replies by default to help improve the feature. People can change their own choice in Exie. |
| **Exie model**                   | The model ID used for new Exie conversations and turns. See [AI features](/docs/self-hosting/ai/#choose-the-model).                      |
| **Event submission**             | Whether Exceptionless accepts new events.                                                                                                |

### Stop accepting events {#event-submission}

To stop taking in events, for example while you repair Elasticsearch:

1. On **Settings**, turn off **Event submission**.
2. Select **Save**.

New event submissions get HTTP 503 right away. Session heartbeats still succeed, but they don't record activity. To
accept events again, turn the switch back on and save, or select **Reset** to go back to the deployed setting. The
deployed setting is `EX_EventSubmissionDisabled`, which defaults to `false`.

## Review Exie usage {#exie-usage}

**Exie** shows this month's Exie usage across all organizations: **Active Organizations**, **Turns**, **Tokens**,
**Total Provider Cost**, **Tokens per Turn**, and **Average Cost**.

The **Organization Usage** table lists each organization by provider cost, then tokens. For each one it shows when Exie
was last used, its activity, tokens and cost against the plan's monthly limits, outcomes, and how many turns were
blocked. Totals can lag by up to five minutes.

## Run maintenance jobs

**Actions** lists maintenance jobs by category: **Billing**, **Configuration**, **Elasticsearch**, **Maintenance**,
**Security**, and **Users**. Each one describes what it does, and destructive jobs are marked **DESTRUCTIVE**.

1. Select **Run** next to the job.
2. Fill in any options. Some jobs accept a start date, an end date, or an organization ID.
3. Type the job's name to confirm, and select **Run Job**.

For example, **Configure Indexes** creates missing indexes and applies current mappings, and **Fix Stack Stats**
recalculates each stack's first occurrence, last occurrence, and event count.

## Post a banner notification

**Notifications** shows a banner to every user of the app, for example before planned maintenance.

1. Enter the **Message**. HTML is allowed and is sanitized before display.
2. Choose the **Level**: **Info**, **Warning**, or **Error**.
3. Select **Set Notification**, or **Update Notification** if one is already showing.

Select **Clear Notification** to remove it. While no notification is set here, the app shows `EX_NotificationMessage` if
you configured one.

## Manage predefined saved views

**Saved Views** manages the predefined [saved views](/docs/saved-views/) that each organization starts with. The
**Predefined** tab shows their definitions as JSON. Select **Edit JSON** to change them, then **Save Predefined**.

**Force Update** queues a background job that overwrites every organization-wide saved view whose predefined key matches
a saved definition, and creates missing predefined views. It discards customizations to those views. Private views and
views that don't match aren't changed.

The **Current Org** tab exports the saved views of the organization you're in as predefined definitions.

## Manage OAuth apps

**OAuth Apps** lists the OAuth clients that can request access to the API and the [MCP server](/docs/ai/mcp-server/).
Filter the list by **Authorized**, **Not authorized**, or **All applications**, by name or client ID, or by
organization.

Clients usually register themselves when someone connects an agent. To create one by hand:

1. Select **New OAuth App**.
2. Enter a **Name** and a **Client ID**.
3. Enter the **Redirect URIs**, one per line. Each must match exactly, with no wildcards, and use HTTPS or HTTP on a
   loopback address such as `localhost`.
4. Choose the **Scopes**: **MCP**, **Projects**, **Stacks**, **Stacks Write**, **Events**, and **Offline Access**.
5. Optionally add **Notes**, then select **Create App**.

Open an app to edit it. Check **Disabled** to block a client: disabled clients can't authorize, refresh, or use existing
access tokens. To delete an app, open its menu in the list and select **Delete**.

## Migrations {#migrations}

Exceptionless updates Elasticsearch with migrations. They run on their own when the job process starts, or when the app
starts if it runs the jobs in process. In Kubernetes, you start them yourself; see
[Kubernetes](/docs/self-hosting/kubernetes/#run-migrations).

**Migrations** shows the **Current Version** (the highest completed migration), how many have **Failed** or are
**Running**, and the **Total**. The table lists each migration's name, version, type, status, start and finish times,
duration, and error. Turn off **Show completed** to see only the ones that need attention.

### Rerun a migration {#rerunning-a-completed-migration}

Some completed migrations can be run again, for example after a fix to the data they change. Only those have an actions
menu.

1. Take an Elasticsearch snapshot, and stop any Exceptionless instances still running an older version.
2. On **Migrations**, open the migration's menu and select **Rerun migration**.
3. Type the phrase shown, such as `RERUN 5`, and select **Rerun Migration**.

The rerun runs in the background, and a message tells you when it finishes. It doesn't change the current migration
version or the original completion time.

When the jobs run in a separate process, a rerun from this page needs Redis for the cache and a distributed queue.
Without them, run it from the job image instead, with the same settings as your other containers. Use the migration's
name from the **Name** column. With the [Docker Compose setup](/docs/self-hosting/docker/):

```bash
docker compose run --rm jobs Migration --rerun MIGRATION_NAME
```
