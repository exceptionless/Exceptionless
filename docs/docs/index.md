---
title: "Overview"
metaTitle: "Exceptionless documentation"
description: "Exceptionless docs: send your first event, connect your AI agent to Exceptionless, or self-host it. Guides for .NET, JavaScript, the REST API, and more."
sources:
  - src/Exceptionless.Web/ClientApp/src/routes/(auth)/signup/+page.svelte
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/Assistant/AssistantAccessService.cs
---

# Exceptionless documentation

Exceptionless collects errors, logs, feature usage, and 404s from your apps, groups repeats of the same problem into
stacks, and shows you what to fix first. These docs cover the hosted service at
[be.exceptionless.io](https://be.exceptionless.io) and self-hosted installs.

## Start here

**Send your first event.** Create a free account, add a client to your app, and see a test error in a few minutes.
[Quickstart](/docs/getting-started/)

**Connect your AI agent.** Ask Exie, the assistant in the app, about your errors, or connect Claude Code, Codex,
Copilot, or another coding agent through the Exceptionless MCP server so it can find and fix them in your code.
[AI overview](/docs/ai/)

**Self-host.** Run Exceptionless on your own infrastructure with Docker or Kubernetes.
[Self-hosting](/docs/self-hosting/)

Coming back after a while? [What's new in 9.0](/docs/whats-new/) covers the new app, saved views, Exie, the MCP server,
and where things moved.

## Explore the docs

- [Core concepts](/docs/concepts/): events, stacks, projects, environments, and saved views in five minutes.
- [AI](/docs/ai/): [Exie](/docs/ai/exie/), the [MCP server](/docs/ai/mcp-server/), and
  [coding agents](/docs/ai/coding-agents/).
- [Send data](/docs/clients/): the [.NET](/docs/clients/dotnet/) and [JavaScript](/docs/clients/javascript/) clients,
  and [other languages](/docs/clients/custom-clients/).
- Use Exceptionless: [events](/docs/events/), [stacks](/docs/managing-stacks/),
  [filtering and searching](/docs/filtering-and-searching/), [saved views](/docs/saved-views/), and
  [notifications](/docs/notifications/).
- How it works: [stacking](/docs/concepts/stacking/), [limits and deduplication](/docs/concepts/limits/), and
  [security and privacy](/docs/security/).
- Reference: [search syntax](/docs/reference/search-syntax/), [event format](/docs/reference/event-format/), and the
  [FAQ](/docs/FAQ/).
- [REST API](/docs/api/): authentication, posting events, and querying events and stacks.
- [Self-hosting](/docs/self-hosting/): Docker, Kubernetes, configuration, and upgrading.

Every page is also available as Markdown for AI tools. See [Docs for AI](/docs/ai/docs-for-ai/).
