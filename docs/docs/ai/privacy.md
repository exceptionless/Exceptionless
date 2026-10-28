---
title: "AI privacy and security"
description: "What Exie and the MCP server can see and change, what is sent to an AI model, how chats are stored, and how to limit or revoke access."
navTitle: "Privacy and security"
sources:
  - src/Exceptionless.Web/Assistant/AssistantService.cs
  - src/Exceptionless.Web/Assistant/AssistantSystemPrompt.cs
  - src/Exceptionless.Web/Assistant/AssistantToolContext.cs
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - src/Exceptionless.Web/Assistant/AssistantLimits.cs
  - src/Exceptionless.Web/Assistant/AssistantConversationService.cs
  - src/Exceptionless.Web/Assistant/AssistantConversationSharingService.cs
  - src/Exceptionless.Web/Assistant/AssistantUsageService.cs
  - src/Exceptionless.Core/Configuration/AssistantOptions.cs
  - src/Exceptionless.Web/appsettings.yml
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/conversation-history.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/assistant-telemetry.ts
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Core/Services/OAuthService.cs
---

# AI privacy and security

Exie and the MCP server give AI access to your error data. This page explains what each one can see and change, where
your data goes, and how to limit or remove that access.

## What Exie can see and do

Exie acts as you, inside the organization you're viewing:

- It sees only what your account can see in that organization. It can't reach other organizations, even ones you belong
  to.
- It can read projects, stacks, and events, including stack traces, request details, and custom data.
- It can change stacks in five ways: status, snooze, critical, and adding or removing reference links. These are things
  you can already do in the app. It can't delete data or change settings.
- It changes a stack only when your message asks for that change, and every change appears in **Exie's steps**.

See [Exie](/docs/ai/exie/) for the full list of tools.

## What the MCP server can see and do

A coding agent connected to the MCP server acts as you, limited to what you approved when it connected:

- **Organizations:** only the ones you selected, and only while you're still a member.
- **Permissions:** the scopes you approved. Without **Stacks Write**, the agent can only read.
- It can read projects, stacks, and events, and with **Projects Read** it can read a React Native or Expo project's API
  key through `get_client_setup_instructions`.
- With **Stacks Write**, it can make the same five kinds of stack changes as Exie. No tool deletes data or changes
  settings.

See [MCP server](/docs/ai/mcp-server/#approve-access) for each permission.

## Where your data goes

### Exie

To answer, Exceptionless sends an AI model:

- Exie's instructions, with the IDs of the organization, project, stack, and event you're viewing, the page path, and
  your time zone.
- Up to the last 20 messages of the chat, at most 48,000 characters.
- The results of the tools Exie calls for this answer and recent results from earlier in the chat, at most 48,000
  characters. One event's details are cut to about 16 KB.

On the hosted service, Exie sends these requests to [OpenRouter](https://openrouter.ai/), which passes them to the model
Exceptionless has selected for Exie. On a self-hosted instance, the administrator chooses the provider and model. See
[AI features](/docs/self-hosting/ai/).

### The MCP server

The MCP server doesn't run a model. It returns tool results to your agent, and your agent sends them to its own model
provider under that provider's terms. Exceptionless doesn't see your agent's prompts or your code.

## Exie conversations {#exie-conversations}

- **In your browser.** Your chats are saved in your browser's local storage: up to 20 per organization, for 30 days.
  Large tool results are trimmed before they're saved. Delete a chat from **Recent chats**. Signing out removes all of
  them from that browser.
- **On the server.** Exceptionless keeps the tool results from a chat for 30 minutes so follow-up questions don't repeat
  the same lookups. They're stored for your user, organization, and chat only, and are dropped early if a project they
  came from is deleted. The model's internal reasoning isn't stored or sent to your browser.
- **Usage records.** Exceptionless counts each organization's Exie responses, tokens, and cost per month to enforce plan
  limits. These records don't include message content.

### Chat sharing

**Chat sharing** at the bottom of the Exie panel controls whether Exceptionless can review your conversations to improve
Exie. When it's on, your browser sends your messages and Exie's replies to Exceptionless. When it's off, it doesn't. The
popover shows the default and whether you've changed it, and your choice follows your account to other devices.

Usage and error diagnostics are sent either way: for example, how long a response took, how many tools it called, and
whether it failed. They don't include your messages or Exie's replies.

## Prompt injection {#prompt-injection}

Event data comes from your apps and the people who use them. Anyone who can cause an error can choose its message, and
request data and custom data can contain anything. An AI model that reads that text could be tricked into treating it as
instructions.

How Exie limits this:

- Its instructions treat tool results and event text as untrusted data and tell it never to follow instructions found
  inside them.
- It changes data only when your own message asks for that change, never because of something it read.
- It has no way to run code, browse the web, send messages, or reach other organizations, so the most it can do is one
  of the five stack changes, which appear in **Exie's steps** and can be reversed.

How to limit it with the MCP server:

- The server returns event data as it was received. Your agent's model has to treat it as untrusted.
- Connect with read-only access unless the agent needs to change stacks. Write tools are marked as not read-only, so
  agents that ask before running such tools will ask you.
- Coding agents can edit files and run commands, so an agent that reads event data can be steered toward your code. Keep
  your agent's own approval settings on, and review its changes.
- Add the instructions in [Coding agents](/docs/ai/coding-agents/#add-instructions-to-your-repository) to your
  repository so agents know event data is untrusted.

## Read-only access

An MCP connection is read-only unless you grant **Stacks Write**. A client that doesn't ask for specific permissions
gets read-only access, and you can clear **Stacks Write** on the consent screen when a client asks for it.

Exie has no read-only mode. Anyone who can use Exie in an organization can ask it to change stacks there, just as they
can in the app.

## Revoke access

- **Coding agents:** **My Account** > **Applications**, then **Revoke Access** on the application. The agent has to sign
  in again before it can use your account. See [MCP server](/docs/ai/mcp-server/#connected-applications).
- **Leaving an organization** removes every connection's access to it right away.
- **Exie chats:** delete them from **Recent chats**, or sign out to remove them from the browser.

## Turn AI off

On the hosted service, Exie runs only when someone in the organization opens it and asks a question, and the MCP server
is used only by agents someone connects and approves. There's no organization or project setting to turn them off. To
stop an agent, revoke its access.

On a self-hosted instance, the administrator can turn Exie off for everyone. See [AI features](/docs/self-hosting/ai/).

For how Exceptionless handles your data in general, see [Security and privacy](/docs/security/).
