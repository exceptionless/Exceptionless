import lume from "lume/mod.ts"
import codeHighlight from "lume/plugins/code_highlight.ts"
import { docsBreadcrumbs, docsNavHtml, siteDataJson } from "./scripts/site-collections.ts"
import { defaultSocialImage, structuredData } from "./scripts/structured-data.ts"

const site = lume({
  src: ".",
  dest: "_site",
  location: new URL("https://exceptionless.com/"),
})

site.ignore(
  ".cache",
  ".gitignore",
  ".npmrc",
  "_site",
  "deno.json",
  "deno.lock",
  "docs-rewrite-plan.md",
  "news/index.md",
  "news/2020/2020-11-11-self-hosted-error-monitoring-on-azure.md",
  "package-lock.json",
  "package.json",
  "public",
  "README.md",
  "scripts",
  "serialization-architecture.md",
  "site-migration-plan.md",
  "snapshots",
)

site.use(codeHighlight())

site.hooks.markdownIt((markdownIt: any) => {
  const defaultHeadingOpen = markdownIt.renderer.rules.heading_open ??
    ((tokens: any[], index: number, options: unknown, env: Record<string, unknown>, self: any) => {
      return self.renderToken(tokens, index, options)
    })

  markdownIt.renderer.rules.heading_open = (
    tokens: any[],
    index: number,
    options: unknown,
    env: Record<string, unknown>,
    self: any,
  ) => {
    const token = tokens[index]
    if (!token.attrGet("id")) {
      const content = tokens[index + 1]?.content ?? ""
      token.attrSet("id", uniqueHeadingSlug(content, env))
    }

    return defaultHeadingOpen(tokens, index, options, env, self)
  }

  const defaultImage = markdownIt.renderer.rules.image
  markdownIt.renderer.rules.image = (
    tokens: any[],
    index: number,
    options: unknown,
    env: Record<string, unknown>,
    self: any,
  ) => {
    const token = tokens[index]
    const name = token.attrGet("src")?.match(/^\/assets\/img\/docs\/(.+)-light\.webp$/)?.[1]
    return name ? docsShot(name, token.content) : defaultImage(tokens, index, options, env, self)
  }
})

site.preprocess([".md"], (pages) => {
  for (const page of pages) {
    if (!page.data.description && typeof page.data.content === "string") {
      page.data.description = markdownExcerpt(page.data.content)
    }
  }
})

site.data("docsNavHtml", docsNavHtml)
site.data("docsBreadcrumbs", docsBreadcrumbs)
site.data("structuredData", structuredData)
site.data("defaultSocialImage", defaultSocialImage)
site.data("siteDataJson", siteDataJson)
site.data("copyrightYear", new Date().getFullYear())
site.data("exceptionlessClientScriptSrc", exceptionlessClientScriptSrc())
site.data("siteAssetVersionQuery", siteAssetVersionQuery())
site.data("productShot", productShot)

export default site

type ProductShotOptions = {
  eager?: boolean
  frame?: boolean
  path?: string
  sizes?: string
}

const productShotSizes: Record<string, [number, number]> = {
  stacks: [1440, 900],
  errors: [1440, 900],
  mcp: [1184, 769],
}

/** Renders a light/dark product screenshot pair from public/assets/img/product with lightbox support. */
function productShot(name: string, alt: string, options: ProductShotOptions = {}): string {
  const [width, height] = productShotSizes[name] ?? [1184, 839]
  const sizes = options.sizes ?? "(max-width: 960px) 100vw, 680px"
  const loading = options.eager ? 'fetchpriority="high" decoding="async"' : 'loading="lazy" decoding="async"'
  const images = ["light", "dark"].map((scheme) => {
    const base = `/assets/img/product/${name}-${scheme}`
    return `<img class="only-${scheme}" src="${base}.webp" srcset="${base}.webp ${width}w, ${base}@2x.webp ${
      width * 2
    }w" sizes="${sizes}" width="${width}" height="${height}" alt="${
      escapeAttribute(alt)
    }" data-full="${base}@2x.webp" ${loading}>`
  }).join("")

  const button = `<button class="shot${
    options.frame ? " shot-framed" : ""
  }" type="button" data-lightbox data-caption="${escapeAttribute(alt)}" aria-label="Enlarge screenshot: ${
    escapeAttribute(alt)
  }">${images}</button>`

  if (!options.frame) {
    return button
  }

  return `<div class="browser"><div class="browser-bar" aria-hidden="true"><div class="browser-dots"><span></span><span></span><span></span></div><div class="browser-url">be.exceptionless.io${
    options.path ?? ""
  }</div></div>${button}</div>`
}

/**
 * Renders a docs screenshot written as ![alt](/assets/img/docs/name-light.webp) as a light/dark pair with lightbox
 * support. Each screenshot needs name-light, name-dark, and @2x variants of both in public/assets/img/docs.
 */
function docsShot(name: string, alt: string): string {
  const [width, height] = webpSize(`public/assets/img/docs/${name}-light.webp`)
  const images = ["light", "dark"].map((scheme) => {
    const base = `/assets/img/docs/${name}-${scheme}`
    return `<img class="only-${scheme}" src="${base}.webp" srcset="${base}.webp 1x, ${base}@2x.webp 2x" width="${width}" height="${height}" alt="${
      escapeAttribute(alt)
    }" data-full="${base}@2x.webp" loading="lazy" decoding="async">`
  }).join("")

  return `<button class="shot" type="button" data-lightbox data-caption="${
    escapeAttribute(alt)
  }" aria-label="Enlarge screenshot: ${escapeAttribute(alt)}">${images}</button>`
}

/** Reads the pixel size of a lossy, lossless, or extended WebP image. */
function webpSize(path: string): [number, number] {
  const bytes = Deno.readFileSync(path)
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
  const chunk = new TextDecoder().decode(bytes.subarray(12, 16))
  if (chunk === "VP8X") {
    return [1 + (view.getUint32(24, true) & 0xffffff), 1 + (view.getUint32(27, true) & 0xffffff)]
  }

  if (chunk === "VP8L") {
    const bits = view.getUint32(21, true)
    return [1 + (bits & 0x3fff), 1 + ((bits >> 14) & 0x3fff)]
  }

  return [view.getUint16(26, true) & 0x3fff, view.getUint16(28, true) & 0x3fff]
}

/** Builds a meta description from the first substantial paragraph of a Markdown page. */
function markdownExcerpt(markdown: string, maxLength = 160): string | undefined {
  const paragraphs = markdown
    .replace(/```[\s\S]*?```/g, "")
    .split(/\r?\n\s*\r?\n/)
    .map((block) =>
      block
        .replace(/<[^>]+>/g, " ")
        .replace(/!\[[^\]]*\]\([^)]*\)/g, "")
        .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
        .replace(/[*_`>#|]/g, "")
        .replace(/\s+/g, " ")
        .trim()
    )
    .filter((text) => text.length >= 60 && !/^[-+:]/.test(text))

  const text = paragraphs[0]
  if (!text) {
    return undefined
  }

  if (text.length <= maxLength) {
    return text
  }

  const clipped = text.slice(0, maxLength - 1)
  const sentenceEnd = clipped.lastIndexOf(". ")
  if (sentenceEnd >= 80) {
    return clipped.slice(0, sentenceEnd + 1)
  }

  return `${clipped.slice(0, clipped.lastIndexOf(" ")).replace(/[,;:]$/, "")}…`
}

function escapeAttribute(value: string): string {
  return value.replaceAll("&", "&amp;").replaceAll('"', "&quot;").replaceAll("<", "&lt;")
}

function siteAssetVersionQuery(): string {
  const version = (Deno.env.get("EXCEPTIONLESS_SITE_VERSION") ?? "").trim()
  return version ? `?v=${encodeURIComponent(version)}` : ""
}

function exceptionlessClientScriptSrc(): string {
  const apiKey = (Deno.env.get("EXCEPTIONLESS_SITE_API_KEY") ?? "").trim()
  if (!apiKey) {
    return ""
  }

  const params = new URLSearchParams({ apiKey })
  const serverUrl = (Deno.env.get("EXCEPTIONLESS_SITE_SERVER_URL") ?? "").trim()

  if (serverUrl) {
    params.set("serverUrl", serverUrl)
  }

  const version = (Deno.env.get("EXCEPTIONLESS_SITE_VERSION") ?? "").trim()
  if (version) {
    params.set("version", version)
  }

  return `/assets/js/exceptionless-client.js?${params}`
}

function uniqueHeadingSlug(value: string, env: Record<string, unknown>): string {
  const counts = getHeadingSlugCounts(env)
  const baseSlug = slugifyHeading(value) || "section"
  const count = counts.get(baseSlug) ?? 0
  counts.set(baseSlug, count + 1)
  return count ? `${baseSlug}-${count}` : baseSlug
}

function getHeadingSlugCounts(env: Record<string, unknown>): Map<string, number> {
  const key = "__headingSlugCounts"
  const existing = env[key]
  if (existing instanceof Map) {
    return existing as Map<string, number>
  }

  const counts = new Map<string, number>()
  env[key] = counts
  return counts
}

function slugifyHeading(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/\s+/g, "-")
    .replace(/\./g, "")
    .replace(/[^a-z0-9_-]/g, "")
    .replace(/^-+|-+$/g, "")
}
