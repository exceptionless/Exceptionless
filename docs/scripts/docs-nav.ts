import { parse } from "lume/deps/yaml.ts"

/** A docs page in the nav, identified by its URL. */
export type NavPage = { kind: "page"; url: string }

/** A collapsible sub-group. When `url` is set, the group heading links to that overview page. */
export type NavGroup = { kind: "group"; title: string; url?: string; items: NavEntry[] }

export type NavEntry = NavPage | NavGroup

export type NavSection = { title: string; items: NavEntry[] }

/** A page in reading order, with the section and groups that contain it. */
export type NavLocation = { url: string; section: NavSection; groups: NavGroup[] }

/** Parses and validates the text of `_data/docs_nav.yml`. */
export function parseDocsNav(yamlText: string): NavSection[] {
  return normalizeDocsNav(parse(yamlText))
}

/** Validates the parsed nav, which Lume also exposes to templates as `docs_nav`. Throws with a readable message. */
export function normalizeDocsNav(raw: unknown): NavSection[] {
  if (!Array.isArray(raw)) {
    throw new Error("docs_nav.yml must be a list of sections.")
  }

  const seen = new Set<string>()
  const sections = raw.map((section, index) => {
    if (!isRecord(section) || typeof section.section !== "string" || !section.section.trim()) {
      throw new Error(`docs_nav.yml: entry ${index + 1} must have a "section" name.`)
    }

    const unknown = Object.keys(section).filter((key) => key !== "section" && key !== "items")
    if (unknown.length) {
      throw new Error(`docs_nav.yml: section "${section.section}" has unknown keys: ${unknown.join(", ")}.`)
    }

    return {
      title: section.section.trim(),
      items: parseEntries(section.items, section.section, seen),
    }
  })

  const titles = sections.map((section) => section.title)
  if (new Set(titles).size !== titles.length) {
    throw new Error("docs_nav.yml: section names must be unique.")
  }

  return sections
}

function parseEntries(value: unknown, where: string, seen: Set<string>): NavEntry[] {
  if (!Array.isArray(value)) {
    throw new Error(`docs_nav.yml: "${where}" must have an "items" list.`)
  }

  return value.map((entry): NavEntry => {
    if (typeof entry === "string") {
      return { kind: "page", url: claimUrl(entry, where, seen) }
    }

    if (isRecord(entry) && typeof entry.group === "string") {
      const unknown = Object.keys(entry).filter((key) => !["group", "url", "items"].includes(key))
      if (unknown.length) {
        throw new Error(`docs_nav.yml: group "${entry.group}" has unknown keys: ${unknown.join(", ")}.`)
      }

      const url = typeof entry.url === "string" ? claimUrl(entry.url, entry.group, seen) : undefined
      return { kind: "group", title: entry.group, url, items: parseEntries(entry.items, entry.group, seen) }
    }

    throw new Error(`docs_nav.yml: "${where}" has an item that is neither a URL nor a group.`)
  })
}

function claimUrl(url: string, where: string, seen: Set<string>): string {
  if (!/^\/docs\/(?:[^\s?#]*\/)?$/.test(url)) {
    throw new Error(`docs_nav.yml: "${url}" in "${where}" must be a /docs/ URL that ends with a slash.`)
  }

  if (seen.has(url)) {
    throw new Error(`docs_nav.yml: "${url}" is listed more than once.`)
  }

  seen.add(url)
  return url
}

/** Lists every page in reading order: a group's overview page first, then its items. */
export function flattenNav(sections: NavSection[]): NavLocation[] {
  const pages: NavLocation[] = []

  const visit = (entries: NavEntry[], section: NavSection, groups: NavGroup[]) => {
    for (const entry of entries) {
      if (entry.kind === "page") {
        pages.push({ url: entry.url, section, groups })
        continue
      }

      const inner = [...groups, entry]
      if (entry.url) {
        pages.push({ url: entry.url, section, groups: inner })
      }

      visit(entry.items, section, inner)
    }
  }

  for (const section of sections) {
    visit(section.items, section, [])
  }

  return pages
}

export function navUrls(sections: NavSection[]): string[] {
  return flattenNav(sections).map((page) => page.url)
}

/** The pages that make up a section, in reading order. */
export function sectionPages(sections: NavSection[], title: string): string[] {
  return flattenNav(sections).filter((page) => page.section.title === title).map((page) => page.url)
}

/**
 * The client overview pages: the clients index and the overview page of each language group directly under
 * "Send data". These are the pages `llms-small.txt` includes next to Get started and AI.
 */
export function clientOverviewUrls(sections: NavSection[]): string[] {
  const section = sections.find((candidate) => candidate.title === "Send data")
  if (!section) {
    return []
  }

  const urls: string[] = []
  for (const entry of section.items) {
    if (entry.kind === "page" && entry.url === "/docs/clients/") {
      urls.push(entry.url)
    } else if (entry.kind === "group" && entry.url) {
      urls.push(entry.url)
    }
  }

  return urls
}

export type PagerLink = { url: string; title: string }

/** Finds the previous and next pages around `currentUrl`, skipping nav entries that have no page yet. */
export function pagerFor(
  sections: NavSection[],
  currentUrl: string,
  titleFor: (url: string) => string | undefined,
): { previous?: PagerLink; next?: PagerLink } {
  const pages = flattenNav(sections)
    .map((page) => ({ url: page.url, title: titleFor(page.url) }))
    .filter((page): page is PagerLink => Boolean(page.title))
  const index = pages.findIndex((page) => page.url === currentUrl)
  if (index === -1) {
    return {}
  }

  return { previous: pages[index - 1], next: pages[index + 1] }
}

export type NavCrumb = { name: string; url: string }

/** Builds the trail between "Docs" and the page: the section, then each group, linking to the first page where one exists. */
export function crumbsFor(
  sections: NavSection[],
  currentUrl: string,
  titleFor: (url: string) => string | undefined,
): NavCrumb[] {
  const pages = flattenNav(sections)
  const location = pages.find((page) => page.url === currentUrl)
  if (!location) {
    return []
  }

  const crumbs: NavCrumb[] = []
  const firstPage = pages.find((page) => page.section === location.section && titleFor(page.url))
  if (firstPage && firstPage.url !== currentUrl) {
    crumbs.push({ name: location.section.title, url: firstPage.url })
  }

  for (const group of location.groups) {
    if (group.url && group.url !== currentUrl && titleFor(group.url)) {
      crumbs.push({ name: group.title, url: group.url })
    }
  }

  return crumbs
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
}
