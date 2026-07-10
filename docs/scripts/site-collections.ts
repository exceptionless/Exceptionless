type Searcher = {
  pages<T = Record<string, unknown>>(query?: string, sort?: string, limit?: number): Array<LumePageData & T>
}

type LumePageData = {
  title?: string
  url?: string | false
  date?: Date | string | number
  order?: number
  hidden?: boolean
  draft?: boolean
  llms?: boolean
  page?: {
    sourcePath?: string
  }
}

export type ContentPage = {
  title: string
  url: string
  sourcePath: string
  date?: string
  order?: number
  llms?: boolean
}

type NavItem = {
  text: string
  href?: string
  items?: NavItem[]
}

type TreeNode = {
  segment: string
  page?: ContentPage
  children: Map<string, TreeNode>
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

export function docsNavHtml(search: Searcher, currentUrl = ""): string {
  const nav = buildDocsNav(docsPages(search))
  return `<ul class="toc-list">${renderNavItems(nav, currentUrl)}</ul>`
}

export function siteDataJson(search: Searcher): string {
  return JSON.stringify(
    {
      docsPages: visibleDocsPages(search),
      newsPosts: newsPosts(search),
    },
    null,
    2,
  )
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
    url: page.url,
    sourcePath,
    date: dateFromPath(sourcePath) || dateToIsoDate(page.date),
    order: optionalNumber(page.order),
    llms: optionalBool(page.llms),
  }
}

function buildDocsNav(pages: ContentPage[]): NavItem[] {
  const root: TreeNode = { segment: "docs", children: new Map() }

  for (const page of pages) {
    const relative = page.sourcePath.replace(/^docs\//, "").replace(/\.md$/, "")
    const parts = relative.split("/")
    const isIndex = parts.at(-1) === "index"
    const nodeParts = isIndex ? parts.slice(0, -1) : parts
    let current = root

    for (const part of nodeParts) {
      let child = current.children.get(part)
      if (!child) {
        child = { segment: part, children: new Map() }
        current.children.set(part, child)
      }
      current = child
    }

    current.page = page
  }

  const rootItem = nodeToNav(root)
  return rootItem ? [rootItem] : []
}

function nodeToNav(node: TreeNode): NavItem | undefined {
  const children = [...node.children.values()]
    .sort(compareNodes)
    .map(nodeToNav)
    .filter((item): item is NavItem => Boolean(item))

  if (node.page) {
    return {
      text: node.page.title,
      href: node.page.url,
      items: children.length ? children : undefined,
    }
  }

  if (!children.length) {
    return undefined
  }

  return {
    text: titleFromSegment(node.segment),
    items: children,
  }
}

function compareNodes(a: TreeNode, b: TreeNode): number {
  return compareOptionalNumbers(a.page?.order, b.page?.order) || nodeTitle(a).localeCompare(nodeTitle(b))
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

function nodeTitle(node: TreeNode): string {
  return node.page?.title || titleFromSegment(node.segment)
}

function renderNavItems(items: NavItem[], currentUrl: string): string {
  return items.map((item) => {
    const label = escapeHtml(item.text)
    const childHtml = item.items?.length ? `<ul>${renderNavItems(item.items, currentUrl)}</ul>` : ""
    if (!item.href) {
      return `<li><span>${label}</span>${childHtml}</li>`
    }

    const href = escapeHtml(item.href)
    const activeClass = item.href === currentUrl ? ' class="toc-active"' : ""
    return `<li${activeClass} data-url="${href}"><a href="${href}">${label}</a>${childHtml}</li>`
  }).join("\n")
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
