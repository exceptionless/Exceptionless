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

const siteUrl = "https://exceptionless.com"
const generated = JSON.parse(await Deno.readTextFile("_site/site-data.json")) as GeneratedData

await copyDir("public", "_site")
await copyDirIfExists("docs/img", "_site/docs/img")
await copyNonMarkdownFiles("news", "_site/news")
await rewriteHtmlReferences("_site")
await rewriteNewsIndex(generated.newsPosts)

const routes = await collectHtmlRoutes("_site")
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
  return html
    .replace(/<[^>]*>/g, "")
    .replace(/&amp;/g, "&")
    .replace(/&quot;/g, '"')
    .replace(/&#39;|&apos;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/\s+/g, " ")
    .trim()
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

  const articles: string[] = []
  for (const post of posts.slice(0, 10)) {
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

  await Deno.writeTextFile(indexPath, html.replace("<!-- NEWS_INDEX_PLACEHOLDER -->", articles.join("\n\n")))
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

async function collectRedirectAliases(root: string, posts: ContentPage[]): Promise<RedirectAlias[]> {
  const aliases = new Map<string, string>()
  const postAliases = buildPostAliasMap(posts)

  aliases.set("/contact/", "/")

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
