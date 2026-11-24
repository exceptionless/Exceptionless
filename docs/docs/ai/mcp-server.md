---
title: "MCP server"
metaTitle: "Exceptionless MCP server for AI coding agents"
description: "Connect Claude Code, Codex, Cursor, VS Code, or another MCP client to Exceptionless to search errors, update stacks, and manage saved views."
sources:
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.SavedViews.cs
  - src/Exceptionless.Web/Mcp/McpModels.cs
  - src/Exceptionless.Web/Mcp/McpOriginValidationMiddleware.cs
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Core/Services/OAuthService.cs
  - src/Exceptionless.Core/Configuration/OAuthServerOptions.cs
  - src/Exceptionless.Core/Authorization/AuthorizationRoles.cs
  - src/Exceptionless.Web/Api/Endpoints/OAuthEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/OAuthHandler.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/users/components/oauth-grants/table/oauth-grant-actions-cell.svelte
  - src/Exceptionless.Web/Api/Endpoints/UserEndpoints.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(auth)/oauth/authorize/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/ai-tools/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/applications/+page.svelte
  - tests/Exceptionless.Tests/Api/Endpoints/OAuthEndpointTests.cs
---

# MCP server

The Exceptionless MCP server lets AI coding agents work with your production errors. It implements the
[Model Context Protocol](https://modelcontextprotocol.io/), so an agent can search your stacks and events, read stack
traces and request details, and, with your permission, update stacks and manage saved views while it fixes your code.

| Setting        | Value                                                                        |
| -------------- | ---------------------------------------------------------------------------- |
| Server URL     | `https://be.exceptionless.io/mcp`                                            |
| Transport      | Streamable HTTP                                                              |
| Authentication | OAuth in your browser. No API key or client secret.                          |
| Access         | Read-only when neither **Stacks Write** nor **Saved Views Write** is granted |
| Plans          | Every plan, including Free                                                   |

> [!NOTE]
> On a self-hosted instance, the server is at `/mcp` on your API URL, for example
> `https://exceptionless.example.com/mcp`. See [AI features](/docs/self-hosting/ai/) to set it up.

The **AI Tools** page under **My Account** in the app shows setup instructions with the server URL for your instance,
for Claude Code, Codex, Cursor, Copilot (the GitHub Copilot CLI), OpenCode, and VS Code. The Cursor and VS Code options
include an **Add to Cursor** or **Add to VS Code** install link and configuration you can copy.

## Connect your agent

Add the server to your agent, then approve access in the browser window that opens. See
[Approve access](#approve-access) for what the consent screen asks.

### Claude Code

1. Add the server:

   ```bash
   claude mcp add --transport http exceptionless https://be.exceptionless.io/mcp
   ```

   This adds it for you in the current project. Add `--scope user` to use it in every project, or `--scope project` to
   share it with your team in `.mcp.json`.

2. Sign in:

   ```bash
   claude mcp login exceptionless
   ```

   You can also run `/mcp` inside Claude Code and select the server.

3. Approve access in your browser.

### Codex

1. Add the server:

   ```bash
   codex mcp add exceptionless --url https://be.exceptionless.io/mcp
   ```

   Or add it to `~/.codex/config.toml`:

   ```toml
   [mcp_servers.exceptionless]
   url = "https://be.exceptionless.io/mcp"
   ```

2. Approve access in your browser. If Codex didn't open it when you added the server, sign in:

   ```bash
   codex mcp login exceptionless
   ```

### Cursor

1. On **My Account** > **AI Tools**, choose **Cursor** and select **Add to Cursor**. Or add the server to
   `~/.cursor/mcp.json`, or `.cursor/mcp.json` in your project, keeping any servers already there:

   ```json
   {
     "mcpServers": {
       "exceptionless": {
         "url": "https://be.exceptionless.io/mcp"
       }
     }
   }
   ```

2. Connect to the server in Cursor, and approve access in your browser when it asks you to sign in.

### VS Code with GitHub Copilot

1. On **My Account** > **AI Tools**, choose **VS Code** and select **Add to VS Code**. Or add the server to
   `.vscode/mcp.json` in your workspace, keeping any servers already there:

   ```json
   {
     "servers": {
       "exceptionless": {
         "type": "http",
         "url": "https://be.exceptionless.io/mcp"
       }
     }
   }
   ```

2. Start the server in VS Code, or ask Copilot Chat a question that uses Exceptionless. Allow the sign-in when VS Code
   asks, then approve access in your browser.

### GitHub Copilot CLI

1. Add the server:

   ```bash
   copilot mcp add --transport http exceptionless https://be.exceptionless.io/mcp
   ```

2. Start Copilot with a question that uses Exceptionless, and approve access in your browser when it asks you to sign
   in:

   ```bash
   copilot -i "List my Exceptionless projects"
   ```

### OpenCode

1. Add the server:

   ```bash
   opencode mcp add exceptionless --url https://be.exceptionless.io/mcp
   ```

2. Sign in, then approve access in your browser:

   ```bash
   opencode mcp auth exceptionless
   ```

### Other MCP clients

Any MCP client that supports remote servers over streamable HTTP with OAuth can connect with the server URL. The server
supports:

- OAuth 2.1 authorization code flow with PKCE (`S256`).
- Client ID Metadata Documents and dynamic client registration for public clients. No client secret is needed.
- Redirect URIs that use HTTPS, or HTTP on a loopback address.
- Discovery metadata at `/.well-known/oauth-protected-resource/mcp` and `/.well-known/oauth-authorization-server`.

Requests from a browser must come from the Exceptionless app's own origin, so the server rejects browser-based clients
on other sites. Desktop, command-line, and server-side clients don't send an origin and aren't affected.

## Approve access {#approve-access}

When your agent connects, Exceptionless opens **Approve OAuth access** in your browser. If you aren't signed in, you
sign in first. The screen shows:

- **Signed in as:** the account the agent will act as.
- **Organizations:** every organization you belong to, all selected. Clear the ones the agent shouldn't use. You must
  keep at least one.
- **Application**, **Redirect URI**, and **Resource:** who is asking and where the token goes. Check that they match the
  agent you're connecting.
- **Scopes:** the permissions the agent requested that the application is allowed to use. **MCP** is marked **Required**
  and can't be cleared. Every other requested scope starts selected; clear any you don't want.

Select **Approve** to finish, or **Cancel** to deny access.

| Permission        | Scope               | Lets the agent                                                                            |
| ----------------- | ------------------- | ----------------------------------------------------------------------------------------- |
| MCP               | `mcp:read`          | Connect to the server, list organizations, find a project by name, and list filter fields |
| Projects Read     | `projects:read`     | List and read projects and their setup information                                        |
| Stacks Read       | `stacks:read`       | Search and read stacks                                                                    |
| Events Read       | `events:read`       | Search, count, and read events                                                            |
| Stacks Write      | `stacks:write`      | Change stack status, snooze, set critical, and add or remove reference links              |
| Saved Views Read  | `saved-views:read`  | List saved views visible to your account                                                  |
| Saved Views Write | `saved-views:write` | Create, update, and permanently delete saved views, including shared views                |
| Offline Access    | `offline_access`    | Stay connected without asking you again when its access token expires                     |

The saved-view scopes apply to the MCP resource, not OAuth access to the REST API.

### Keep a connection read-only {#read-only}

MCP clients usually request every scope the server supports, and every requested scope starts selected, including
**Stacks Write** and **Saved Views Write**. To keep a connection read-only, clear both before you select **Approve**.
Clear **Offline Access** too if the connection should end when its one-hour access token expires.

An application that registered itself, through dynamic client registration or a client ID metadata document, can request
any supported scope, whatever it registered with. The scopes you approve on the consent screen are what the connection
gets.

Once an administrator saves an application's scopes, that list is enforced. The consent screen then shows only the
requested scopes the application allows. Authorization fails if a required scope such as **MCP** isn't allowed, or if
the request includes an unsupported scope; restart the agent's authorization flow with a valid request.

If the agent calls a tool it doesn't have permission for, the tool returns an error that names the missing scope. To
change the permissions of a connection, revoke it and connect again.

Existing tokens don't gain new permissions automatically. To use saved-view tools with an existing connection, reconnect
and approve **Saved Views Read** and, for changes, **Saved Views Write**. Clients with a fixed requested scope list must
add the needed scopes before reconnecting. For an application whose scopes an administrator saved, the administrator
must allow the new scopes first.

### How long access lasts

Access tokens expire after one hour. With **Offline Access**, the agent refreshes its token without asking you. A
connection that goes 30 days without refreshing has to sign in again.

The agent can only use the organizations you approved, and only while you're still a member. If you leave an
organization, the connection loses access to it right away.

## Manage connected applications {#connected-applications}

**My Account** > **Applications** lists every application that can access your account, with the **Access** it has and
the **Organizations** it can use.

To disconnect an agent, open the row's menu, select **Revoke Access**, and confirm. The agent has to complete OAuth
again before it can use your account. See [Your account](/docs/account/).

## Tools {#tools}

Search tools use the same filter syntax as the app. See [Search syntax](/docs/reference/search-syntax/). Time ranges use
date math, such as `[now-24h TO now]`, `[now/d TO now/d]` for today, or `[2026-06-01 TO 2026-06-30]`, and are resolved
in the `timeZone` you pass, or UTC.

Filters can't start with a wildcard: use `title:timeout` or `title:time*`, not `title:*timeout*`.

Every tool returns `ok`, with either `data` or an `error` that has a code and message. Projects, stacks, events, and
saved views in results include a `url` with their REST API path. Dates in results are UTC.

### Read tools

| Tool                            | Scope           | What it does                                                                                                                                                                                                                                                                                   |
| ------------------------------- | --------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `list_organizations`            | `mcp:read`      | Lists the organizations this connection can use.                                                                                                                                                                                                                                               |
| `resolve_project`               | `mcp:read`      | Finds a project by `projectId` or exact `projectName`. Inputs can be omitted when only one project is available.                                                                                                                                                                               |
| `get_filter_fields`             | `mcp:read`      | Lists the fields you can filter and sort projects, stacks, and events by.                                                                                                                                                                                                                      |
| `list_projects`                 | `projects:read` | Lists projects. Takes `organizationId`, `filter`, `sort` (`name`, `created_utc`, `updated_utc`, `last_event_date_utc`), and paging. A `name` filter matches whole words (`name:web`) or word prefixes (`name:web*`).                                                                           |
| `get_project`                   | `projects:read` | Returns a project's summary.                                                                                                                                                                                                                                                                   |
| `get_project_setup`             | `projects:read` | Returns a link to the project's **Client Setup** page and the supported clients: .NET, JavaScript / Node.js, React Native, and Expo. Takes `projectId` or an exact `projectName`.                                                                                                              |
| `get_client_setup_instructions` | `projects:read` | Returns setup steps for a React Native or Expo app (`platform`: `react-native` or `expo`), including the project's API key.                                                                                                                                                                    |
| `search_stacks`                 | `stacks:read`   | Searches one project's stacks by status, tag, title, type, and other stack fields. `time` matches each stack's last occurrence. Pass `projectId` when more than one project is available.                                                                                                      |
| `get_stack`                     | `stacks:read`   | Returns a stack by `stackId`: status, all-time total, first and last occurrence, tags, reference links, and fixed version.                                                                                                                                                                     |
| `get_stack_events`              | `events:read`   | Lists a stack's recent events. Takes `filter`, `sort`, `time`, and paging.                                                                                                                                                                                                                     |
| `search_events`                 | `events:read`   | Searches a project's events by user, message, path, error type, environment, tag, indexed custom data (`data.*`), and more.                                                                                                                                                                    |
| `get_event`                     | `events:read`   | Returns an event by `eventId`, with its error and stack trace, request, environment, and custom data. `maxDetailSize` sets the detail size in bytes: 16384 by default, up to 65536.                                                                                                            |
| `count_events`                  | `events:read`   | Counts events and occurrences in a project. `interval` (such as `1h` or `1d`) returns trend buckets. `groupBy` groups by `stack`, `version`, `environment`, `type`, `source`, `status`, `tag`, `user`, `level`, `error.type`, `error.code`, `os`, `os.version`, or `browser`, up to 25 groups. |

`count_events` with `groupBy` `stack` ranks the stacks that happened most in a time range and includes each stack's
first occurrence, so a stack first seen in the range is a new error. Use it, not `search_stacks`, to find what happened
in a period.

Project, stack, and event list tools return 10 rows by default and up to 100. When `pagination.hasMore` is true, pass
`pagination.after` or `pagination.before` to page. Saved-view lists have a separate limit below.

### Stack write tools {#write-tools}

Stack write tools need **Stacks Write**. They're marked as not read-only, so agents that ask before running such tools
will ask you first.

| Tool                          | What it does                                                                                                                                         |
| ----------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------- |
| `update_stack_status`         | Sets a stack's `status` to `open`, `fixed`, `ignored`, or `discarded`. With `fixed`, `fixedInVersion` (such as `1.0.2`) marks it fixed in a release. |
| `snooze_stack`                | Snoozes a stack for a `duration` such as `2h`, `3d`, or `1w`, or until `snoozeUntilUtc`.                                                             |
| `set_stack_critical`          | With `critical` true, marks future occurrences of a stack critical. False stops it.                                                                  |
| `add_stack_reference_link`    | Adds a `url`, such as an issue, pull request, deployment, or incident, to a stack.                                                                   |
| `remove_stack_reference_link` | Removes a reference link from a stack.                                                                                                               |

### Saved-view tools {#saved-view-tools}

These tools work with the private views you can access and the organization's shared views.

| Tool                | Scope               | What it does                                                                                                                                                                                    |
| ------------------- | ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `list_saved_views`  | `saved-views:read`  | Lists up to 50 visible views. Takes `organizationId`, `viewType`, and `name`; `name` matches IDs, names, or URL names. Use `name` or `viewType` to narrow the list; it has no cursor arguments. |
| `create_saved_view` | `saved-views:write` | Creates a view with `name` and `viewType` (`stacks`, `events`, `sessions`, or `stream`). Pass `projectIds` or `allProjects: true`. Views are private unless `shared` is true.                   |
| `update_saved_view` | `saved-views:write` | Renames a view or changes its projects, filters, time range, or sort. Pass `savedViewId` from the list. Omitted values stay unchanged, and renaming keeps the same URL.                         |
| `delete_saved_view` | `saved-views:write` | Permanently deletes the view identified by `savedViewId`. Deleting a shared view removes it for everyone in the organization.                                                                   |

New views default to the last 7 days, except live stream views, which have no time range. They include open and
regressed stacks by default, except sessions views. Use `types`, `statuses`, `environments`, `levels`, and `tags` for
those filters, and `query` for other search expressions. Only events and sessions views accept `sort` (`date` or
`-date`).

When updating, use `clearCriteria` to remove a type, status, environment, level, tag, or query filter. A view with a
custom raw filter can't have its criteria changed separately: replace the whole filter with `query` and
`clearCriteria: ["query"]`. A view's private or shared visibility can't be changed; create a copy instead. The copy has
a new URL and doesn't keep custom columns.

Saved-view writes are marked as not read-only, and update and delete are also marked destructive. MCP clients use their
own approval settings; these calls don't use Exie's follow-up chat confirmation. See [Saved views](/docs/saved-views/).

No tool can delete events, stacks, projects, or organizations, or change project or organization settings.

## Example prompts

```prompt agent
What are my top Exceptionless errors this week?
```

```prompt agent
Find the most frequent new error in my Exceptionless project from the last 24 hours, read its latest event, and fix it.
```

```prompt agent
Show me the top 404s in my Exceptionless project over the last 7 days.
```

```prompt agent
Which Exceptionless errors did user jane@example.com hit today? Show the stack trace of the most recent one.
```

```prompt agent
Which versions has this Exceptionless stack occurred in since Monday? https://be.exceptionless.io/stack/STACK_ID
```

```prompt agent
Mark the Exceptionless stack for this bug fixed in version 2.4.1 and add this pull request as a reference link.
```

```prompt agent
Create a private Exceptionless saved view named Production Errors for this project's errors in production over the last 7 days.
```

```prompt agent
Find my Exceptionless saved view named Production Errors and change it to show the last 24 hours.
```

For step-by-step jobs, see the recipes in [Coding agents](/docs/ai/coding-agents/).

## Security {#security}

- **Event data is untrusted.** Error messages, stack traces, request data, and custom data come from your apps and the
  people who use them, so anyone who can cause an error can put text in front of your agent. Treat it as input, never as
  instructions. Prefer an agent that asks before it runs commands or edits files, and review what it does.
- **Grant only what the agent needs.** Clear **Stacks Write** unless the agent should change stacks, and **Saved Views
  Write** unless it should create, update, or delete views. Approve only the organizations it needs.
- **The API key in setup results.** `get_client_setup_instructions` returns the project's API key so the agent can
  configure a client. Anyone with the key can send events to the project, so treat it like any other configuration
  secret.
- **Review connected applications** under **My Account** > **Applications**, and revoke any you don't use.

See [AI privacy and security](/docs/ai/privacy/) for how Exie and the MCP server handle your data.
