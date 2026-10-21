/**
 * Verifies the built site keeps the metadata search engines and AI assistants rely on:
 * unique titles and descriptions, canonical URLs, one H1, valid JSON-LD, Markdown copies of docs, and llms.txt links.
 */
const siteRoot = "_site"
const siteUrl = "https://exceptionless.com"
const maxTitleLength = 70
const descriptionLength = { min: 50, max: 200 }

const errors: string[] = []
const titles = new Map<string, string[]>()
const descriptions = new Map<string, string[]>()
const indexableRoutes = new Set<string>()

for await (const path of walk(siteRoot)) {
  if (!path.endsWith(".html")) {
    continue
  }

  const html = await Deno.readTextFile(path)
  const route = routeForPath(path)
  if (html.includes('http-equiv="refresh"') || /<meta name="robots" content="[^"]*noindex/.test(html)) {
    continue
  }

  indexableRoutes.add(route)
  const title = decode(html.match(/<title>([^<]*)<\/title>/)?.[1] ?? "")
  const description = decode(html.match(/<meta name="description" content="([^"]*)"/)?.[1] ?? "")
  const canonical = html.match(/<link rel="canonical" href="([^"]*)"/)?.[1]
  const h1Count = (stripCode(html).match(/<h1[\s>]/g) ?? []).length
  const isNews = route.startsWith("/news/") && route !== "/news/" && !route.startsWith("/news/page/")

  if (!title) {
    errors.push(`${route}: missing <title>`)
  } else if (title.length > maxTitleLength && !isNews) {
    errors.push(`${route}: title is ${title.length} characters (max ${maxTitleLength}): ${title}`)
  }

  if (!description) {
    errors.push(`${route}: missing meta description`)
  } else if (
    !isNews && (description.length < descriptionLength.min || description.length > descriptionLength.max)
  ) {
    errors.push(`${route}: description is ${description.length} characters: ${description}`)
  }

  if (canonical !== `${siteUrl}${route}`) {
    errors.push(`${route}: canonical is ${canonical ?? "missing"}`)
  }

  if (h1Count !== 1) {
    errors.push(`${route}: has ${h1Count} <h1> elements`)
  }

  const image = html.match(/<meta property="og:image" content="([^"]*)"/)?.[1]
  if (!image?.startsWith(siteUrl) || !exists(`${siteRoot}${image.slice(siteUrl.length)}`)) {
    errors.push(`${route}: og:image ${image ?? "missing"} does not exist`)
  }

  const jsonLd = [...html.matchAll(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g)]
  if (!jsonLd.length) {
    errors.push(`${route}: missing JSON-LD`)
  }

  for (const [, json] of jsonLd) {
    try {
      JSON.parse(json)
    } catch (error) {
      errors.push(`${route}: invalid JSON-LD (${error instanceof Error ? error.message : error})`)
    }
  }

  const markdown = html.match(/<link rel="alternate" type="text\/markdown"[^>]*href="([^"]*)"/)?.[1]
  if (route.startsWith("/docs/") && (!markdown || !exists(`${siteRoot}${markdown}`))) {
    errors.push(`${route}: Markdown alternate ${markdown ?? "missing"} does not exist`)
  }

  if (!isNews) {
    titles.set(title, [...(titles.get(title) ?? []), route])
    descriptions.set(description, [...(descriptions.get(description) ?? []), route])
  }
}

for (const [title, routes] of titles) {
  if (routes.length > 1 && !routes.every((route) => route.startsWith("/news/page/") || route === "/news/")) {
    errors.push(`Duplicate title "${title}": ${routes.join(", ")}`)
  }
}

for (const [description, routes] of descriptions) {
  if (routes.length > 1 && !routes.every((route) => route.startsWith("/news/page/") || route === "/news/")) {
    errors.push(`Duplicate description "${description}": ${routes.join(", ")}`)
  }
}

const sitemap = await Deno.readTextFile(`${siteRoot}/sitemap.xml`)
const sitemapRoutes = new Set([...sitemap.matchAll(/<loc>([^<]*)<\/loc>/g)].map(([, loc]) => loc.slice(siteUrl.length)))
for (const route of indexableRoutes) {
  if (!sitemapRoutes.has(route)) {
    errors.push(`${route}: missing from sitemap.xml`)
  }
}

for (const route of sitemapRoutes) {
  if (!indexableRoutes.has(route)) {
    errors.push(`sitemap.xml lists ${route}, which is not an indexable page`)
  }
}

for (const file of ["llms.txt", "llms-full.txt"]) {
  const text = await Deno.readTextFile(`${siteRoot}/${file}`)
  for (const [, url] of text.matchAll(/\]\((https:\/\/exceptionless\.com[^)\s]*)\)/g)) {
    const path = url.slice(siteUrl.length).replace(/[#?].*$/, "")
    if (!exists(`${siteRoot}${path}`) && !exists(`${siteRoot}${path.replace(/\/?$/, "/")}index.html`)) {
      errors.push(`${file}: link ${url} does not exist`)
    }
  }
}

const robots = await Deno.readTextFile(`${siteRoot}/robots.txt`)
if (!robots.includes(`Sitemap: ${siteUrl}/sitemap.xml`) || /Disallow:\s*\/\s*$/m.test(robots)) {
  errors.push("robots.txt must allow crawling and reference the sitemap")
}

if (errors.length) {
  console.error(`SEO verification failed with ${errors.length} problem(s):`)
  for (const error of errors) {
    console.error(`- ${error}`)
  }
  Deno.exit(1)
}

console.log(`SEO verification passed for ${indexableRoutes.size} pages.`)

function routeForPath(path: string): string {
  const relative = path.slice(siteRoot.length).replaceAll("\\", "/")
  return relative.endsWith("/index.html") ? relative.slice(0, -"index.html".length) : relative
}

function stripCode(html: string): string {
  return html.replace(/<(pre|code|script)\b[\s\S]*?<\/\1>/g, "")
}

function decode(value: string): string {
  return value
    .replaceAll("&quot;", '"')
    .replaceAll("&#39;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
    .replaceAll("&amp;", "&")
    .trim()
}

function exists(path: string): boolean {
  try {
    return Deno.statSync(path).isFile
  } catch {
    return false
  }
}

async function* walk(dir: string): AsyncGenerator<string> {
  for await (const entry of Deno.readDir(dir)) {
    const path = `${dir}/${entry.name}`
    if (entry.isDirectory) {
      yield* walk(path)
    } else if (entry.isFile) {
      yield path
    }
  }
}
