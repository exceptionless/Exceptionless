# Docs style guide

How the Exceptionless docs are written. Every writer, person or agent, follows it.

## Voice

- Second person, present tense, plain words, short paragraphs. Write for a developer in a hurry and for an agent reading
  the Markdown copy of the page.
- Neutral and direct. No marketing language ("powerful", "seamless", "simply"), no jokes, no emoji.
- Exie's friendly voice belongs only inside Exie's speech bubbles and prompt examples.

## Page shape

- One page, one type: a tutorial, a how-to guide, reference, or an explanation.
- Start with one or two sentences on what the page lets you do. Then the steps or the facts.
- No history and no release notes. "What changed in 9.0" goes on What's new, version history on the upgrading pages.
- Task pages end with an `## Ask Exie` section (see [Prompts](#prompts)).
- Don't write "Next" or "Previous" links. The layout generates them from the nav.

## Titles and headings

- The `title` frontmatter describes the page in a few words ("Send your first event", "ASP.NET Core"). The page body
  starts with a matching `# Heading`.
- Headings use sentence case: "Mark a stack fixed", not "Mark A Stack Fixed".
- To keep an anchor that other pages or the app link to, give the heading an explicit id:
  `## Client configuration
  {#client-configuration}`.

## Frontmatter

```yaml
---
title: "Saved views"
description: "One or two sentences, at most 160 characters, used for search results and social cards."
navTitle: "Saved views" # optional shorter sidebar label
plan: "Medium" # optional; renders a "Medium plan and up" badge. Use a plan name from _data/plans.json.
redirectFrom: # optional; old URLs that now point at this page
  - /docs/manual-stacking/
  - from: /docs/bulk-actions/
    anchor: bulk-actions # optional; the redirect lands on this heading
sources: # code the page is checked against; not rendered
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExtensions.cs
  - Exceptionless.JavaScript:packages/core/src/configuration/Configuration.ts
---
```

`sources` paths are relative to this repository unless prefixed with `Exceptionless.Net:` or
`Exceptionless.JavaScript:`.

## Code

- Complete and runnable, with imports and package installs.
- Use the `YOUR_API_KEY` placeholder for API keys.
- Hosted URLs: the API is `https://api.exceptionless.io`, and the web app is `https://be.exceptionless.io`. Never
  `collector.exceptionless.io`. Self-hosted overrides go in a note.
- Modern setup first: ASP.NET Core minimal hosting with `appsettings.json`, ES modules. .NET Framework and `web.config`
  belong on the legacy ASP.NET page.
- Only document released APIs: Exceptionless.Net 6.2.0 and Exceptionless.JavaScript 3.2.1.
- Show the same thing in several languages with tabs: adjacent fenced code blocks that each have a `tab` label.

  ````markdown
  ```csharp tab=".NET"
  ExceptionlessClient.Default.SubmitLog("Order placed");
  ```

  ```ts tab="JavaScript"
  await Exceptionless.submitLog("Order placed")
  ```

  ```bash tab="curl"
  curl -X POST https://api.exceptionless.io/api/v2/events ...
  ```
  ````

- To show the install and setup snippet for every client from `_data/clients.json`, use an empty `clients` block:

  ````markdown
  ```clients
  ```
  ````

## Callouts

Use GitHub alert syntax, and use it sparingly:

```markdown
> [!NOTE]
> Self-hosted instances use your own API URL instead.
```

`[!NOTE]`, `[!TIP]`, `[!IMPORTANT]`, and `[!WARNING]` are supported.

## Prompts

Copyable prompts use a `prompt` fenced block. The second word says who the prompt is for:

- `prompt exie`: something to ask Exie in the app.
- `prompt agent`: something to paste into a coding agent, such as Claude Code, Codex, or Copilot.

````markdown
## Ask Exie

```prompt exie
Which new stacks appeared in production since yesterday's deploy?
```

```prompt exie
Snooze the stacks from the payments worker for a day.
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most frequent new error in my Exceptionless project, fix it, and mark the stack fixed in the next version.
```
````

Prompts must be things Exie and the MCP tools can actually do today. Check the tool list in the code before writing one.

## UI names and keys

- UI names match the app exactly and are bold: **Stacks**, **Mark Fixed**, **Project Settings**.
- Keyboard keys use `<kbd>`: <kbd>Ctrl</kbd>+<kbd>K</kbd>.

## Links

- Link to docs pages with site paths and a trailing slash: `/docs/saved-views/`.
- Link to a heading only if it has a stable id.
- Link to code on GitHub at the default branch and a file path, never a line number.

## Screenshots

- Use the existing light and dark pairs: `![Alt text](/assets/img/docs/name-light.webp)`. The dark image is picked up
  automatically.
- Only add a screenshot when the UI is hard to describe. Never leave a screenshot that shows the old UI.

## Page templates

**Client app-type page** (for example `/docs/clients/dotnet/aspnet-core/`):

1. What it captures, in one or two sentences, and the requirements.
2. Install.
3. Configure.
4. Verify: throw a test error and find it in the app.
5. What is captured automatically.
6. Next steps.
7. Ask Exie.

**Feature how-to** (for example `/docs/saved-views/`):

1. What the feature does and when to use it.
2. One section per task, named for the task.
3. Self-hosting notes, if behavior differs.
4. Ask Exie.

**Reference** (for example `/docs/reference/search-syntax/`): a short introduction, then tables or definition lists. No
"Ask Exie" section.

**Explanation** (for example `/docs/concepts/stacking/`): the idea, how it works, what you can change, and links to the
how-to pages.
