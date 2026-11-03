---
title: "AI"
metaTitle: "AI in Exceptionless: Exie and the MCP server"
description: "How Exie, the in-app assistant, and the Exceptionless MCP server for coding agents differ, and when to use each one."
navTitle: "Overview"
sources:
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - src/Exceptionless.Web/Assistant/AssistantAccessService.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Core/Billing/BillingPlans.cs
  - src/Exceptionless.Core/Services/OAuthService.cs
---

# AI in Exceptionless

Exceptionless gives AI two ways into your error data: Exie, an assistant inside the app, and an MCP server that coding
agents connect to. Both read the same stacks and events you see in the app, and both act only within your own access.

## Exie

[Exie](/docs/ai/exie/) is the assistant built into the Exceptionless app. Open it from any page, ask a question in plain
words, and it looks up stacks and events to answer: which errors are new, how often a stack happens, what an event's
stack trace and request show, and how to fix it. When you ask for a change, Exie can mark a stack fixed, snooze or
ignore it, set it critical, and add or remove reference links.

Exie is included with the Medium plan and up.

## The MCP server

The [Exceptionless MCP server](/docs/ai/mcp-server/) lets AI coding agents such as Claude Code, Codex, and GitHub
Copilot in VS Code use your Exceptionless data while they work in your code. Your agent can find your top errors, read
the event details behind one, fix the code, and, if you granted write access, mark the stack fixed in the version that
ships the fix.

You sign in with OAuth and choose which organizations and permissions the agent gets. Connections are read-only unless
you grant **Stacks Write**. The MCP server is included on every plan, including Free.

## Which one to use

| You want to                                              | Use                                                             |
| -------------------------------------------------------- | --------------------------------------------------------------- |
| Ask about an error while you look at it in the app       | Exie                                                            |
| Triage what's new or noisy without leaving the browser   | Exie                                                            |
| Fix an error in your code with an agent that can read it | The MCP server                                                  |
| Check production from your editor or terminal            | The MCP server                                                  |
| Mark a stack fixed when the fix ships                    | Either. Exie acts on request; the agent needs **Stacks Write**. |
| Add Exceptionless to a new project                       | A [coding agent](/docs/ai/coding-agents/) and the client docs   |

The main differences:

|                     | Exie                                                    | MCP server                                     |
| ------------------- | ------------------------------------------------------- | ---------------------------------------------- |
| Where it runs       | In the Exceptionless app                                | In your coding agent                           |
| Plans               | Medium and up                                           | Every plan                                     |
| Scope               | The organization you're viewing                         | The organizations you approve when you connect |
| Changes stacks      | When you ask for a specific change                      | Only with the **Stacks Write** permission      |
| Which model answers | The model Exceptionless runs Exie on                    | Your agent's own model                         |
| Tools               | A subset of the MCP tools, scoped to the page you're on | [Every MCP tool](/docs/ai/mcp-server/#tools)   |

## More

- [Coding agents](/docs/ai/coding-agents/): set up Exceptionless with an agent, an `AGENTS.md` snippet, the SDK agent
  skills, and prompts for common jobs.
- [AI privacy and security](/docs/ai/privacy/): what Exie and the MCP server can see and do, where data goes, and how to
  revoke access.
- [Docs for AI](/docs/ai/docs-for-ai/): `llms.txt` and the Markdown copy of every docs page.
- [AI features for self-hosters](/docs/self-hosting/ai/): setting up Exie and the MCP server on your own instance.
