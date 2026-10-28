---
title: "Exie"
metaTitle: "Exie, the Exceptionless AI assistant"
description: "Ask Exie about your errors in the Exceptionless app: open it, what it can answer, which stack changes it makes, and its plan limits."
plan: "Medium"
sources:
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - src/Exceptionless.Web/Assistant/AssistantSystemPrompt.cs
  - src/Exceptionless.Web/Assistant/AssistantLimits.cs
  - src/Exceptionless.Web/Assistant/AssistantService.cs
  - src/Exceptionless.Web/Assistant/AssistantAccessService.cs
  - src/Exceptionless.Web/Assistant/AssistantUsageService.cs
  - src/Exceptionless.Web/Assistant/AssistantConversationService.cs
  - src/Exceptionless.Web/Api/Endpoints/AssistantEndpoints.cs
  - src/Exceptionless.Core/Billing/BillingPlans.cs
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-panel.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-composer.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-message-actions.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-history-menu.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-fix-button.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-project-scope.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/assistant-starters.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/conversation-history.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/keyboard-shortcuts.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/navigation-command.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/navbar.svelte
---

# Exie

Exie is the AI assistant in the Exceptionless app. Ask it about your errors in plain words. It looks up your stacks and
events to answer, shows each step it takes, and changes a stack only when you ask it to.

Exie works in the organization you're viewing and sees only data your account can access. It's included with the Medium
plan and up. On a smaller plan, opening Exie shows **Upgrade Plan** instead of a chat.

## Open Exie

- **Ask Exie** in the top bar opens the side panel from any page. Press <kbd>A</kbd> to open or close it, as long as you
  aren't typing in a field.
- **Expand to full page** in the panel header opens Exie at `/exie` with the same conversation and context. **Collapse
  to side panel** takes you back to the page you came from.
- **Fix** on a stack, or on the stack summary in an event's details, opens Exie and asks it to find the likely root
  cause and give you prioritized next steps.
- The command palette (<kbd>/</kbd>) has an **Exie** group: **Ask Exie** opens the panel, **Triage Recent Errors**
  summarizes the most important recent errors, and **Analyze Error Trends** looks for spikes and regressions over the
  last 7 days.

See [Navigating the app](/docs/navigation/) for the other shortcuts and the **Meet Exie** guided tour.

## Ask a question

Type in the message box and press <kbd>Enter</kbd> to send. <kbd>Shift</kbd>+<kbd>Enter</kbd> adds a new line. An empty
chat shows three suggested questions that fit the page you're on, such as **Explain this error** on an event or **Top
errors in this project** on a project.

Exie uses the page you're on as context:

- **On an event or stack**, the message box says "Ask about this event…" or "Ask about this stack…", so "Why is this
  happening?" is enough. A chip above the message box names the event or stack. Close it to ask a general question, and
  select **Use this event as context** (or stack) to bring it back.
- **Project scope.** The selector in the message box sets which project Exie searches: one project, or **All projects**.
  It starts on the project you're viewing.
- **Time.** Exie uses your browser's time zone for "today" and "this week". If you don't give a time range, it uses the
  last 7 days and says so.

Answers link to the stacks, events, and projects they mention. When there's a useful next step, Exie adds up to three
suggested follow-ups that you can select instead of typing.

## What Exie can look up

Exie answers from your live data with these tools:

| Tool                | What Exie uses it for                                                                                     |
| ------------------- | --------------------------------------------------------------------------------------------------------- |
| `count_events`      | Ranking the most frequent or new errors in a period, and counting by version, environment, user, and more |
| `search_stacks`     | Finding stacks by status, tag, title, or type                                                             |
| `get_stack`         | A stack's status, totals, first and last occurrence, tags, and reference links                            |
| `get_stack_events`  | A stack's recent occurrences                                                                              |
| `get_event`         | An event's error and stack trace, request, environment, and custom data                                   |
| `list_projects`     | The projects in the organization                                                                          |
| `get_project_setup` | The project's **Client Setup** page and which clients are supported                                       |

Every tool call appears as a step above the answer, with the project, filter, and time range it used. Expand a step to
see its request and response.

Exie can't see data outside the current organization, delete anything, change project or organization settings, or read
your source code. To work with your code, connect a coding agent to the [MCP server](/docs/ai/mcp-server/).

## What Exie can change

When you ask, Exie can:

| Tool                          | Change                                                                                       |
| ----------------------------- | -------------------------------------------------------------------------------------------- |
| `update_stack_status`         | Set a stack to open, fixed (optionally in a version, such as `2.4.1`), ignored, or discarded |
| `snooze_stack`                | Snooze a stack for a duration, such as `3d`, or until a time                                 |
| `set_stack_critical`          | Mark future occurrences of a stack critical, or stop marking them                            |
| `add_stack_reference_link`    | Add a link to an issue, pull request, deployment, or incident                                |
| `remove_stack_reference_link` | Remove a reference link                                                                      |

Exie doesn't show a confirmation dialog. Instead, it changes a stack only when your message asks for that exact change.
Asking it to look at, investigate, or explain something never changes data. After a change, Exie says what changed, or
that nothing needed to change, and the change always appears as a step above the answer.

Exie can make mistakes, so check important changes. You can reverse any of them on the stack in the app. See
[Stacks](/docs/managing-stacks/).

## Chat history

- **Recent chats** in the panel header lists your last 20 chats in this organization. Select one to continue it, or
  delete it with its trash icon.
- **New chat** starts over.
- Chats are saved in your browser, not on the Exceptionless servers. They're kept for 30 days, are separate for each
  organization, and are removed when you sign out. They don't follow you to another browser.

When you continue a chat, Exie reuses the results it looked up in the last 30 minutes. After that, it looks them up
again. See [AI privacy and security](/docs/ai/privacy/#exie-conversations) for what is stored where.

## Messages and feedback

Hover over a message to see its actions:

- **Copy message**.
- **Edit and resend** on your own messages.
- **Regenerate response** on Exie's answers.
- **Good response** and **Poor response**. **Poor response** asks what went wrong: **Incorrect**, **Incomplete**, **Not
  relevant**, **Wrong change**, or **Something else**.

While Exie is answering, the send button becomes **Stop generating**.

## Chat sharing

**Chat sharing** at the bottom of the panel shows whether you share your conversations to help improve Exie. When it's
on, Exceptionless can review your messages and Exie's replies. Turn it on or off with **Share conversations to improve
Exie**. The setting follows your account to other devices, and **Use default** returns to the default.

Usage and error diagnostics, such as how long a response took and whether a tool failed, are collected either way.

## Limits

Each organization's plan sets how much it can use Exie:

| Plan        | Responses at a time | Responses per minute |
| ----------- | ------------------- | -------------------- |
| Medium      | 2                   | 10                   |
| Large       | 3                   | 15                   |
| Extra Large | 5                   | 25                   |
| Enterprise  | 10                  | 50                   |

Yearly plans have the same limits as monthly ones. Each plan also includes a monthly AI usage allowance, which resets on
the first day of each month (UTC). Limits are shared by everyone in the organization.

When the organization reaches a limit, Exie says which one and when it resets:

- **Too many at once** or **too quickly:** wait a few seconds and send again.
- **Monthly allowance:** Exie is unavailable for the organization until the first of next month. If the allowance runs
  out during an answer, Exie stops that answer.

Each answer also has limits that keep it fast:

- Exie makes up to 12 tool calls in up to 3 rounds, and searches at most 5 projects. When an organization has more
  projects, it says which ones it searched and suggests searching the rest. Pick a project in the scope selector to
  search just that one.
- A tool call returns at most 10 items, and one event's details are cut to about 16 KB.
- An answer that takes longer than 2 minutes stops with what Exie has so far.
- Exie reads the last 20 messages of a long chat. If a chat gets too long for one answer, start a **New chat**.

## Self-hosting

Exie needs an AI provider API key on a self-hosted instance. Organizations created there without billing are on the
Unlimited plan, which has higher limits than the plans above. See [AI features](/docs/self-hosting/ai/).

## Ask Exie

```prompt exie
What are the top errors in this project over the last 24 hours?
```

```prompt exie
Which new stacks appeared in production since yesterday?
```

```prompt exie
How often did this stack happen this week, and which versions is it in?
```

```prompt exie
Which errors did jane@example.com run into in the last 7 days?
```

```prompt exie
Mark this stack fixed in version 2.4.1 and add https://github.com/acme/shop/pull/412 as a reference link.
```

```prompt exie
Snooze this stack for 3 days.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same from your editor and fix the
code too:

```prompt agent
Find the most frequent new error in my Exceptionless project from the last 24 hours, read its latest event, and fix the cause in this repository.
```
