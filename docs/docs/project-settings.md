---
title: "Projects"
metaTitle: "Exceptionless project settings"
description: "Manage a project's API keys, client configuration values, data exclusions, stacking and spam settings, source maps, and reset or delete the project."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/+layout.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/manage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/api-keys/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/settings/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/configuration-values/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/source-maps/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/stacks/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/usage/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/list/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/tokens/project-token.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/tokens/components/table/token-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/table/config-options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/dialogs/add-project-config-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/dialogs/reset-project-data-dialog.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/dialogs/remove-project-dialog.svelte
  - src/Exceptionless.Core/Models/Collections/SettingsDictionary.cs
  - src/Exceptionless.Core/Extensions/ProjectExtensions.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/0_ThrottleBotsPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/40_RequestInfoPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/45_EnvironmentInfoPlugin.cs
  - src/Exceptionless.Core/Plugins/EventProcessor/Default/90_RemovePrivateInformationPlugin.cs
  - src/Exceptionless.Core/Configuration/AppOptions.cs
  - src/Exceptionless.Web/Api/Endpoints/ProjectEndpoints.cs
  - src/Exceptionless.Web/Utility/Headers.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - Exceptionless.Net:src/Exceptionless/ExceptionlessClient.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/110_IgnoreUserAgentPlugin.cs
  - Exceptionless.Net:src/Exceptionless/Extensions/StringExtensions.cs
  - Exceptionless.JavaScript:packages/core/src/ExceptionlessClient.ts
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
---

# Projects

A project is one app or service that sends events to Exceptionless. Each project has its own API keys and settings. This
page covers every project setting: API keys, client configuration values, data exclusions, stacking, spam detection,
source maps, and resetting or deleting a project.

## Open a project's settings

In the sidebar, open **Settings** > **Projects** and click the project. The settings page has these tabs:

- **General**: the project name, shortcuts, and the reset and delete buttons.
- **API Keys**: client API keys and source map upload tokens.
- **Settings**: the default log level, data exclusions, error stacking, source maps, and spam detection.
- **Configuration**: client configuration values.
- **Integrations**: Slack, webhooks, and Zapier. See [Integrations](/docs/integrations/).
- **Stack Management**: every stack in the project, including ignored and discarded ones you want to restore. See
  [Stacks](/docs/managing-stacks/).
- **Usage**: the project's event history against the organization's plan limit, including blocked, discarded, too big,
  and deleted events.

The **General** tab also has **Go To Stacks**, **Client Setup** (install a client and send your first event),
**Notifications** (your own email notifications for this project, see [Notifications](/docs/notifications/)), and
**Generate Sample Data**, which queues sample events so you can try the app.

You can also reach most of these from the command palette: press <kbd>/</kbd> and type **Project**. See
[Navigating the app](/docs/navigation/).

## API keys {#api-keys}

A client API key lets an app send events to one project. It can't read your data or change settings, so it's safe to
ship inside an app.

1. Open the **API Keys** tab.
2. Click **Add token** and choose:
   - **Client API key** to send events from an application or service.
   - **Source map upload token** to upload source maps from CI/CD for this project only. Its scope is
     `source-maps:write`.
3. Open the menu (**...**) on the new row and click **Copy Token**.

![API Keys tab with the Add token menu open](/assets/img/docs/project-settings-api-keys-light.webp)

The same menu has **Edit Notes**, **Disable Token** (or **Enable Token**), and **Delete**. A disabled key is struck
through in the list and stops working until you enable it again. To rotate a key, add a new one, deploy it, then disable
or delete the old one.

To install a client with the key, open **Client Setup** on the **General** tab or see [Choose a client](/docs/clients/).

## Client configuration values {#client-configuration}

Client configuration values are key-value pairs that you set on the project and that the Exceptionless clients download
while your app runs. Use them to change your app's behavior without a redeploy, for example to turn a feature on or off
or to drop a noisy kind of event.

To add one:

1. Open the **Configuration** tab.
2. Click **+** (**Add Configuration Value**).
3. Enter a **Key** and a **Value**, then click **Add Configuration Value**.

![Configuration tab listing a project's client configuration values](/assets/img/docs/project-settings-configuration-light.webp)

To change or remove a value, open its menu (**...**) and click **Edit** or **Delete**.

The table doesn't list the values that the **Settings** tab manages: `@@DataExclusions`, `@@IncludePrivateInformation`,
`@@UserAgentBotPatterns`, `@@log:*`, `UserNamespaces`, and `CommonMethods`.

### How values reach your app

The .NET and JavaScript clients keep their copy up to date:

- Every response to an event submission includes the project's current configuration version in the
  `X-Exceptionless-ConfigVersion` header. If it's newer than the client's copy, the client downloads the new values.
- When the app is idle, the client also checks for changes: 5 seconds after startup, then every 2 minutes.
- A check sends only the client's current version, and downloads values only when they've changed. Checks don't count
  toward your plan's event limit.

You can change the idle interval, or turn idle checks off, in each client's configuration. A custom client can download
the values itself with `GET /api/v2/projects/config` and its client API key.

To read values and react to changes in your code, see the client pages:

- [.NET client configuration values](/docs/clients/dotnet/client-configuration-values/)
- [JavaScript client configuration values](/docs/clients/javascript/client-configuration-values/)

### Discard events with configuration values

The clients discard events whose type and source match a key in the form `@@EVENT_TYPE:SOURCE` with the value `false`.
The source can contain `*` as a wildcard. For errors, the source is the exception type, and the clients check inner
exceptions too.

| Key                                    | Value   | What it discards                                 |
| -------------------------------------- | ------- | ------------------------------------------------ |
| `@@error:System.ArgumentNullException` | `false` | Errors with an `ArgumentNullException`           |
| `@@error:*`                            | `false` | All errors                                       |
| `@@404:*`                              | `false` | All 404 events                                   |
| `@@404:/favicon.ico`                   | `false` | 404 events for one path                          |
| `@@usage:*`                            | `false` | All [feature usage](/docs/feature-usage/) events |

Log events work the same way, except the value is a minimum log level: `Trace`, `Debug`, `Info`, `Warn`, `Error`,
`Fatal`, or `Off`. The **Default Log Level** on the **Settings** tab sets `@@log:*`, and a key such as
`@@log:*AuthController` overrides it for matching log sources. See [Logs and log levels](/docs/setting-log-levels/).

## Data exclusions {#data-exclusions}

Data exclusions remove fields you don't want stored, such as passwords or card numbers, from your events. They're on the
**Settings** tab, under **Data Exclusions**.

![Data Exclusions section on the project Settings tab](/assets/img/docs/project-settings-data-exclusions-light.webp)

Enter a comma-separated list of field names. Matching is case-insensitive, and `*` is a wildcard:

| Entry        | Removes fields that   |
| ------------ | --------------------- |
| `Password`   | are named `Password`  |
| `Password*`  | start with `Password` |
| `*Password`  | end with `Password`   |
| `*Password*` | contain `Password`    |

The clients remove matching fields from the data they collect, such as extended data properties, form fields, cookies,
and query string parameters, before an event leaves your app. The server also removes matching cookies, query string
parameters, and POST data fields from every event it receives, so those exclusions apply even to clients that don't
support them. Changes save when you leave the field.

Turn on **Automatically remove user identifiable information from events** to have the server strip personal data from
every event in the project: the user identity, the email address on user descriptions, IP addresses, the machine name,
cookies, query string parameters, and POST data.

For what each client collects and the settings that control it in code, see [Security and privacy](/docs/security/) and
the [.NET private information](/docs/clients/dotnet/private-information/) page.

## Error stacking

The **Error Stacking** section of the **Settings** tab controls which stack frame Exceptionless uses to group errors
into stacks:

- **User Namespaces**: a comma-separated list of the namespaces your code is in, such as `Contoso`. When set, only
  methods in these namespaces are used as stacking targets.
- **Common Methods**: a comma-separated list of method names, such as `Assert`, that should never be used as stacking
  targets. Use it for shared helpers that throw for many unrelated reasons.

Changes apply to new errors. See [Stacking](/docs/concepts/stacking/) for how Exceptionless picks a stack.

## Source maps

Exceptionless downloads public source maps for minified JavaScript automatically. To upload private maps, click **Manage
source maps** under **JavaScript Source Maps** on the **Settings** tab, or create a **Source map upload token** on the
**API Keys** tab for your build pipeline. See [Source maps](/docs/clients/javascript/source-maps/).

## Spam detection {#spam-detection}

The **Spam Detection** section of the **Settings** tab filters out bots and crawlers.

![Spam Detection section on the project Settings tab](/assets/img/docs/project-settings-spam-detection-light.webp)

- **User agents**: a comma-separated list of user agent patterns, with `*` wildcards. New projects start with
  `*bot*,*crawler*,*spider*,*aolbuild*,*teoma*,*yahoo*`. The .NET and JavaScript clients don't send events from requests
  whose user agent matches. The server marks matching events as bots and doesn't send notifications for them.
- **Reduce noise by automatically hiding high volumes of events coming from a single client IP address**: when more than
  25 events arrive from one public IP address within five minutes, the server discards them and deletes the events from
  that window. This needs IP addresses, so it doesn't apply while the project removes user identifiable information.

## Reset project data

Resetting deletes every event and stack in the project and keeps its settings, API keys, and integrations. Use it to
start fresh after testing.

1. On the **General** tab, click the warning button (**Reset project data**).
2. Click **Reset Project Data** to confirm.

The reset runs in the background and can't be undone.

## Delete a project {#delete-a-project}

1. On the **General** tab, click the **X** button (**Delete project**).
2. Click **Delete Project** to confirm.

The project and its data are queued for deletion. You can also delete a project from its menu (**...**) on the
**Projects** list.

## Ask Exie

[Exie](/docs/ai/exie/) is on Medium plans and up.

```prompt exie
List my projects and when each one last received an event.
```

```prompt exie
How do I send events from my ASP.NET Core app to this project?
```

```prompt exie
Which error types happened most in this project over the last 7 days?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can set up a client for you:

```prompt agent
Get the Exceptionless client setup for my project and add Exceptionless to this app. Read the API key from configuration instead of hard-coding it.
```
