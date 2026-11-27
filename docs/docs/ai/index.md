---
title: "AI"
metaTitle: "AI in Exceptionless: Exie and the MCP server"
description: "How Exie, the in-app assistant, and the Exceptionless MCP server for coding agents differ, and when to use each one."
navTitle: "Overview"
sources:
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - src/Exceptionless.Web/Assistant/AssistantAccessService.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.SavedViews.cs
  - src/Exceptionless.Web/Assistant/AssistantConfirmation.cs
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
ignore it, set it critical, and add or remove reference links. It can also create, update, and delete saved views, with
an extra confirmation before deleting a view or changing a shared one.

Exie is included with the Medium plan and up. Eligible accounts can also use it during an active
[free trial](/docs/organizations/#free-trial), after verifying their email.

## The MCP server

The [Exceptionless MCP server](/docs/ai/mcp-server/) lets AI coding agents such as Claude Code, Codex, Cursor, and
GitHub Copilot use your Exceptionless data while they work in your code. Your agent can find your top errors, read the
event details behind one, fix the code, and, if you granted write access, mark the stack fixed in the version that ships
the fix. With write access, it can also create and manage saved views.

You sign in with OAuth and choose which organizations and permissions the agent gets. **Stacks Write** allows stack
changes; **Saved Views Write** allows creating, updating, and deleting saved views. Both start selected when the agent
requests them, so clear both for read-only access. The MCP server is included on every plan, including Free.

## Which one to use

| You want to                                              | Use                                                             |
| -------------------------------------------------------- | --------------------------------------------------------------- |
| Ask about an error while you look at it in the app       | Exie                                                            |
| Triage what's new or noisy without leaving the browser   | Exie                                                            |
| Fix an error in your code with an agent that can read it | The MCP server                                                  |
| Check production from your editor or terminal            | The MCP server                                                  |
| Mark a stack fixed when the fix ships                    | Either. Exie acts on request; the agent needs **Stacks Write**. |
| Create or manage a saved view                            | Either. The agent needs **Saved Views Write**.                  |
| Add Exceptionless to a new project                       | A [coding agent](/docs/ai/coding-agents/) and the client docs   |

The main differences:

|                     | Exie                                                    | MCP server                                                     |
| ------------------- | ------------------------------------------------------- | -------------------------------------------------------------- |
| Where it runs       | In the Exceptionless app                                | In your coding agent                                           |
| Plans               | Medium and up; eligible active trials                   | Every plan                                                     |
| Scope               | The organization you're viewing                         | The organizations you approve when you connect                 |
| Changes stacks      | Instructed to act on explicit requests                  | Only with the **Stacks Write** permission                      |
| Manages saved views | Confirms deletion and changes to shared views           | Only with **Saved Views Write**; approvals depend on the agent |
| Which model answers | The model Exceptionless runs Exie on                    | Your agent's own model                                         |
| Tools               | A subset of the MCP tools, scoped to the page you're on | [Every MCP tool](/docs/ai/mcp-server/#tools)                   |

## More

- [Coding agents](/docs/ai/coding-agents/): set up Exceptionless with an agent, an `AGENTS.md` snippet, the SDK agent
  skills, and prompts for common jobs.
- [AI privacy and security](/docs/ai/privacy/): what Exie and the MCP server can see and do, where data goes, and how to
  revoke access.
- [Docs for AI](/docs/ai/docs-for-ai/): `llms.txt` and the Markdown copy of every docs page.
- [AI features for self-hosters](/docs/self-hosting/ai/): setting up Exie and the MCP server on your own instance.
