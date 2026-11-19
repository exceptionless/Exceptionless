/** HTML for the docs components that Markdown pages and `_data/clients.json` render: tab sets, prompts, and callouts. */

export type Icons = Record<string, string>

export type Tab = {
  label: string
  /** Rendered panel content. */
  html: string
}

export function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
}

/** Opens a tab set: the container and its tablist. The caller adds the panels and closes the container. */
export function renderTabSetOpen(id: string, labels: string[], ariaLabel = "Code examples"): string {
  const buttons = labels.map((label, index) => {
    const selected = index === 0
    return `<button type="button" role="tab" id="${id}-tab-${index}" aria-controls="${id}-panel-${index}" aria-selected="${selected}"${
      selected ? "" : ' tabindex="-1"'
    } data-tab-label="${escapeHtml(label)}">${escapeHtml(label)}</button>`
  }).join("")

  return `<div class="code-window doc-tabs" data-tabs data-tab-sync><div class="tab-list" role="tablist" aria-label="${
    escapeHtml(ariaLabel)
  }">${buttons}</div>`
}

/** One tab panel. Every panel keeps a visible label for readers without JavaScript. */
export function renderTabPanel(id: string, index: number, label: string, html: string): string {
  return `<div role="tabpanel" id="${id}-panel-${index}" aria-labelledby="${id}-tab-${index}"${
    index === 0 ? "" : " hidden"
  }><p class="tab-label">${escapeHtml(label)}</p>${html}</div>`
}

/**
 * An accessible tab set. The first tab is selected in the HTML, and every panel keeps a visible label that is shown when
 * JavaScript is off, so no content is hidden from readers without scripts. site.js turns it into a tablist and keeps
 * tab sets with the same label in sync.
 */
export function renderTabSet(id: string, tabs: Tab[], ariaLabel = "Code examples"): string {
  const panels = tabs.map((tab, index) => renderTabPanel(id, index, tab.label, tab.html)).join("")
  return `${renderTabSetOpen(id, tabs.map((tab) => tab.label), ariaLabel)}${panels}</div>\n`
}

/** A code block that Lume's highlight.js pass picks up through its `language-` class. */
export function renderCodeBlock(language: string, code: string): string {
  const className = language ? ` class="language-${escapeHtml(language)}"` : ""
  return `<pre><code${className}>${escapeHtml(code.replace(/\n$/, ""))}\n</code></pre>`
}

export type PromptKind = "exie" | "agent"

const promptKinds: Record<PromptKind, { title: string; icon: string }> = {
  exie: { title: "Ask Exie", icon: "exie" },
  agent: { title: "Paste into your coding agent", icon: "terminal" },
}

export function isPromptKind(value: string): value is PromptKind {
  return Object.hasOwn(promptKinds, value)
}

export function promptKindList(): string {
  return Object.keys(promptKinds).join(", ")
}

/** A copyable prompt card. The copy button works through site.js and is hidden without JavaScript. */
export function renderPrompt(id: string, kind: PromptKind, text: string, icons: Icons): string {
  const { title, icon } = promptKinds[kind]
  return `<div class="prompt prompt-${kind}" role="group" aria-labelledby="${id}-title"><div class="prompt-header"><span class="prompt-title" id="${id}-title">${
    icons[icon] ?? ""
  }<span>${title}</span></span><button type="button" class="prompt-copy" data-copy-target="${id}-text" aria-label="Copy prompt">${
    icons.copy ?? ""
  }<span data-copy-label>Copy</span></button></div><div class="prompt-text" id="${id}-text">${
    escapeHtml(text.trim())
  }</div></div>\n`
}

export type CalloutKind = "note" | "tip" | "important" | "warning" | "caution"

export const calloutKinds: Record<CalloutKind, { label: string; icon: string }> = {
  note: { label: "Note", icon: "info" },
  tip: { label: "Tip", icon: "lightbulb" },
  important: { label: "Important", icon: "circle-alert" },
  warning: { label: "Warning", icon: "triangle-alert" },
  caution: { label: "Caution", icon: "octagon-alert" },
}

export function renderCalloutTitle(kind: CalloutKind, icons: Icons): string {
  const { label, icon } = calloutKinds[kind]
  return `<p class="callout-title">${icons[icon] ?? ""}<span>${label}</span></p>\n`
}

/** The "Medium plan and up" badge, linked to the pricing page. */
export function renderPlanBadge(plan: string): string {
  return `<p class="docs-plan-row"><a class="docs-plan" href="/pricing/">${escapeHtml(plan)} plan and up</a></p>\n`
}
