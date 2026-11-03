import { crumbsFor, type NavEntry, type NavGroup, normalizeDocsNav, pagerFor } from "./docs-nav.ts"
import { collectRedirects, type Redirect } from "./redirects.ts"

type Searcher = {
  pages<T = Record<string, unknown>>(query?: string, sort?: string, limit?: number): Array<LumePageData & T>
}

type LumePageData = {
  title?: string
  description?: string
  url?: string | false
  date?: Date | string | number
  order?: number
  navTitle?: string
  plan?: string
  redirectFrom?: unknown
  hidden?: boolean
  draft?: boolean
  llms?: boolean
  page?: {
    sourcePath?: string
  }
}

export type ContentPage = {
  title: string
  navTitle?: string
  plan?: string
  description?: string
  url: string
  sourcePath: string
  date?: string
  order?: number
  llms?: boolean
}

const excludedNews = new Set([
  "news/2020/2020-11-11-self-hosted-error-monitoring-on-azure.md",
  "news/index.md",
])

export function docsPages(search: Searcher): ContentPage[] {
  return search.pages()
    .map(toContentPage)
    .filter((page): page is ContentPage => Boolean(page))
    .filter((page) => page.sourcePath.startsWith("docs/") && page.sourcePath.endsWith(".md"))
    .sort(comparePages)
}

export function visibleDocsPages(search: Searcher): ContentPage[] {
  return docsPages(search).filter((page) => page.llms !== false)
}

export function newsPosts(search: Searcher): ContentPage[] {
  return search.pages()
    .map(toContentPage)
    .filter((page): page is ContentPage => Boolean(page))
    .filter((page) => page.sourcePath.startsWith("news/") && page.sourcePath.endsWith(".md"))
    .filter((page) => !excludedNews.has(page.sourcePath))
    .sort((a, b) => (b.date || "").localeCompare(a.date || "") || a.url.localeCompare(b.url))
}

/** The label a page has in the sidebar, breadcrumbs, and previous/next links. */
function pageLabel(page: ContentPage): string {
  return page.navTitle || page.title
}

function labelsByUrl(search: Searcher): Map<string, string> {
  return new Map(docsPages(search).map((page) => [page.url, pageLabel(page)]))
}

/** Renders the docs sidebar from `_data/docs_nav.yml`. Entries without a page yet are skipped. */
export function docsNavHtml(search: Searcher, currentUrl: string, rawNav: unknown): string {
  const sections = normalizeDocsNav(rawNav)
  const labels = labelsByUrl(search)

  const renderEntries = (entries: NavEntry[]): string =>
    entries.map((entry) => {
      if (entry.kind === "page") {
        const label = labels.get(entry.url)
        return label ? renderPageItem(entry.url, label, currentUrl) : ""
      }

      return renderGroup(entry, labels, currentUrl, renderEntries)
    }).filter(Boolean).join("\n")

  const items = sections.map((section) => {
    const children = renderEntries(section.items)
    return children ? `<li class="toc-section"><span>${escapeHtml(section.title)}</span><ul>${children}</ul></li>` : ""
  }).filter(Boolean).join("\n")

  return `<ul class="toc-list">${items}</ul>`
}

function renderPageItem(url: string, label: string, currentUrl: string): string {
  const href = escapeHtml(url)
  const current = url === currentUrl
  return `<li${current ? ' class="toc-active"' : ""} data-url="${href}"><a href="${href}"${
    current ? ' aria-current="page"' : ""
  }>${escapeHtml(label)}</a></li>`
}

function renderGroup(
  group: NavGroup,
  labels: Map<string, string>,
  currentUrl: string,
  renderEntries: (entries: NavEntry[]) => string,
): string {
  const children = renderEntries(group.items)
  const groupHasPage = group.url ? labels.has(group.url) : false
  if (!children && !groupHasPage) {
    return ""
  }

  const open = containsUrl(group, currentUrl) ? " open" : ""
  const current = group.url === currentUrl
  const heading = group.url && groupHasPage
    ? `<a href="${escapeHtml(group.url)}"${current ? ' aria-current="page"' : ""}>${escapeHtml(group.title)}</a>`
    : `<span>${escapeHtml(group.title)}</span>`
  const list = children ? `<ul>${children}</ul>` : ""
  return `<li class="toc-group${
    current ? " toc-active" : ""
  }"><details${open}><summary>${heading}</summary>${list}</details></li>`
}

function containsUrl(group: NavGroup, url: string): boolean {
  return group.url === url ||
    group.items.some((entry) => entry.kind === "page" ? entry.url === url : containsUrl(entry, url))
}

/** Renders the previous and next links at the bottom of a docs page, following the nav order. */
export function docsPagerHtml(search: Searcher, currentUrl: string, rawNav: unknown): string {
  const labels = labelsByUrl(search)
  const { previous, next } = pagerFor(normalizeDocsNav(rawNav), currentUrl, (url) => labels.get(url))
  if (!previous && !next) {
    return ""
  }

  const link = (kind: "previous" | "next", target: { url: string; title: string } | undefined) =>
    target
      ? `<a class="docs-pager-${kind}" href="${escapeHtml(target.url)}" rel="${
        kind === "next" ? "next" : "prev"
      }"><span class="docs-pager-label">${kind === "next" ? "Next" : "Previous"}</span><span class="docs-pager-title">${
        escapeHtml(target.title)
      }</span></a>`
      : "<span></span>"

  return `<nav class="docs-pager" aria-label="Previous and next pages" data-search-ignore>${
    link("previous", previous)
  }${link("next", next)}</nav>`
}

export type Breadcrumb = {
  name: string
  url: string
}

/**
 * Returns the Home > Docs > section > group > page trail for a docs URL. The sections and groups come from the nav,
 * and each links to its first page.
 */
export function docsBreadcrumbs(search: Searcher, currentUrl: string, rawNav?: unknown): Breadcrumb[] {
  if (!currentUrl.startsWith("/docs/")) {
    return []
  }

  const labels = labelsByUrl(search)
  const crumbs: Breadcrumb[] = [{ name: "Home", url: "/" }, { name: "Docs", url: "/docs/" }]
  if (rawNav) {
    crumbs.push(...crumbsFor(normalizeDocsNav(rawNav), currentUrl, (url) => labels.get(url)))
  }

  const current = labels.get(currentUrl)
  if (current && currentUrl !== "/docs/") {
    crumbs.push({ name: current, url: currentUrl })
  }

  return crumbs
}

export function siteDataJson(search: Searcher): string {
  return JSON.stringify(
    {
      docsPages: visibleDocsPages(search),
      newsPosts: newsPosts(search),
      redirects: redirects(search),
    },
    null,
    2,
  )
}

/** Every `redirectFrom` declared by a published page, checked for bad paths and collisions. */
export function redirects(search: Searcher): Redirect[] {
  const pages = search.pages()
    .filter((page) => !page.draft && !page.hidden && typeof page.url === "string")
    .map((page) => ({
      url: page.url as string,
      title: page.title,
      sourcePath: slash(page.page?.sourcePath || "").replace(/^\/+/, ""),
      redirectFrom: page.redirectFrom,
    }))
    .filter((page) => page.sourcePath !== "(generated)")

  return collectRedirects(pages)
}

function toContentPage(page: LumePageData): ContentPage | undefined {
  if (page.draft || page.hidden) {
    return undefined
  }

  if (typeof page.url !== "string") {
    return undefined
  }

  const sourcePath = slash(page.page?.sourcePath || "").replace(/^\/+/, "")
  if (!sourcePath || sourcePath === "(generated)") {
    return undefined
  }

  return {
    title: page.title || titleFromPath(sourcePath),
    navTitle: page.navTitle,
    plan: page.plan,
    description: page.description,
    url: page.url,
    sourcePath,
    date: dateFromPath(sourcePath) || dateToIsoDate(page.date),
    order: optionalNumber(page.order),
    llms: optionalBool(page.llms),
  }
}

function comparePages(a: ContentPage, b: ContentPage): number {
  return compareOptionalNumbers(a.order, b.order) || a.url.localeCompare(b.url)
}

function compareOptionalNumbers(a?: number, b?: number): number {
  if (a !== undefined && b !== undefined && a !== b) {
    return a - b
  }

  if (a !== undefined && b === undefined) {
    return -1
  }

  if (a === undefined && b !== undefined) {
    return 1
  }

  return 0
}

function titleFromPath(path: string): string {
  const normalized = slash(path).replace(/\.md$/, "")
  const name = normalized.endsWith("/index")
    ? normalized.split("/").at(-2) || "Page"
    : normalized.split("/").at(-1) || "Page"
  return titleFromSegment(name)
}

function titleFromSegment(segment: string): string {
  return segment
    .split("-")
    .map((part) => part ? part[0].toUpperCase() + part.slice(1) : part)
    .join(" ")
}

function dateFromPath(path: string): string | undefined {
  const match = slash(path).match(/\/(\d{4}-\d{2}-\d{1,2})-/)
  return match?.[1]
}

function dateToIsoDate(value: Date | string | number | undefined): string | undefined {
  if (!value) {
    return undefined
  }

  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.valueOf())) {
    return typeof value === "string" ? value : undefined
  }

  return date.toISOString().slice(0, 10)
}

function optionalNumber(value: number | undefined): number | undefined {
  return Number.isFinite(value) ? value : undefined
}

function optionalBool(value: boolean | undefined): boolean | undefined {
  return typeof value === "boolean" ? value : undefined
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
}

function slash(path: string): string {
  return path.replaceAll("\\", "/")
}
