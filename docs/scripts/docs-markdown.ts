import {
  type CalloutKind,
  calloutKinds,
  escapeHtml,
  type Icons,
  isPromptKind,
  promptKindList,
  renderCalloutTitle,
  renderPlanBadge,
  renderPrompt,
  renderTabPanel,
  renderTabSetOpen,
} from "./docs-components.ts"
import { type Client, type FeatureDefinition, renderClientTabs, renderFeatureMatrix } from "./clients.ts"

// deno-lint-ignore no-explicit-any
type MarkdownIt = any
// deno-lint-ignore no-explicit-any
type Token = any
// deno-lint-ignore no-explicit-any
type Env = Record<string, any>
type TokenConstructor = new (type: string, tag: string, nesting: number) => Token

export type DocsMarkdownOptions = {
  icons: Icons
  clients: Client[]
  features: FeatureDefinition[]
  /** Plan names from `_data/plans.json`, for the `plan` frontmatter badge. */
  plans: string[]
}

type FenceInfo = {
  /** The first word when it is a language, such as `csharp`. */
  language: string
  /** The words after the language that are not `name=value` attributes. */
  words: string[]
  attributes: Record<string, string>
}

/**
 * Adds the docs Markdown features to a markdown-it instance: unique heading ids (explicit `{#id}` suffixes come from
 * markdown-it-attrs, which Lume enables), GitHub alerts, prompt blocks, tab groups, `clients` blocks, and the plan badge.
 */
export function docsMarkdown(md: MarkdownIt, options: DocsMarkdownOptions): void {
  md.core.ruler.push("docs_heading_ids", headingIds)
  md.core.ruler.push(
    "docs_callouts",
    (state: { tokens: Token[]; Token: TokenConstructor }) => callouts(state, options.icons),
  )
  md.core.ruler.push("docs_tabs", tabGroups)
  md.core.ruler.push(
    "docs_plan_badge",
    (state: { tokens: Token[]; env: Env; Token: TokenConstructor }) => planBadge(state, options.plans),
  )

  const defaultFence = md.renderer.rules.fence
  md.renderer.rules.fence = (tokens: Token[], index: number, rendererOptions: unknown, env: Env, self: unknown) => {
    const token = tokens[index]
    const info = parseFenceInfo(token.info)

    if (info.language === "prompt") {
      const kind = info.words[0]
      if (!kind || !isPromptKind(kind)) {
        throw new Error(
          `Prompt blocks need a kind (${promptKindList()}), for example \`\`\`prompt exie. Got "${token.info}".`,
        )
      }
      return renderPrompt(nextId(env, "prompt"), kind, token.content, options.icons)
    }

    if (info.language === "clients") {
      if (token.content.trim()) {
        throw new Error("A clients block must be empty. The content comes from _data/clients.json.")
      }
      if (info.words[0] === "features") {
        return renderFeatureMatrix(options.clients, options.features)
      }
      if (info.words.length) {
        throw new Error(`Unknown clients block "${token.info}". Use \`\`\`clients or \`\`\`clients features.`)
      }
      return renderClientTabs(nextId(env, "clients"), options.clients, options.icons)
    }

    if (token.meta?.tabPanel) {
      const { label, setId, index: tabIndex } = token.meta.tabPanel
      // The language is the only part highlight.js needs; drop the tab attribute before the default renderer.
      token.info = info.language
      const code = defaultFence(tokens, index, rendererOptions, env, self).trimEnd()
      return renderTabPanel(setId, tabIndex, label, code)
    }

    return defaultFence(tokens, index, rendererOptions, env, self)
  }

  // tabs_open renders the container and tablist; each fence in the group renders its own panel.
  md.renderer.rules.tabs_open = (tokens: Token[], index: number) => {
    const { setId, labels } = tokens[index].meta
    return renderTabSetOpen(setId, labels)
  }
  md.renderer.rules.tabs_close = () => "</div>\n"
}

/** Splits a fence info string such as `csharp tab=".NET"` into a language, words, and attributes. */
export function parseFenceInfo(info: string): FenceInfo {
  const attributes: Record<string, string> = {}
  const rest: string[] = []
  const pattern = /([A-Za-z][\w-]*)=(?:"([^"]*)"|'([^']*)'|(\S+))|(\S+)/g
  for (const match of (info ?? "").matchAll(pattern)) {
    if (match[1]) {
      attributes[match[1]] = match[2] ?? match[3] ?? match[4]
    } else {
      rest.push(match[5])
    }
  }

  return { language: rest[0] ?? "", words: rest.slice(1), attributes }
}

function nextId(env: Env, prefix: string): string {
  const key = "__docsIds"
  const counts: Record<string, number> = env[key] ??= {}
  counts[prefix] = (counts[prefix] ?? 0) + 1
  return `${prefix}-${counts[prefix]}`
}

/** Assigns every heading an id: explicit ids stay, and the rest get a unique slug. */
function headingIds(state: { tokens: Token[]; env: Env }): void {
  const used = new Set<string>()
  const headings = state.tokens.filter((token) => token.type === "heading_open")
  for (const heading of headings) {
    const id = heading.attrGet("id")
    if (id) {
      used.add(id)
    }
  }

  for (const heading of headings) {
    if (heading.attrGet("id")) {
      continue
    }

    const index = state.tokens.indexOf(heading)
    const base = slugifyHeading(state.tokens[index + 1]?.content ?? "") || "section"
    let id = base
    for (let count = 1; used.has(id); count++) {
      id = `${base}-${count}`
    }

    used.add(id)
    heading.attrSet("id", id)
  }
}

export function slugifyHeading(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/\s+/g, "-")
    .replace(/\./g, "")
    .replace(/[^a-z0-9_-]/g, "")
    .replace(/^-+|-+$/g, "")
}

const calloutPattern = /^\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\][ \t]*(?:\r?\n|$)/i

/** Turns `> [!NOTE]` blockquotes into styled callouts with an icon and a label. */
function callouts(state: { tokens: Token[]; Token: TokenConstructor }, icons: Icons): void {
  const tokens = state.tokens
  for (let index = 0; index < tokens.length; index++) {
    const open = tokens[index]
    if (open.type !== "blockquote_open") {
      continue
    }

    const paragraph = tokens[index + 1]
    const inline = tokens[index + 2]
    const match = paragraph?.type === "paragraph_open" && inline?.type === "inline"
      ? inline.content.match(calloutPattern)
      : null
    if (!match) {
      continue
    }

    const kind = match[1].toLowerCase() as CalloutKind
    if (!Object.hasOwn(calloutKinds, kind)) {
      continue
    }

    // Drop the marker line from the paragraph.
    inline.content = inline.content.slice(match[0].length)
    const breakIndex = inline.children.findIndex((child: Token) =>
      child.type === "softbreak" || child.type === "hardbreak"
    )
    inline.children = breakIndex === -1 ? [] : inline.children.slice(breakIndex + 1)

    const close = findClose(tokens, index, "blockquote_close")
    open.tag = "div"
    open.attrJoin("class", `callout callout-${kind}`)
    open.attrSet("role", "note")
    if (close) {
      close.tag = "div"
    }

    const title = new state.Token("html_block", "", 0)
    title.content = renderCalloutTitle(kind, icons)
    title.block = true
    title.level = open.level + 1

    if (inline.children.length === 0) {
      tokens.splice(index + 1, 3, title)
    } else {
      tokens.splice(index + 1, 0, title)
    }
  }
}

function findClose(tokens: Token[], openIndex: number, type: string): Token | undefined {
  const level = tokens[openIndex].level
  for (let index = openIndex + 1; index < tokens.length; index++) {
    if (tokens[index].type === type && tokens[index].level === level) {
      return tokens[index]
    }
  }
}

/** Groups adjacent fenced code blocks that carry `tab="Label"` into one tab set. */
function tabGroups(state: { tokens: Token[]; env: Env; Token: TokenConstructor }): void {
  const tokens = state.tokens
  const result: Token[] = []
  let index = 0

  while (index < tokens.length) {
    const token = tokens[index]
    if (!isTabFence(token)) {
      result.push(token)
      index++
      continue
    }

    const group: Token[] = []
    while (index < tokens.length && isTabFence(tokens[index]) && tokens[index].level === token.level) {
      group.push(tokens[index])
      index++
    }

    if (group.length === 1) {
      // A single labeled block is just a code block.
      result.push(group[0])
      continue
    }

    const labels = group.map((fence) => parseFenceInfo(fence.info).attributes.tab)
    const duplicate = labels.find((label, position) => labels.indexOf(label) !== position)
    if (duplicate) {
      throw new Error(`Two code blocks in one tab group share the tab label "${duplicate}".`)
    }

    const setId = nextId(state.env, "tabs")
    const open = new state.Token("tabs_open", "div", 1)
    open.block = true
    open.level = token.level
    open.meta = { setId, labels }
    const close = new state.Token("tabs_close", "div", -1)
    close.block = true
    close.level = token.level

    group.forEach((fence, position) => {
      fence.meta = { ...fence.meta, tabPanel: { label: labels[position], setId, index: position } }
    })

    result.push(open, ...group, close)
  }

  state.tokens = result
}

function isTabFence(token: Token): boolean {
  return token.type === "fence" && Boolean(parseFenceInfo(token.info).attributes.tab)
}

/** Shows a "Medium plan and up" badge under the page title when the page declares a `plan`. */
function planBadge(
  state: { tokens: Token[]; env: Env; Token: TokenConstructor },
  plans: string[],
): void {
  const plan = state.env?.data?.plan
  if (plan === undefined) {
    return
  }

  if (typeof plan !== "string" || !plans.includes(plan)) {
    const file = state.env?.filename ?? "this page"
    throw new Error(`${file}: plan "${plan}" is not in _data/plans.json (${plans.join(", ")}).`)
  }

  const heading = state.tokens.findIndex((token) => token.type === "heading_open" && token.tag === "h1")
  if (heading === -1) {
    return
  }

  const badge = new state.Token("html_block", "", 0)
  badge.content = renderPlanBadge(plan)
  badge.block = true
  state.tokens.splice(heading + 3, 0, badge)
}
