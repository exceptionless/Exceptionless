type CandidateCategory = "public" | "docs-img" | "news-asset"

type AssetCandidate = {
  category: CandidateCategory
  sourcePath: string
  outputPath: string
  size: number
}

const siteRoot = "_site"
const keepOutputPaths = new Set([
  "_site/favicon.ico",
])
const staticAssetExtensions = new Set([
  ".avif",
  ".css",
  ".eot",
  ".gif",
  ".ico",
  ".jpeg",
  ".jpg",
  ".js",
  ".json",
  ".mjs",
  ".mov",
  ".mp3",
  ".mp4",
  ".otf",
  ".pdf",
  ".png",
  ".svg",
  ".ttf",
  ".txt",
  ".webm",
  ".webp",
  ".woff",
  ".woff2",
  ".xml",
  ".zip",
])

await ensureSiteExists()

const candidates = await collectAssetCandidates()
const references = await collectReferencedOutputPaths(siteRoot)
const missingOutputs = candidates.filter((candidate) => !existsFile(candidate.outputPath))
const unused = candidates
  .filter((candidate) => !missingOutputs.includes(candidate))
  .filter((candidate) => !keepOutputPaths.has(candidate.outputPath))
  .filter((candidate) => !references.has(candidate.outputPath))
  .sort(compareCandidates)

if (Deno.args.includes("--json")) {
  console.log(JSON.stringify({ unused, missingOutputs }, null, 2))
} else {
  printReport(unused, missingOutputs)
}

function printReport(unused: AssetCandidate[], missingOutputs: AssetCandidate[]): void {
  if (missingOutputs.length) {
    console.log(`Found ${missingOutputs.length} source assets that were not copied to ${siteRoot}:`)
    printGrouped(missingOutputs)
    console.log("")
  }

  if (!unused.length) {
    console.log("No unused copied source assets found.")
    return
  }

  const totalBytes = unused.reduce((sum, candidate) => sum + candidate.size, 0)
  console.log(`Found ${unused.length} unused copied source assets (${formatBytes(totalBytes)}):`)
  printGrouped(unused)
}

function printGrouped(candidates: AssetCandidate[]): void {
  const groups = new Map<CandidateCategory, AssetCandidate[]>()
  for (const candidate of candidates) {
    const group = groups.get(candidate.category) ?? []
    group.push(candidate)
    groups.set(candidate.category, group)
  }

  for (const [category, items] of [...groups.entries()].sort(([a], [b]) => a.localeCompare(b))) {
    const bytes = items.reduce((sum, candidate) => sum + candidate.size, 0)
    console.log(`\n${category} (${items.length}, ${formatBytes(bytes)})`)
    for (const item of items) {
      console.log(`  ${item.sourcePath}`)
    }
  }
}

async function collectAssetCandidates(): Promise<AssetCandidate[]> {
  return [
    ...await collectTreeAssets("public", "", "public"),
    ...await collectTreeAssets("docs/img", "docs/img", "docs-img"),
    ...await collectNewsAssets("news"),
  ].sort(compareCandidates)
}

async function collectTreeAssets(
  sourceRoot: string,
  outputRoot: string,
  category: CandidateCategory,
): Promise<AssetCandidate[]> {
  const candidates: AssetCandidate[] = []

  try {
    if (!(await Deno.stat(sourceRoot)).isDirectory) {
      return candidates
    }
  } catch {
    return candidates
  }

  for await (const path of walk(sourceRoot)) {
    const relative = slash(path).slice(`${slash(sourceRoot)}/`.length)
    candidates.push({
      category,
      sourcePath: slash(path),
      outputPath: slash(`${siteRoot}/${joinUrlPath(outputRoot, relative)}`),
      size: (await Deno.stat(path)).size,
    })
  }

  return candidates
}

async function collectNewsAssets(sourceRoot: string): Promise<AssetCandidate[]> {
  const candidates: AssetCandidate[] = []

  try {
    if (!(await Deno.stat(sourceRoot)).isDirectory) {
      return candidates
    }
  } catch {
    return candidates
  }

  for await (const path of walk(sourceRoot)) {
    if (!isStaticAssetFile(path)) {
      continue
    }

    const relative = slash(path).slice(`${slash(sourceRoot)}/`.length)
    candidates.push({
      category: "news-asset",
      sourcePath: slash(path),
      outputPath: slash(`${siteRoot}/news/${relative}`),
      size: (await Deno.stat(path)).size,
    })
  }

  return candidates
}

async function collectReferencedOutputPaths(root: string): Promise<Set<string>> {
  const references = new Set<string>()

  for await (const path of walk(root)) {
    if (!isTextFile(path)) {
      continue
    }

    const text = stripComments(await Deno.readTextFile(path))
    collectReferencesFromText(path, text, references)
  }

  return references
}

function collectReferencesFromText(sourcePath: string, text: string, references: Set<string>): void {
  const moduleImportPattern = /\b(?:import|export)\s+(?:[^"']*?\s+from\s+)?["']([^"']+)["']/g
  for (const match of text.matchAll(moduleImportPattern)) {
    addReference(sourcePath, match[1], references)
  }

  const attributePattern = /\b(?:href|src|poster|content|data-[\w-]+)=(?:["'])([^"']+)["']/gi
  for (const match of text.matchAll(attributePattern)) {
    addReference(sourcePath, match[1], references)
  }

  const srcsetPattern = /\bsrcset=(?:["'])([^"']+)["']/gi
  for (const match of text.matchAll(srcsetPattern)) {
    for (const candidate of match[1].split(",")) {
      addReference(sourcePath, candidate.trim().split(/\s+/)[0] ?? "", references)
    }
  }

  const cssUrlPattern = /url\(\s*(["']?)(.*?)\1\s*\)/gi
  for (const match of text.matchAll(cssUrlPattern)) {
    addReference(sourcePath, match[2], references)
  }

  const localPathPattern =
    /(?:https?:\/\/(?:www\.)?exceptionless\.com)?\/(?:assets|docs\/img|img|news|favicon\.ico)\/?.*?(?=["'\s<>)\],]|$)/gi
  for (const match of text.matchAll(localPathPattern)) {
    addReference(sourcePath, match[0], references)
  }
}

function addReference(sourcePath: string, rawTarget: string, references: Set<string>): void {
  const target = cleanTarget(decodeHtmlAttribute(rawTarget))
  if (shouldSkip(target)) {
    return
  }

  let url: URL
  try {
    url = new URL(target, `https://local${urlPathForSource(sourcePath)}`)
  } catch {
    return
  }

  if (!isLocalSiteUrl(url)) {
    return
  }

  const pathname = safeDecodeURIComponent(url.pathname).replace(/^\/+/, "")
  if (!pathname) {
    return
  }

  references.add(slash(`${siteRoot}/${pathname}`))
}

function shouldSkip(target: string): boolean {
  const value = target.trim()
  return !value ||
    value === "#" ||
    value.startsWith("#") ||
    value.startsWith("mailto:") ||
    value.startsWith("tel:") ||
    value.startsWith("javascript:") ||
    value.startsWith("data:")
}

function isLocalSiteUrl(url: URL): boolean {
  return url.origin === "https://local" ||
    url.origin === "https://exceptionless.com" ||
    url.origin === "http://exceptionless.com" ||
    url.origin === "https://www.exceptionless.com" ||
    url.origin === "http://www.exceptionless.com"
}

function cleanTarget(value: string): string {
  return value
    .trim()
    .replace(/[),.;]+$/g, "")
}

function decodeHtmlAttribute(value: string): string {
  return value
    .replaceAll("&amp;", "&")
    .replaceAll("&quot;", '"')
    .replaceAll("&#39;", "'")
    .replaceAll("&lt;", "<")
    .replaceAll("&gt;", ">")
}

function safeDecodeURIComponent(value: string): string {
  try {
    return decodeURIComponent(value)
  } catch {
    return value
  }
}

function urlPathForSource(path: string): string {
  const relative = slash(path).slice(siteRoot.length + 1)
  if (relative.endsWith("/index.html")) {
    return `/${relative.slice(0, -"index.html".length)}`
  }

  return `/${relative}`
}

function isStaticAssetFile(path: string): boolean {
  return staticAssetExtensions.has(getExtension(path))
}

function getExtension(path: string): string {
  const fileName = path.toLowerCase()
  const index = fileName.lastIndexOf(".")
  return index === -1 ? "" : fileName.slice(index)
}

function isTextFile(path: string): boolean {
  return /\.(?:html|css|js|json|xml|txt|svg|webmanifest|map)$/i.test(path) || path.endsWith("/_redirects")
}

function stripComments(text: string): string {
  return text
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/\/\*[\s\S]*?\*\//g, "")
}

async function ensureSiteExists(): Promise<void> {
  try {
    if ((await Deno.stat(siteRoot)).isDirectory) {
      return
    }
  } catch {
    // Fall through to the error below.
  }

  console.error(`${siteRoot} does not exist. Run deno task build first.`)
  Deno.exit(1)
}

function existsFile(path: string): boolean {
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
      yield slash(path)
    }
  }
}

function compareCandidates(a: AssetCandidate, b: AssetCandidate): number {
  return a.category.localeCompare(b.category) || a.sourcePath.localeCompare(b.sourcePath)
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`
  }

  const units = ["KB", "MB", "GB"]
  let value = bytes / 1024
  let unitIndex = 0
  while (value >= 1024 && unitIndex < units.length - 1) {
    value = value / 1024
    unitIndex++
  }

  return `${value.toFixed(value >= 10 ? 1 : 2)} ${units[unitIndex]}`
}

function joinUrlPath(...parts: string[]): string {
  return parts
    .filter(Boolean)
    .join("/")
    .replace(/\/+/g, "/")
    .replace(/^\/+/, "")
}

function slash(path: string): string {
  return path.replaceAll("\\", "/")
}
