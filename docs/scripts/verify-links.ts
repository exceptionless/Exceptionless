type BrokenReference = {
  page: string
  target: string
  attribute: string
  reason: string
}

const siteRoot = "_site"
const broken: BrokenReference[] = []
const htmlCache = new Map<string, string>()
const anchorsCache = new Map<string, Set<string>>()

for await (const page of walk(siteRoot)) {
  if (page.endsWith(".html")) {
    const html = stripHtmlComments(await Deno.readTextFile(page))
    htmlCache.set(page, html)
    verifyHtmlReferences(page, html)
  } else if (page.endsWith(".css")) {
    verifyCssReferences(page, await Deno.readTextFile(page), "css")
  }
}

if (broken.length > 0) {
  console.error(`Found ${broken.length} broken local links or assets:`)
  for (const item of broken) {
    console.error(`${item.page}: ${item.attribute}=\"${item.target}\" (${item.reason})`)
  }

  Deno.exit(1)
}

console.log("No broken local links, anchors, or assets found.")

function verifyHtmlReferences(page: string, html: string): void {
  const attributePattern = /\b(href|src)=(["'])([^"']+)\2/gi
  for (const match of html.matchAll(attributePattern)) {
    verifyTarget(page, match[3], match[1])
  }

  const srcsetPattern = /\bsrcset=(["'])([^"']+)\1/gi
  for (const match of html.matchAll(srcsetPattern)) {
    for (const candidate of match[2].split(",")) {
      const target = candidate.trim().split(/\s+/)[0]
      verifyTarget(page, target, "srcset")
    }
  }

  const styleAttributePattern = /\bstyle=(["'])([^"']+)\1/gi
  for (const match of html.matchAll(styleAttributePattern)) {
    verifyCssReferences(page, match[2], "style")
  }

  const styleTagPattern = /<style\b[^>]*>([\s\S]*?)<\/style>/gi
  for (const match of html.matchAll(styleTagPattern)) {
    verifyCssReferences(page, match[1], "style")
  }
}

function verifyCssReferences(page: string, css: string, attribute: string): void {
  const urlPattern = /url\(\s*(["']?)(.*?)\1\s*\)/gi
  for (const match of css.matchAll(urlPattern)) {
    verifyTarget(page, match[2], attribute)
  }
}

function verifyTarget(page: string, target: string, attribute: string): void {
  if (shouldSkip(target)) {
    return
  }

  const url = new URL(target, `https://local${urlPathForSource(page)}`)
  if (url.origin !== "https://local") {
    return
  }

  const localPath = findLocalPath(decodeURIComponent(url.pathname))
  if (!localPath) {
    broken.push({ page: slash(page), target, attribute, reason: "missing target" })
    return
  }

  if (url.hash && localPath.endsWith(".html") && !hasAnchor(localPath, url.hash)) {
    broken.push({ page: slash(page), target, attribute, reason: "missing anchor" })
  }
}

function shouldSkip(target: string): boolean {
  const value = target.trim()
  return !value ||
    value === "#" ||
    value.startsWith("mailto:") ||
    value.startsWith("tel:") ||
    value.startsWith("javascript:") ||
    value.startsWith("data:") ||
    value.startsWith("http://") ||
    value.startsWith("https://") ||
    value.startsWith("//")
}

function findLocalPath(pathname: string): string | undefined {
  for (const candidate of localPathCandidates(pathname)) {
    try {
      if (Deno.statSync(candidate).isFile) {
        return slash(candidate)
      }
    } catch {
      // Try the next clean URL candidate.
    }
  }

  return undefined
}

function localPathCandidates(pathname: string): string[] {
  const cleanPath = pathname.replace(/^\/+/, "")
  if (pathname.endsWith("/")) {
    return [`${siteRoot}/${cleanPath}index.html`]
  }

  return [
    `${siteRoot}/${cleanPath}`,
    `${siteRoot}/${cleanPath}/index.html`,
    `${siteRoot}/${cleanPath}.html`,
  ]
}

function hasAnchor(htmlPath: string, hash: string): boolean {
  const anchors = anchorsForPage(htmlPath)
  const fragment = decodeFragment(hash)
  return anchors.has(fragment)
}

function anchorsForPage(htmlPath: string): Set<string> {
  let anchors = anchorsCache.get(htmlPath)
  if (anchors) {
    return anchors
  }

  const html = htmlCache.get(htmlPath) ?? stripHtmlComments(Deno.readTextFileSync(htmlPath))
  anchors = new Set([""])

  const attributePattern = /\b(?:id|name)=(["'])([^"']+)\1/gi
  for (const match of html.matchAll(attributePattern)) {
    anchors.add(decodeHtmlAttribute(match[2]))
  }

  anchorsCache.set(htmlPath, anchors)
  return anchors
}

function decodeFragment(hash: string): string {
  const raw = hash.replace(/^#/, "")
  try {
    return decodeURIComponent(raw)
  } catch {
    return raw
  }
}

function decodeHtmlAttribute(value: string): string {
  return value
    .replaceAll("&amp;", "&")
    .replaceAll("&quot;", '"')
    .replaceAll("&#39;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
}

function urlPathForSource(path: string): string {
  const relative = slash(path).slice(siteRoot.length + 1)
  if (relative.endsWith("/index.html")) {
    return `/${relative.slice(0, -"index.html".length)}`
  }

  return `/${relative}`
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

function stripHtmlComments(html: string): string {
  return html.replace(/<!--[\s\S]*?-->/g, "")
}

function slash(path: string): string {
  return path.replaceAll("\\", "/")
}
