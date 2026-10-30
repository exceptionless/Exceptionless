/**
 * Redirects for moved pages. GitHub Pages ignores `_redirects`, so every old URL gets a static stub: an HTML page at
 * `<from>index.html` and a Markdown page at the old `.md` URL.
 */

export type RedirectPage = {
  url: string
  title?: string
  sourcePath?: string
  redirectFrom?: unknown
}

export type Redirect = {
  /** The old URL, for example `/docs/manual-stacking/`. */
  from: string
  /** The page the old URL moved to. */
  to: string
  /** The heading id on the new page the redirect lands on, without `#`. */
  anchor?: string
  /** The new page's title, for the Markdown stub. */
  toTitle: string
  /** The source file that declares the redirect, for error messages. */
  source: string
}

const siteUrl = "https://exceptionless.com"

/** Reads the `redirectFrom` frontmatter of every page and checks that the old URLs are well formed and unique. */
export function collectRedirects(pages: RedirectPage[]): Redirect[] {
  const redirects: Redirect[] = []
  const declaredBy = new Map<string, string>()

  for (const page of pages) {
    if (page.redirectFrom === undefined) {
      continue
    }

    const source = page.sourcePath || page.url
    if (!Array.isArray(page.redirectFrom)) {
      throw new Error(`${source}: redirectFrom must be a list.`)
    }

    for (const entry of page.redirectFrom) {
      const { from, anchor } = parseRedirectEntry(entry, source)
      if (from === page.url) {
        throw new Error(`${source}: redirectFrom ${from} is the page's own URL.`)
      }

      const previous = declaredBy.get(from)
      if (previous) {
        throw new Error(`${source}: redirectFrom ${from} is already declared by ${previous}.`)
      }

      declaredBy.set(from, source)
      redirects.push({ from, to: page.url, anchor, toTitle: page.title || page.url, source })
    }
  }

  const realUrls = new Map<string, string>()
  for (const page of pages) {
    realUrls.set(page.url, page.sourcePath || page.url)
    if (page.url.startsWith("/docs/")) {
      realUrls.set(markdownPathFor(page.url), page.sourcePath || page.url)
    }
  }

  for (const redirect of redirects) {
    for (const path of [redirect.from, markdownPathFor(redirect.from)]) {
      const collision = realUrls.get(path)
      if (collision) {
        throw new Error(`${redirect.source}: redirectFrom ${redirect.from} collides with the real page ${collision}.`)
      }
    }
  }

  return redirects.sort((a, b) => a.from.localeCompare(b.from))
}

function parseRedirectEntry(entry: unknown, source: string): { from: string; anchor?: string } {
  const value = typeof entry === "string" ? { from: entry } : entry
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    throw new Error(`${source}: each redirectFrom entry must be a URL or { from, anchor }.`)
  }

  const { from, anchor, ...rest } = value as Record<string, unknown>
  if (Object.keys(rest).length) {
    throw new Error(`${source}: redirectFrom has unknown keys: ${Object.keys(rest).join(", ")}.`)
  }

  if (typeof from !== "string" || !/^\/(?:[^\s?#.]+\/)+$/.test(from)) {
    throw new Error(
      `${source}: redirectFrom ${JSON.stringify(from)} must be a site path that starts and ends with a slash.`,
    )
  }

  if (anchor !== undefined && (typeof anchor !== "string" || !/^[^\s#]+$/.test(anchor))) {
    throw new Error(`${source}: redirectFrom anchor for ${from} must be a heading id without "#".`)
  }

  return { from, anchor: anchor as string | undefined }
}

/** The Markdown URL of a page: `/docs/` is `/docs.md` and `/docs/a/b/` is `/docs/a/b.md`. */
export function markdownPathFor(url: string): string {
  return url === "/docs/" ? "/docs.md" : `${url.replace(/\/$/, "")}.md`
}

export function redirectTarget(redirect: Pick<Redirect, "to" | "anchor">): string {
  return `${redirect.to}${redirect.anchor ? `#${redirect.anchor}` : ""}`
}

/**
 * An HTML stub that sends the visitor to the new page. A hash on the old URL wins over the declared anchor, so links
 * such as `/docs/bulk-actions/#some-heading` keep working.
 */
export function renderRedirectHtml(target: { to: string; anchor?: string }, origin = siteUrl): string {
  const destination = redirectTarget(target)
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="robots" content="noindex">
  <title>Redirecting - Exceptionless</title>
  <link rel="canonical" href="${origin}${target.to}">
  <script>location.replace(${scriptString(target.to)} + (location.hash || ${
    scriptString(target.anchor ? `#${target.anchor}` : "")
  }))</script>
  <meta http-equiv="refresh" content="0; url=${attribute(destination)}">
</head>
<body>
  <p>This page moved. <a href="${attribute(destination)}">Continue to the new page</a>.</p>
</body>
</html>
`
}

/** A Markdown stub for the old `.md` URL, for readers and agents that fetch Markdown. */
export function renderRedirectMarkdown(redirect: Redirect, origin = siteUrl): string {
  const markdownTarget = redirect.to.startsWith("/docs/") ? markdownPathFor(redirect.to) : redirect.to
  const link = `${origin}${markdownTarget}${redirect.anchor ? `#${redirect.anchor}` : ""}`
  return `# This page moved

The page at ${origin}${redirect.from} moved to [${redirect.toTitle}](${link}).

Read it at: ${link}

---

All Exceptionless documentation: ${origin}/llms.txt
`
}

function attribute(value: string): string {
  return value.replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll("<", "&lt;")
}

function scriptString(value: string): string {
  return JSON.stringify(value).replaceAll("<", "\\u003c")
}
