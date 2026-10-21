---
title: "MCP Server"
metaTitle: "Exceptionless MCP server for AI coding agents"
description: "Connect Claude Code, Codex, GitHub Copilot, or OpenCode to the Exceptionless MCP server to search errors, read stack traces, and update stacks."
order: 3
---

# MCP server

The Exceptionless MCP server lets AI coding agents work with your production errors. It implements the [Model Context Protocol](https://modelcontextprotocol.io/), so agents such as Claude Code, Codex, GitHub Copilot CLI, and OpenCode can search stacks and events, read stack traces, and, with your permission, update stacks while they fix the code.

| Setting | Value |
| --- | --- |
| Server URL | `https://be.exceptionless.io/mcp` |
| Transport | Streamable HTTP |
| Authentication | OAuth 2.1 with PKCE, in your browser |
| Plans | Every plan, including Free |

## Connect your agent

Run the command for your agent, then approve access in the browser window that opens. The **AI Tools** page in your account settings shows the same commands.

### Claude Code

```bash
claude mcp add --transport http exceptionless https://be.exceptionless.io/mcp
claude mcp login exceptionless
```

### Codex

```bash
codex mcp add exceptionless --url https://be.exceptionless.io/mcp
```

Approve access when Codex prompts you.

### GitHub Copilot CLI

```bash
copilot mcp add --transport http exceptionless https://be.exceptionless.io/mcp
copilot -i "List my Exceptionless projects"
```

Approve the OAuth browser flow when Copilot prompts you.

### OpenCode

```bash
opencode mcp add exceptionless --url https://be.exceptionless.io/mcp
opencode mcp auth exceptionless
```

### Other MCP clients

Any MCP client that supports remote streamable HTTP servers with OAuth can connect using the server URL. The server publishes standard OAuth discovery metadata at `/.well-known/oauth-protected-resource/mcp` and `/.well-known/oauth-authorization-server`, and supports dynamic client registration, so no API key or client secret is needed.

## Approving access

When your agent connects, Exceptionless asks you to approve it. You choose:

- **Organizations:** the agent can only use the organizations you select. If you later leave an organization, the agent loses access to it.
- **Permissions:** MCP access is required. Projects Read, Stacks Read, Events Read, Stacks Write, and Offline Access are optional. Leave **Stacks Write** unchecked to keep the agent read-only.

Access tokens expire after one hour. With Offline Access, the agent can refresh its token without asking you again. To disconnect an agent, open **Account > Applications** and revoke it.

## What to ask

- "What are my top issues this week?"
- "Fix my top error."
- "Show me the top 404s in my project."
- "What changed after version 1.0.2?"
- "Find recent errors for this user or order ID."
- "Mark this stack fixed in version 1.0.2."

## Tools

Read tools:

| Tool | Description |
| --- | --- |
| `list_organizations` | Lists the organizations this connection can use. |
| `list_projects` | Lists projects, with filtering and sorting. |
| `resolve_project` | Finds a project by ID or exact name. |
| `get_project` | Returns a project summary. |
| `get_project_setup` | Returns the project's client setup page and supported clients. |
| `get_client_setup_instructions` | Returns project-specific setup steps, including Expo and React Native. |
| `search_stacks` | Searches stacks, such as top or most recent issues, over a time range. |
| `get_stack` | Returns stack details. |
| `get_stack_events` | Lists recent events in a stack. |
| `search_events` | Searches events by user, correlation or order ID, custom data, and more. |
| `get_event` | Returns event details: error and stack trace, request, environment, and extended data. |
| `count_events` | Counts events over time, grouped by version, tag, user, error type, and more. |
| `get_filter_fields` | Lists the fields you can filter and sort by. |

Write tools, which need the **Stacks Write** permission:

| Tool | Description |
| --- | --- |
| `update_stack_status` | Sets a stack to open, fixed (optionally in a version), ignored, or discarded. |
| `snooze_stack` | Snoozes a stack for a duration or until a time. |
| `set_stack_critical` | Marks future occurrences critical, or clears the flag. |
| `add_stack_reference_link` | Attaches an issue, pull request, deployment, or incident URL to a stack. |
| `remove_stack_reference_link` | Removes a reference link from a stack. |

Write tools are annotated as non-read-only, so MCP clients that support tool approval can ask you before running them. No tool can delete events, projects, or organizations.

Search tools use the same filter syntax as the app. See [filtering and searching](/docs/filtering-and-searching/).

## Self-hosted Exceptionless

The MCP server is part of the Exceptionless web app. On a self-hosted install it is available at `/mcp` on your app's URL, for example `https://exceptionless.example.com/mcp`. Make sure the app's base URL setting matches the public URL so OAuth redirects and discovery metadata point to the right host.

## Using the in-app assistant instead

If you want to ask questions from the Exceptionless app instead of a coding agent, use [Exie](/docs/ai/exie/).
