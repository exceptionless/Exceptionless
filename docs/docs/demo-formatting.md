---
title: "Markdown Formatting Demo"
hidden: true
url: false
---

# Markdown Formatting Demo

Shows all the different markdown formatting options available for writing docs and blog posts

General markdown-it formatting: <https://markdown-it.github.io/>

- [Footnote](#footnote)
- [Anchor Links](#anchor-links)
- [Attrs {.text-center}](#attrs-text-center)
- [Bracketed Spans](#bracketed-spans)
- [Alerts](#alerts)
- [Abbreviations](#abbreviations)
- [Tables](#tables)

## Footnote

<https://github.com/markdown-it/markdown-it-footnote>

Here is a footnote reference,[^1] and another.[^longnote]

[^1]: Here is the footnote.

[^longnote]: Here's one with multiple blocks.

    Subsequent paragraphs are indented to show that they belong to the previous footnote.

## Anchor Links

<https://github.com/valeriangalliat/markdown-it-anchor>

[Link to footnote section](#footnote)

## Attrs {.text-center}

<https://github.com/arve0/markdown-it-attrs>

paragraph {.text-center}

## Bracketed Spans

<https://github.com/mb21/markdown-it-bracketed-spans>

paragraph with [a warning span]{.text-warning}

## Alerts

<https://github.com/nunof07/markdown-it-alerts>

::: danger Danger danger danger! [Link](#). :::

::: info Information information information! [Link](#). :::

::: success Success success success! [Link](#). :::

## Abbreviations

<https://github.com/markdown-it/markdown-it-abbr>

*[HTML]: Hyper Text Markup Language *[W3C]: World Wide Web Consortium The HTML specification is maintained by the W3C.

## Tables

| Feature                | Exceptionless | Application Insights | Elmah | Raygun |
| :--------------------- | :-----------: | :------------------: | :---: | :----: |
| Open Source            |       X       |                      |   X   |        |
| Free Self Hosting      |       X       |                      |   X   |        |
| Detailed error reports |       X       |          X           |       |   X    |
