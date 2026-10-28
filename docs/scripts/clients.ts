import { escapeHtml, type Icons, renderCodeBlock, renderTabSet, type Tab } from "./docs-components.ts"

/** The client data in `_data/clients.json` and `_data/client_features.json`. */

export type ClientSnippet = {
  label: string
  package: string
  install: string
  language: string
  code: string
  caption: string
  docsUrl: string
}

export type ClientPackage = {
  name: string
  registry: string
  url: string
  description: string
  minimumRuntime: string
}

export type FeatureStatus = "yes" | "opt-in" | "partial" | "no" | "n/a"

export type ClientFeature = { status: FeatureStatus; note?: string }

export type Client = {
  id: string
  language: string
  name: string
  support: "Official" | "Community" | "Preview"
  repo: string
  docsUrl: string
  version: string
  minimumRuntime: string
  summary: string
  packages: ClientPackage[]
  snippets: ClientSnippet[]
  features: Record<string, ClientFeature>
}

export type FeatureDefinition = { id: string; label: string; description: string }

export const featureStatusLabels: Record<FeatureStatus, string> = {
  "yes": "Yes",
  "opt-in": "Opt-in",
  "partial": "Partial",
  "no": "No",
  "n/a": "Not applicable",
}

/** Checks the shape of the client data so a typo fails the build instead of rendering a broken page. */
export function validateClients(clients: Client[], features: FeatureDefinition[]): void {
  const ids = new Set<string>()
  for (const client of clients) {
    if (ids.has(client.id)) {
      throw new Error(`clients.json: duplicate client id "${client.id}".`)
    }
    ids.add(client.id)

    for (const key of ["language", "name", "support", "repo", "docsUrl", "version", "minimumRuntime"] as const) {
      if (!client[key]) {
        throw new Error(`clients.json: client "${client.id}" is missing "${key}".`)
      }
    }

    if (!["Official", "Community", "Preview"].includes(client.support)) {
      throw new Error(`clients.json: client "${client.id}" has unknown support level "${client.support}".`)
    }

    for (const snippet of client.snippets) {
      for (const key of ["label", "package", "install", "language", "code", "caption", "docsUrl"] as const) {
        if (!snippet[key]) {
          throw new Error(`clients.json: a "${client.id}" snippet is missing "${key}".`)
        }
      }
    }

    for (const feature of features) {
      const value = client.features[feature.id]
      if (!value || !Object.hasOwn(featureStatusLabels, value.status)) {
        throw new Error(`clients.json: client "${client.id}" needs a valid status for feature "${feature.id}".`)
      }
    }

    for (const key of Object.keys(client.features)) {
      if (!features.some((feature) => feature.id === key)) {
        throw new Error(`clients.json: client "${client.id}" has an unknown feature "${key}".`)
      }
    }
  }
}

type SnippetEntry = { client: Client; snippet: ClientSnippet }

function allSnippets(clients: Client[]): SnippetEntry[] {
  return clients.flatMap((client) => client.snippets.map((snippet) => ({ client, snippet })))
}

/**
 * Tabs with each client's install command and a minimal setup snippet, for a `clients` block in a docs page. The
 * install command is its own copyable block above the code.
 */
export function renderClientTabs(id: string, clients: Client[], icons: Icons): string {
  const tabs: Tab[] = allSnippets(clients).map(({ snippet }) => ({
    label: snippet.label,
    html: `<div class="client-snippet">${renderCodeBlock("bash", snippet.install)}${
      renderCodeBlock(snippet.language, snippet.code)
    }<p class="code-caption">${icons["circle-check"] ?? ""}${escapeHtml(snippet.caption)} <a href="${
      escapeHtml(snippet.docsUrl)
    }">Read the guide</a>.</p></div>`,
  }))

  return renderTabSet(id, tabs, "Client setup examples")
}

/**
 * The marketing home page's setup tabs. The look matches the docs tabs, but the install command is a comment on the
 * first line of the snippet, as on the original page.
 */
export function renderHomeClientTabs(clients: Client[], icons: Icons, ariaLabel = "Client setup examples"): string {
  const entries = allSnippets(clients)
  const buttons = entries.map(({ snippet }, index) => {
    const selected = index === 0
    return `<button type="button" role="tab" id="${tabId(snippet)}" aria-controls="${
      panelId(snippet)
    }" aria-selected="${selected}"${selected ? "" : ' tabindex="-1"'}>${escapeHtml(snippet.label)}</button>`
  }).join("\n        ")

  const panels = entries.map(({ snippet }, index) => {
    const code = `${installComment(snippet.install, snippet.language)}\n${snippet.code}`
    return `      <div role="tabpanel" id="${panelId(snippet)}" aria-labelledby="${tabId(snippet)}"${
      index === 0 ? "" : " hidden"
    }>\n${renderCodeBlock(snippet.language, code)}\n        <p class="code-caption">${icons["circle-check"] ?? ""} ${
      escapeHtml(snippet.caption)
    }</p>\n      </div>`
  }).join("\n")

  return `<div class="code-window code-window-dark reveal" data-tabs>
      <div class="tab-list" role="tablist" aria-label="${escapeHtml(ariaLabel)}">
        ${buttons}
      </div>
${panels}
    </div>`
}

function tabId(snippet: ClientSnippet): string {
  return `tab-${slug(snippet.label)}`
}

function panelId(snippet: ClientSnippet): string {
  return `panel-${slug(snippet.label)}`
}

function slug(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "")
}

/** Wraps a long install command in a comment, breaking at spaces with a trailing backslash like a shell would. */
export function installComment(install: string, language: string, maxLength = 62): string {
  const prefix = ["python", "ruby", "bash", "sh", "yaml"].includes(language) ? "#" : "//"
  const words = install.split(/\s+/)
  const lines: string[] = []
  let line = ""
  for (const word of words) {
    if (line && `${line} ${word}`.length > maxLength) {
      lines.push(`${line} \\`)
      line = word
    } else {
      line = line ? `${line} ${word}` : word
    }
  }
  lines.push(line)

  return lines.map((text, index) => `${prefix} ${index ? "  " : ""}${text}`).join("\n")
}

/** The feature matrix as an HTML table. */
export function renderFeatureMatrix(clients: Client[], features: FeatureDefinition[]): string {
  const head = clients.map((client) =>
    `<th scope="col"><a href="${escapeHtml(client.docsUrl)}">${escapeHtml(client.language)}</a><br><small>${
      escapeHtml(client.name)
    } ${escapeHtml(client.version)}</small></th>`
  ).join("")

  const rows = features.map((feature) => {
    const cells = clients.map((client) => {
      const value = client.features[feature.id]
      const note = value.note ? `<small>${escapeHtml(value.note)}</small>` : ""
      return `<td><span class="feature-status feature-${value.status.replace("/", "")}">${
        featureStatusLabels[value.status]
      }</span>${note}</td>`
    }).join("")
    return `<tr><th scope="row">${escapeHtml(feature.label)}</th>${cells}</tr>`
  }).join("")

  return `<table class="feature-matrix"><thead><tr><th scope="col">Feature</th>${head}</tr></thead><tbody>${rows}</tbody></table>\n`
}

/** Markdown for a `clients` block, used by the page's `.md` copy and llms-full.txt. */
export function clientTabsMarkdown(clients: Client[]): string {
  return allSnippets(clients).map(({ client, snippet }) =>
    [
      `**${snippet.label}** (${
        snippet.label === client.language ? "" : `${client.language}, `
      }package \`${snippet.package}\`)`,
      "```bash\n" + snippet.install + "\n```",
      "```" + snippet.language + "\n" + snippet.code + "\n```",
      snippet.caption,
    ].join("\n\n")
  ).join("\n\n")
}

/** Markdown for a `clients features` block. */
export function featureMatrixMarkdown(clients: Client[], features: FeatureDefinition[]): string {
  const header = `| Feature | ${clients.map((client) => `${client.language} ${client.version}`).join(" | ")} |`
  const divider = `| --- | ${clients.map(() => "---").join(" | ")} |`
  const rows = features.map((feature) => {
    const cells = clients.map((client) => {
      const value = client.features[feature.id]
      const note = value.note ? ` (${value.note.replace(/\.$/, "")})` : ""
      return `${featureStatusLabels[value.status]}${note}`.replaceAll("|", "\\|")
    })
    return `| ${feature.label} | ${cells.join(" | ")} |`
  })

  return [header, divider, ...rows].join("\n")
}

/**
 * Replaces `clients` and `clients features` fences in page Markdown with real Markdown. Everything else, including
 * prompts, tabs, and alerts, stays as written.
 */
export function expandClientBlocks(markdown: string, clients: Client[], features: FeatureDefinition[]): string {
  return markdown.replace(
    /^(?<fence>`{3,}|~{3,})[ \t]*clients(?<features>[ \t]+features)?[ \t]*\r?\n(?:(?!\k<fence>)[^\n]*\n)*\k<fence>[`~]*[ \t]*\r?$/gm,
    (_match, _fence, featureGroup) =>
      featureGroup ? featureMatrixMarkdown(clients, features) : clientTabsMarkdown(clients),
  )
}

/** A sentence for marketing pages that names every client and what it covers, linking each to its docs. */
export function clientsSentence(clients: Client[]): string {
  const parts = clients.map((client) =>
    `<a href="${escapeHtml(client.docsUrl)}">${escapeHtml(client.language)}</a> (${escapeHtml(client.summary)})`
  )
  const list = parts.length > 2 ? `${parts.slice(0, -1).join(", ")}, and ${parts.at(-1)}` : parts.join(" and ")
  return `Exceptionless has clients for ${list}.`
}
