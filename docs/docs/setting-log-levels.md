---
title: "Logs and log levels"
navTitle: "Logs and log levels"
metaTitle: "Viewing logs and setting log levels in Exceptionless"
description: "Find log events in Exceptionless and set a project's minimum log level, or one per log source, without redeploying your app."
sources:
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Core/Pipeline/010_AssignToStackAction.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/settings/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/project-log-level.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/projects/components/table/config-options.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/stacks/components/stack-log-level.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/events/options.ts
  - Exceptionless.Net:src/Exceptionless/Models/Collections/SettingsDictionary.cs
  - Exceptionless.Net:src/Exceptionless/Plugins/Default/010_EventExclusionPlugin.cs
  - Exceptionless.JavaScript:packages/core/src/plugins/default/EventExclusionPlugin.ts
---

# Logs and log levels

Exceptionless stores log messages as `log` events, each with a source (usually the logger name) and a level. This page
shows where to find them and how to set the minimum level your apps send, for the whole project or for one log source,
without changing code or redeploying.

To send logs, see the sending events page for [.NET](/docs/clients/dotnet/sending-events/) or
[JavaScript](/docs/clients/javascript/sending-events/), or connect your logging framework with the
[.NET logging integrations](/docs/clients/dotnet/logging/).

## Find your logs

- **Events** > **Logs** in the sidebar lists log messages from open and regressed stacks, newest first.
- Add a **Log Level** filter to any list to narrow it to levels such as **Error** and **Fatal**. In a raw filter, use
  `type:log level:error`.
- Add the **Level** and **Source** columns with **View** > **Manage Columns...**.

![The Logs events view](/assets/img/docs/setting-log-levels-logs-view-light.webp)

Logs from the same source group into one stack, so you can mark a noisy source ignored, or snooze it, like any other
stack. See [Stacks](/docs/managing-stacks/).

## Log levels

From lowest to highest: **Trace**, **Debug**, **Information**, **Warning**, **Error**, and **Fatal**. A minimum level
keeps that level and everything above it. **Off** stops all logs.

## Set the project's default level

1. Open **Settings** > **Projects** in the sidebar and select the project.
2. Open the **Settings** tab.
3. Under **Default Log Level**, open the menu and pick the lowest level to keep.

Every log source in the project uses this level unless it has its own.

## Set the level for one log source

1. Open a log event, or a log stack, from any list.
2. In the stack card, select the log level button, the first of the buttons at the top right. Its tooltip shows the
   current level, such as "Log Level: Warning (Default)".
3. Pick a level.

![Log Level menu on a log stack](/assets/img/docs/setting-log-levels-stack-override-light.webp)

The button appears only on log stacks that have a source. Once a source has its own level, the menu also shows
**Default** with the project's level. Select it to go back to the project default.

## How levels are applied

Log levels are client configuration values that your apps download from Exceptionless. Each client checks a log event's
level and drops it before sending if it's below the minimum, so dropped logs never reach Exceptionless and don't count
against your plan. Your apps pick up a change the next time they sync their configuration. See client configuration
values for [.NET](/docs/clients/dotnet/client-configuration-values/) and
[JavaScript](/docs/clients/javascript/client-configuration-values/).

- The project default is stored as `@@log:*`, and a source's level as `@@log:` followed by the source name.
- Per-source levels show up on the project's **Configuration** tab. There you can also add a level for a group of
  sources with a wildcard, such as `@@log:Acme.Data.*` set to `debug`. The most specific match wins. See
  [Projects](/docs/project-settings/#client-configuration).
- Logs without a level are always sent.
- With no level set for a source or the project, the .NET client sends **Warning** and above, and the JavaScript client
  sends every level. The .NET logging integrations, such as the Microsoft.Extensions.Logging provider and the NLog
  target, change that default to **Trace**, so your logging rules decide. See
  [.NET logging](/docs/clients/dotnet/logging/).

Exceptionless doesn't filter logs on the server. Logs sent another way, such as straight to the API, are stored at any
level.

## Ask Exie

Exie can't change log levels, but it can tell you where your logs come from:

```prompt exie
Which log sources sent the most error and fatal logs today?
```

```prompt exie
How many warning logs did Acme.Billing send each hour over the last 24 hours?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can trace a noisy log back to the code:

```prompt agent
Find the log source in my Exceptionless project that sent the most error logs in the last 24 hours, then find where
this repository writes those logs and tell me whether they should be errors.
```
