---
title: "Docs for AI"
description: "Give an AI assistant or coding agent the Exceptionless docs: llms.txt, the full-text files, Markdown copies of every page, and page actions."
sources:
  - docs/_includes/layouts/docs.vto
  - docs/scripts/postbuild.ts
---

# Docs for AI

The Exceptionless docs are published in forms that AI assistants and coding agents can read directly. Point your agent
at one of these instead of letting it guess from older content.

## Files

| URL                                                           | Contents                                                                                                                                                                                  |
| ------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [`https://exceptionless.com/llms.txt`](/llms.txt)             | An index in the [llms.txt](https://llmstxt.org/) format: a short summary of Exceptionless, then every docs page by section, each linked to its Markdown copy with a one-line description. |
| [`https://exceptionless.com/llms-small.txt`](/llms-small.txt) | The full text of the Get started and AI pages and the client overviews, for agents with a small context window.                                                                           |
| [`https://exceptionless.com/llms-full.txt`](/llms-full.txt)   | The full text of every docs page in one file.                                                                                                                                             |

## Markdown copy of every page

Every docs page is also published as Markdown. Replace the trailing slash in the page's URL with `.md`:

| Page                                              | Markdown                                            |
| ------------------------------------------------- | --------------------------------------------------- |
| `https://exceptionless.com/docs/getting-started/` | `https://exceptionless.com/docs/getting-started.md` |
| `https://exceptionless.com/docs/ai/mcp-server/`   | `https://exceptionless.com/docs/ai/mcp-server.md`   |
| `https://exceptionless.com/docs/`                 | `https://exceptionless.com/docs.md`                 |

Each Markdown copy starts with the page title and description, uses absolute links, and ends with the page's URL and a
link to `llms.txt`.

## Page actions

Each docs page has these actions in the sidebar beside the page, above **On this page**:

- **Copy page as Markdown** copies the page's Markdown to your clipboard, to paste into any assistant.
- **View as Markdown** opens the Markdown copy.
- **Open in ChatGPT** and **Open in Claude** start a new chat that asks the assistant to read the page's Markdown copy
  so it can answer your questions about it.

## Use the docs with an agent

Give your agent the Markdown URL of the page you're working from:

```prompt agent
Read https://exceptionless.com/docs/clients/javascript.md and add Exceptionless to this app.
```

To look up your own errors and stacks, rather than the docs, connect your agent to the
[MCP server](/docs/ai/mcp-server/).
