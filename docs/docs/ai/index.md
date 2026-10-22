---
title: "AI"
metaTitle: "AI features in Exceptionless"
description: "How Exceptionless uses AI: the Exie assistant in the app, the MCP server for coding agents like Claude Code and Codex, and Markdown docs for AI tools."
---

# AI features

Exceptionless has three ways to bring AI into error monitoring. They all work from your real stacks and events, and they all respect your account's permissions.

## Exie, the in-app assistant

[Exie](/docs/ai/exie/) is an AI assistant built into the Exceptionless app. Ask it which errors affect the most users, why a stack keeps regressing, or what changed after a release. Exie searches your stacks and events, explains what it finds, and can update a stack when you explicitly ask it to.

Exie is included with Medium plans and up.

## The MCP server, for coding agents

The [Exceptionless MCP server](/docs/ai/mcp-server/) lets AI coding agents such as Claude Code, Codex, GitHub Copilot CLI, and OpenCode query Exceptionless while they work. Your agent can find your top errors, read the stack trace and request behind an event, and, with your permission, mark the stack fixed once the change ships. The MCP server is included on every plan, including Free.

## Documentation for AI tools

Every documentation page is published as Markdown so AI assistants can read it accurately:

- [`/llms.txt`](/llms.txt) lists every docs page with a short summary, following the [llms.txt](https://llmstxt.org/) convention.
- [`/llms-full.txt`](/llms-full.txt) contains the complete documentation in a single file.
- Any docs page is available as Markdown by replacing the trailing slash with `.md`, for example [`/docs/getting-started.md`](/docs/getting-started.md).
- Each docs page has **Copy page as Markdown**, **Open in ChatGPT**, and **Open in Claude** actions.

To have a coding agent set up Exceptionless in your project, point it at the client docs, for example: "Read https://exceptionless.com/docs/clients/dotnet.md and add Exceptionless to this ASP.NET Core app."
