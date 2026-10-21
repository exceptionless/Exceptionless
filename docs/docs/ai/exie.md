---
title: "Exie AI Assistant"
metaTitle: "Exie, the Exceptionless AI assistant"
description: "Use Exie, the AI assistant in Exceptionless, to triage errors, find root causes, and update stacks on request. Covers plans, limits, privacy, and self-hosting."
order: 2
---

# Exie AI assistant

Exie is the AI assistant built into Exceptionless, your debugging sidekick for production errors. Ask it questions in plain English and it investigates your stacks and events to answer, using the same data you see in the app. It shows its work, tells you when the data can't answer something, and only changes things when you ask.

## Opening Exie

- Click **Ask Exie** in the top navigation to open the assistant panel from any page. You can expand the panel to a full page at `/exie`.
- Click **Fix** on a stack or event to have Exie analyze that error and suggest how to fix the underlying issue.
- Open the command palette and choose **Ask Exie**, **Triage Recent Errors**, or **Analyze Error Trends**.

Exie uses the organization, project, stack, or event you are viewing as context, so "Why is this happening?" on a stack page is enough.

## What you can ask

- "What are my top issues this week?"
- "Which errors affected the most users in the last 24 hours?"
- "What changed after version 1.0.2?"
- "Why does this stack keep regressing?"
- "Mark this stack fixed in version 1.0.2."

Exie states the time range it used, links to the stacks it mentions, and suggests follow-up questions you can click.

## What Exie can do

Exie can search stacks, list a stack's recent events, read event details such as the stack trace, request, and environment, list projects, and look up client setup information.

When you explicitly ask for a change, Exie can also:

- change a stack's status to open, fixed (optionally in a specific version), ignored, or discarded
- snooze a stack
- mark future occurrences of a stack as critical, or clear that flag
- add or remove a reference link, such as an issue or pull request URL

Exie is instructed never to change data in response to a request to inspect, investigate, or explain something, and to report what changed after every action. Each tool call is shown in the conversation so you can see what it did. AI can make mistakes, so check important changes.

Exie works only inside the current organization and only sees data your account can access. Event messages and payloads are treated as untrusted data, so instructions embedded in an error message are not followed.

## Plans and limits

Exie is included with Medium, Large, Extra Large, and Enterprise plans. Each plan includes a monthly AI usage allowance and limits on how many conversations can run at once. If your organization reaches its limit, Exie tells you and becomes available again when the limit resets. See [pricing](/pricing/) for plan details.

## Privacy

To answer a question, Exie sends the AI model your question, recent messages in the conversation, the page you are on, and the stack and event details it looks up. Event details are truncated to a fixed size before they are sent.

You can choose whether to share your conversations to help improve Exie from the Exie panel. Usage and error diagnostics are collected either way.

## Self-hosting

Exie is available in self-hosted Exceptionless. It uses an OpenAI-compatible chat completions API and defaults to [OpenRouter](https://openrouter.ai/). Set these environment variables on the Exceptionless app:

| Variable | Description |
| --- | --- |
| `EX_Assistant__ApiKey` | API key for your model provider. Exie is enabled when a key is set. |
| `EX_Assistant__Model` | Optional model name to use instead of the default. |
| `EX_Assistant__Endpoint` | Optional chat completions endpoint for a different provider. |

System administrators can also change the model and turn Exie on or off from the admin settings without restarting.

## Using AI coding agents

To work with Exceptionless data from Claude Code, Codex, GitHub Copilot, or OpenCode instead of the web app, connect the [MCP server](/docs/ai/mcp-server/).
