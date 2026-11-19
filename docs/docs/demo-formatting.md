---
title: "Markdown formatting demo"
description: "Source-only demo of the Markdown features available in Exceptionless docs: heading ids, callouts, prompts, tabs, and client blocks."
hidden: true
url: false
---

# Markdown formatting demo

This page is not published and is not in the docs nav. It shows the syntax the docs build supports; `docs/STYLE.md` has
the rules for using it. To see it rendered, temporarily remove `hidden` and `url: false`, add the page to
`_data/docs_nav.yml`, and run `deno task serve`.

## Heading ids {#heading-ids}

An explicit `{#id}` suffix sets the heading's id and is removed from the title. Other headings get an id from their
text.

## Callouts

> [!NOTE]
> Self-hosted instances use your own API URL instead.

> [!TIP]
> Use sparingly.

> [!IMPORTANT]
> Something the reader must not miss.

> [!WARNING]
> Something that can lose data.

## Prompts

```prompt exie
Which new stacks appeared in production since yesterday's deploy?
```

```prompt agent
Find the most frequent new error in my Exceptionless project, fix it, and mark the stack fixed in the next version.
```

## Tabs

Adjacent code blocks with a `tab` label form one tab set. Choosing a label switches every tab set on the page that has
the same label.

```csharp tab=".NET"
ExceptionlessClient.Default.SubmitLog("Order placed");
```

```ts tab="JavaScript"
await Exceptionless.submitLog("Order placed")
```

## Clients

An empty `clients` block shows each client's install command and setup snippet from `_data/clients.json`.

```clients
```

A `clients features` block shows the feature matrix.

```clients features
```
