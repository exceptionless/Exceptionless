type GeneratedData = {
  docsPages: ContentPage[]
  newsPosts: ContentPage[]
}

type ContentPage = {
  title: string
  url: string
  sourcePath: string
  date?: string
}

type RedirectAlias = {
  from: string
  to: string
}

type SearchIndexEntry = {
  title: string
  url: string
  description: string
  prose: string
  text: string
  codeBlocks?: SearchCodeBlock[]
}

type SearchCodeBlock = {
  language: string
  text: string
  tokens: SearchCodeToken[]
}

type SearchCodeToken = {
  text: string
  classes?: string[]
}

type SiteData = {
  description?: string
}

const siteUrl = "https://exceptionless.com"
const newsPostsPerPage = 10
const siteData = JSON.parse(await Deno.readTextFile("_data/site.json")) as SiteData
const fallbackDescription = normalizeHtmlText(siteData.description ?? "")
const generated = JSON.parse(await Deno.readTextFile("_site/site-data.json")) as GeneratedData

await copyDir("public", "_site")
await copyDirIfExists("docs/img", "_site/docs/img")
await copyNonMarkdownFiles("news", "_site/news")
await rewriteHtmlReferences("_site")
await rewriteNewsIndex(generated.newsPosts)

const routes = await collectHtmlRoutes("_site")
await writeSearchIndex("_site", routes)
const aliases = await collectRedirectAliases("_site", generated.newsPosts)
await createRedirectAliasPages(aliases)

await Deno.writeTextFile("_site/feed.xml", renderFeed(generated.newsPosts.slice(0, 25)))
await Deno.writeTextFile("_site/sitemap.xml", renderSitemap(routes))
await Deno.writeTextFile("_site/robots.txt", renderRobots())
await Deno.writeTextFile("_site/llms.txt", renderLlms(generated.docsPages, false))
await Deno.writeTextFile("_site/llms-full.txt", renderLlms(generated.docsPages, true))
await Deno.writeTextFile("_site/_redirects", renderRedirects(aliases))
await removeIfExists("_site/site-data.json")

async function copyDirIfExists(source: string, destination: string): Promise<void> {
  try {
    if ((await Deno.stat(source)).isDirectory) {
      await copyDir(source, destination)
    }
  } catch {
    // Optional asset folders are copied when present.
  }
}

async function removeIfExists(path: string): Promise<void> {
  try {
    await Deno.remove(path)
  } catch (error) {
    if (!(error instanceof Deno.errors.NotFound)) {
      throw error
    }
  }
}

async function copyDir(source: string, destination: string): Promise<void> {
  await Deno.mkdir(destination, { recursive: true })

  for await (const entry of Deno.readDir(source)) {
    const from = `${source}/${entry.name}`
    const to = `${destination}/${entry.name}`
    if (entry.isDirectory) {
      await copyDir(from, to)
    } else if (entry.isFile) {
      await Deno.copyFile(from, to)
    }
  }
}

async function copyNonMarkdownFiles(source: string, destination: string): Promise<void> {
  await Deno.mkdir(destination, { recursive: true })

  for await (const entry of Deno.readDir(source)) {
    const from = `${source}/${entry.name}`
    const to = `${destination}/${entry.name}`
    if (entry.isDirectory) {
      await copyNonMarkdownFiles(from, to)
    } else if (entry.isFile && !entry.name.endsWith(".md")) {
      await Deno.copyFile(from, to)
    }
  }
}

async function rewriteHtmlReferences(root: string): Promise<void> {
  for await (const path of walk(root)) {
    if (!path.endsWith(".html")) {
      continue
    }

    let html = await Deno.readTextFile(path)
    const normalizedPath = slash(path)

    if (normalizedPath.startsWith(`${root}/docs/`)) {
      html = html
        .replace(/src="(?:\.\/)?img\//g, 'src="/docs/img/')
        .replace(/src="\.\.\/docs\/img\//g, 'src="/docs/img/')
        .replace(/src="\.\.\/\.\.\/img\//g, 'src="/docs/img/')
    }

    const newsMatch = normalizedPath.match(new RegExp(`^${root}/news/(\\d{4})/`))
    if (newsMatch) {
      const year = newsMatch[1]
      html = html.replace(
        /src="(?:\.\/)?([^"\/:]+\.(?:png|jpe?g|gif|webp|avif|svg|mp4|webm))"/gi,
        (_match, fileName) => {
          return `src="/news/${year}/${fileName}"`
        },
      )
      html = removeDuplicatePostHeading(html)
      html = rewriteYouTubeEmbeds(html)
    }

    html = normalizeCodeBlockClasses(html)

    await Deno.writeTextFile(path, html)
  }
}

function normalizeCodeBlockClasses(html: string): string {
  return html.replace(
    /<pre><code class="([^"]*\blanguage-[^"]*)">/g,
    '<pre class="$1"><code class="$1">',
  )
}

function removeDuplicatePostHeading(html: string): string {
  const titleMatch = html.match(/<h1 class="entry-title"[^>]*>([\s\S]*?)<\/h1>/i)
  if (!titleMatch) {
    return html
  }

  const title = normalizeHtmlText(titleMatch[1])
  return html.replace(
    /(<div class="entry-content">\s*)<h1\b[^>]*>([\s\S]*?)<\/h1>\s*/i,
    (match, prefix: string, heading: string) => {
      return normalizeHtmlText(heading) === title ? prefix : match
    },
  )
}

function normalizeHtmlText(html: string): string {
  return decodeHtmlEntities(html.replace(/<[^>]*>/g, ""))
    .replace(/\s+/g, " ")
    .trim()
}

function decodeHtmlEntities(value: string): string {
  return value
    .replace(/&nbsp;/g, " ")
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, '"')
    .replace(/&#39;|&apos;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&#(\d+);/g, (_match, value: string) => String.fromCodePoint(Number(value)))
    .replace(/&#x([0-9a-f]+);/gi, (_match, value: string) => String.fromCodePoint(parseInt(value, 16)))
}

function rewriteYouTubeEmbeds(html: string): string {
  return html.replace(
    /<p>\s*(https?:\/\/(?:www\.)?(?:youtube\.com\/watch\?[^<\s]+|youtu\.be\/[^<\s]+))\s*<\/p>/gi,
    (match, url: string) => {
      const videoId = youtubeVideoId(url)
      return videoId ? renderYouTubeEmbed(videoId) : match
    },
  )
}

function youtubeVideoId(value: string): string | undefined {
  try {
    const url = new URL(value)
    if (url.hostname === "youtu.be") {
      return url.pathname.replace(/^\/+/, "").split("/")[0] || undefined
    }

    if (url.hostname === "youtube.com" || url.hostname === "www.youtube.com") {
      return url.searchParams.get("v") || undefined
    }
  } catch {
    return undefined
  }
}

function renderYouTubeEmbed(videoId: string): string {
  return `<div id="${videoId}" class="eleventy-plugin-youtube-embed" style="position:relative;width:100%;padding-top: 56.25%;"><iframe style="position:absolute;top:0;right:0;bottom:0;left:0;width:100%;height:100%;" width="100%" height="100%" frameborder="0" title="Embedded YouTube video" src="https://www.youtube-nocookie.com/embed/${videoId}" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" allowfullscreen></iframe></div>`
}

async function rewriteNewsIndex(posts: ContentPage[]): Promise<void> {
  const indexPath = "_site/news/index.html"
  let html: string
  try {
    html = await Deno.readTextFile(indexPath)
  } catch {
    return
  }

  await removeIfExists("_site/news/page")

  const pageCount = Math.max(1, Math.ceil(posts.length / newsPostsPerPage))
  for (let pageIndex = 0; pageIndex < pageCount; pageIndex++) {
    const pageNumber = pageIndex + 1
    const pagePosts = posts.slice(pageIndex * newsPostsPerPage, pageNumber * newsPostsPerPage)
    const outputPath = pageNumber === 1 ? indexPath : `_site/news/page/${pageNumber}/index.html`
    const body = [
      await renderNewsArticles(pagePosts),
      renderNewsPager(pageNumber, pageCount),
    ].filter(Boolean).join("\n\n")

    await Deno.mkdir(outputPath.replace(/\/index\.html$/, ""), { recursive: true })
    await Deno.writeTextFile(outputPath, renderNewsPage(html, newsPageUrl(pageNumber), body))
  }
}

function renderNewsPage(template: string, pageUrl: string, body: string): string {
  const html = template.replace("<!-- NEWS_INDEX_PLACEHOLDER -->", body)
  if (pageUrl === "/news/") {
    return html
  }

  return html.replaceAll(`${siteUrl}/news/`, `${siteUrl}${pageUrl}`)
}

async function renderNewsArticles(posts: ContentPage[]): Promise<string> {
  const articles: string[] = []
  for (const post of posts) {
    try {
      const postHtml = await Deno.readTextFile(`_site${post.url}index.html`)
      const body = extractPostBody(postHtml)
      articles.push([
        '<article class="post type-post status-publish format-standard">',
        `    <h2 id="${slugify(post.title)}"><a href="${post.url}">${escapeHtml(post.title)}</a></h2>`,
        body,
        "</article>",
      ].join("\n"))
    } catch {
      // Missing post output should be caught by the normal build and link verification.
    }
  }

  return articles.join("\n\n")
}

function renderNewsPager(pageNumber: number, pageCount: number): string {
  if (pageCount <= 1) {
    return ""
  }

  const olderHref = pageNumber < pageCount ? newsPageUrl(pageNumber + 1) : undefined
  const newerHref = pageNumber > 1 ? newsPageUrl(pageNumber - 1) : undefined

  return [
    '<ul class="pager">',
    `  <li class="previous${olderHref ? "" : " disabled"}">${
      olderHref ? `<a href="${olderHref}">Older Articles</a>` : "<span>Older Articles</span>"
    }</li>`,
    `  <li class="next${newerHref ? "" : " disabled"}">${
      newerHref ? `<a href="${newerHref}">Newer Articles</a>` : "<span>Newer Articles</span>"
    }</li>`,
    "</ul>",
  ].join("\n")
}

function newsPageUrl(pageNumber: number): string {
  return pageNumber <= 1 ? "/news/" : `/news/page/${pageNumber}/`
}

function extractPostBody(html: string): string {
  const match = html.match(/<div class="entry-content">\s*([\s\S]*?)\s*<\/div>\s*<\/article>/)
  return match ? match[1].trim() : ""
}

function slugify(value: string): string {
  return value
    .toLowerCase()
    .replace(/&/g, "and")
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
}

function escapeHtml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
}

async function collectHtmlRoutes(root: string): Promise<string[]> {
  const routes: string[] = []
  for await (const path of walk(root)) {
    if (!path.endsWith(".html")) {
      continue
    }

    const relative = slash(path).slice(root.length + 1)
    if (relative === "404.html") {
      routes.push("/404.html")
      continue
    }

    routes.push(`/${relative.replace(/(^|\/)index\.html$/, "$1").replace(/\.html$/, "/")}`.replace(/\/+/g, "/"))
  }

  return [...new Set(routes)].sort()
}

async function writeSearchIndex(root: string, routes: string[]): Promise<void> {
  const entries: SearchIndexEntry[] = []

  for (const route of routes) {
    if (!shouldIndexRoute(route)) {
      continue
    }

    const htmlPath = htmlPathForRoute(root, route)
    let html: string
    try {
      html = await Deno.readTextFile(htmlPath)
    } catch {
      continue
    }

    const cleanedHtml = stripSearchIgnoredContent(stripHtmlComments(html))
    const mainHtml = extractMainHtml(cleanedHtml) || cleanedHtml
    const codeBlocks = extractSearchCodeBlocks(mainHtml)
    const prose = htmlToText(removeCodeBlocks(mainHtml))
    const text = htmlToText(mainHtml)
    if (!text) {
      continue
    }

    entries.push({
      title: pageTitle(cleanedHtml) || titleFromRoute(route),
      url: route,
      description: pageDescription(cleanedHtml),
      prose,
      text,
      ...(codeBlocks.length ? { codeBlocks } : {}),
    })
  }

  await Deno.writeTextFile(`${root}/search-index.json`, JSON.stringify({ entries }, null, 2))
}

function shouldIndexRoute(route: string): boolean {
  return route !== "/404.html" &&
    route !== "/search/" &&
    route !== "/news/" &&
    !route.startsWith("/news/page/") &&
    !route.startsWith("/category/")
}

function htmlPathForRoute(root: string, route: string): string {
  if (route === "/404.html") {
    return `${root}/404.html`
  }

  const relative = route.replace(/^\/+/, "")
  if (!relative) {
    return `${root}/index.html`
  }

  return route.endsWith("/") ? `${root}/${relative}index.html` : `${root}/${relative}`
}

function stripSearchIgnoredContent(html: string): string {
  let previous = ""
  let current = html
  while (current !== previous) {
    previous = current
    current = current.replace(/<([a-z][\w:-]*)(?=[^>]*\bdata-search-ignore\b)[^>]*>[\s\S]*?<\/\1>/gi, "")
  }

  return current.replace(/<[^>]*\bdata-search-ignore\b[^>]*>/gi, "")
}

function extractMainHtml(html: string): string {
  return html.match(/<main\b[^>]*id=["']page-content["'][^>]*>([\s\S]*?)<\/main>/i)?.[1] ?? ""
}

function pageTitle(html: string): string {
  const title = html.match(/<title[^>]*>([\s\S]*?)<\/title>/i)?.[1] ?? ""
  return htmlToText(title).replace(/\s+-\s+Exceptionless$/, "").trim()
}

function metaDescription(html: string): string {
  return htmlToText(
    html.match(/<meta\s+name=["']description["'][^>]*content=["']([^"']*)["'][^>]*>/i)?.[1] ??
      html.match(/<meta\s+content=["']([^"']*)["'][^>]*name=["']description["'][^>]*>/i)?.[1] ??
      "",
  )
}

function pageDescription(html: string): string {
  const description = metaDescription(html)
  return description && description !== fallbackDescription ? description : ""
}

function titleFromRoute(route: string): string {
  const segment = route.replace(/\/$/, "").split("/").filter(Boolean).at(-1) || "Exceptionless"
  return segment
    .split("-")
    .map((part) => part ? part[0].toUpperCase() + part.slice(1) : part)
    .join(" ")
}

function htmlToText(html: string): string {
  return normalizeHtmlText(
    html
      .replace(/<script\b[\s\S]*?<\/script>/gi, " ")
      .replace(/<style\b[\s\S]*?<\/style>/gi, " ")
      .replace(
        /<\/?(?:article|aside|blockquote|br|dd|div|dl|dt|figcaption|figure|footer|h[1-6]|header|li|main|nav|ol|p|pre|section|table|td|th|tr|ul)\b[^>]*>/gi,
        " ",
      ),
  )
}

function removeCodeBlocks(html: string): string {
  return html.replace(/<pre\b[^>]*>\s*<code\b[^>]*>[\s\S]*?<\/code>\s*<\/pre>/gi, " ")
}

function extractSearchCodeBlocks(html: string): SearchCodeBlock[] {
  const blocks: SearchCodeBlock[] = []
  const pattern = /<pre\b([^>]*)>\s*<code\b([^>]*)>([\s\S]*?)<\/code>\s*<\/pre>/gi

  for (const match of html.matchAll(pattern)) {
    const tokens = extractSearchCodeTokens(match[3])
    const text = tokens.map((token) => token.text).join("")
    if (!text.trim()) {
      continue
    }

    blocks.push({
      language: codeLanguage(`${match[1]} ${match[2]}`),
      text,
      tokens,
    })
  }

  return blocks
}

function extractSearchCodeTokens(html: string): SearchCodeToken[] {
  const tokens: SearchCodeToken[] = []
  const classStack: string[][] = []

  for (const match of html.matchAll(/<span\b[^>]*>|<\/span>|[^<]+|</gi)) {
    const part = match[0]
    if (/^<span\b/i.test(part)) {
      classStack.push(highlightClasses(part))
      continue
    }

    if (/^<\/span>$/i.test(part)) {
      classStack.pop()
      continue
    }

    appendSearchCodeToken(tokens, decodeHtmlEntities(part), [...new Set(classStack.flat())])
  }

  return tokens
}

function highlightClasses(span: string): string[] {
  const value = span.match(/\bclass\s*=\s*["']([^"']*)["']/i)?.[1] ?? ""
  return value
    .split(/\s+/)
    .filter((className) => /^hljs-[a-z0-9_-]+$/i.test(className) || /^[a-z][a-z0-9_-]*_$/i.test(className))
}

function appendSearchCodeToken(tokens: SearchCodeToken[], text: string, classes: string[]): void {
  if (!text) {
    return
  }

  const previous = tokens.at(-1)
  if (previous && sameClasses(previous.classes, classes)) {
    previous.text += text
    return
  }

  tokens.push({ text, ...(classes.length ? { classes } : {}) })
}

function sameClasses(left: string[] | undefined, right: string[]): boolean {
  const leftClasses = left ?? []
  return leftClasses.length === right.length && leftClasses.every((className, index) => className === right[index])
}

function codeLanguage(attributes: string): string {
  return attributes.match(/\blanguage-([a-z0-9_-]+)/i)?.[1]?.toLowerCase() ?? "text"
}

async function collectRedirectAliases(root: string, posts: ContentPage[]): Promise<RedirectAlias[]> {
  const aliases = new Map<string, string>()
  const postAliases = buildPostAliasMap(posts)

  for await (const path of walk(root)) {
    if (!path.endsWith(".html")) {
      continue
    }

    const html = stripHtmlComments(await Deno.readTextFile(path))
    for (const match of html.matchAll(/\bhref=["']([^"']+)["']/gi)) {
      const target = match[1]
      if (shouldSkipHref(target)) {
        continue
      }

      const url = new URL(target, `https://local${routeForPage(path, root)}`)
      if (url.origin !== "https://local" || existsLocalRoute(url.pathname, root)) {
        continue
      }

      const pathname = ensureTrailingSlash(url.pathname)
      if (pathname.startsWith("/category/")) {
        aliases.set(pathname, "/news/")
      } else if (postAliases.has(pathname)) {
        aliases.set(pathname, postAliases.get(pathname)!)
      } else if (isRootSlug(pathname)) {
        aliases.set(pathname, postAliases.get(pathname) || "/news/")
      }
    }
  }

  return [...aliases.entries()].map(([from, to]) => ({ from, to })).sort((a, b) => a.from.localeCompare(b.from))
}

async function createRedirectAliasPages(aliases: RedirectAlias[]): Promise<void> {
  for (const alias of aliases) {
    const destination = `_site/${alias.from.replace(/^\/+/, "")}index.html`
    await Deno.mkdir(destination.replace(/\/index\.html$/, ""), { recursive: true })
    await Deno.writeTextFile(destination, renderRedirectPage(alias.to))
  }
}

function buildPostAliasMap(posts: ContentPage[]): Map<string, string> {
  const aliases = new Map<string, string>()
  for (const post of posts) {
    const fileName = post.sourcePath.split("/").pop()?.replace(/\.md$/, "") || ""
    const slug = fileName.replace(/^\d{4}-\d{2}-\d{1,2}-/, "")
    aliases.set(`/${slug}/`, post.url)
  }

  aliases.set("/2016-recap-let-stats/", "/news/2017/2017-01-11-2016-recap-let-there-be-stats/")
  aliases.set("/bug-tracking/", "/why/")
  return aliases
}

async function* walk(dir: string): AsyncGenerator<string> {
  for await (const entry of Deno.readDir(dir)) {
    const path = `${dir}/${entry.name}`
    if (entry.isDirectory) {
      yield* walk(path)
    } else if (entry.isFile) {
      yield slash(path)
    }
  }
}

function renderFeed(posts: ContentPage[]): string {
  const updated = new Date().toISOString()
  const items = posts.map((post) => {
    const url = `${siteUrl}${post.url}`
    const published = post.date ? new Date(`${post.date}T00:00:00.000Z`).toUTCString() : new Date().toUTCString()
    return [
      "    <item>",
      `      <title>${xml(post.title)}</title>`,
      `      <link>${xml(url)}</link>`,
      `      <guid>${xml(url)}</guid>`,
      `      <pubDate>${xml(published)}</pubDate>`,
      "    </item>",
    ].join("\n")
  }).join("\n")

  return `<?xml version="1.0" encoding="UTF-8"?>
<rss version="2.0">
  <channel>
    <title>Exceptionless</title>
    <link>${siteUrl}/</link>
    <description>Exceptionless news, release notes, and engineering updates.</description>
    <lastBuildDate>${updated}</lastBuildDate>
${items}
  </channel>
</rss>
`
}

function renderSitemap(routes: string[]): string {
  const urls = routes
    .filter((route) => route !== "/404.html")
    .map((route) => `  <url><loc>${xml(siteUrl + route)}</loc></url>`)
    .join("\n")

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls}
</urlset>
`
}

function renderRobots(): string {
  return `User-agent: *
Allow: /

Sitemap: ${siteUrl}/sitemap.xml
`
}

function renderLlms(pages: ContentPage[], includeBodies: boolean): string {
  const lines = [
    "# Exceptionless Documentation",
    "",
    "Exceptionless is a real-time error monitoring platform for .NET, JavaScript, and self-hosted deployments.",
    "",
    "## Pages",
    "",
  ]

  for (const page of pages) {
    lines.push(`- [${page.title}](${siteUrl}${page.url})`)
    if (includeBodies) {
      lines.push("")
      lines.push(llmsMarkdownBody(page.sourcePath).trim())
      lines.push("")
    }
  }

  return `${lines.join("\n")}\n`
}

function renderRedirects(aliases: RedirectAlias[]): string {
  return [
    "/docs/index /docs/ 301",
    "/news/index /news/ 301",
    ...aliases.map((alias) => `${alias.from} ${alias.to} 301`),
    "",
  ].join("\n")
}

function renderRedirectPage(target: string): string {
  return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta http-equiv="refresh" content="0; url=${target}">
  <link rel="canonical" href="${siteUrl}${target}">
  <title>Redirecting - Exceptionless</title>
</head>
<body>
  <p><a href="${target}">Redirecting</a></p>
</body>
</html>
`
}

function llmsMarkdownBody(path: string): string {
  return stripNonDocsInternalLinks(markdownBody(path))
}

function markdownBody(path: string): string {
  try {
    return Deno.readTextFileSync(path).replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n?/, "")
  } catch {
    return ""
  }
}

function stripNonDocsInternalLinks(markdown: string): string {
  return markdown
    .replace(/(?<!!)(\[([^\]]+)\]\(([^)]+)\))/g, (match, _full: string, label: string, target: string) => {
      return isNonDocsInternalPageLink(target) ? label : match
    })
    .replace(/<((?:https?:\/\/)?exceptionless\.com\/[^>]+)>/gi, (match, target: string) => {
      return isNonDocsInternalPageLink(target) ? target : match
    })
}

function isNonDocsInternalPageLink(target: string): boolean {
  const rawHref = target.trim().split(/\s+/, 1)[0]?.replace(/^<|>$/g, "") ?? ""
  if (!rawHref) {
    return false
  }

  let url: URL
  try {
    url = new URL(rawHref, siteUrl)
  } catch {
    return false
  }

  return url.origin === siteUrl && !url.pathname.startsWith("/docs/") && !url.pathname.startsWith("/assets/")
}

function shouldSkipHref(target: string): boolean {
  return !target ||
    target.startsWith("#") ||
    target.startsWith("mailto:") ||
    target.startsWith("tel:") ||
    target.startsWith("javascript:") ||
    target.startsWith("data:") ||
    target.startsWith("http://") ||
    target.startsWith("https://") ||
    target.startsWith("//")
}

function existsLocalRoute(pathname: string, root: string): boolean {
  const cleanPath = pathname.replace(/^\/+/, "")
  const candidates = pathname.endsWith("/") ? [`${root}/${cleanPath}index.html`] : [
    `${root}/${cleanPath}`,
    `${root}/${cleanPath}/index.html`,
    `${root}/${cleanPath}.html`,
  ]

  return candidates.some((candidate) => {
    try {
      return Deno.statSync(candidate).isFile
    } catch {
      return false
    }
  })
}

function routeForPage(path: string, root: string): string {
  const relative = slash(path).slice(root.length + 1)
  if (relative.endsWith("/index.html")) {
    return `/${relative.slice(0, -"index.html".length)}`
  }

  return `/${relative}`
}

function stripHtmlComments(html: string): string {
  return html.replace(/<!--[\s\S]*?-->/g, "")
}

function ensureTrailingSlash(pathname: string): string {
  return pathname.endsWith("/") ? pathname : `${pathname}/`
}

function isRootSlug(pathname: string): boolean {
  return /^\/[a-z0-9][a-z0-9-]*\/$/i.test(pathname)
}

function xml(value: string): string {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
}

function slash(path: string): string {
  return path.replaceAll("\\", "/")
}
