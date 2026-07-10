import lume from "lume/mod.ts"
import codeHighlight from "lume/plugins/code_highlight.ts"
import picture from "lume/plugins/picture.ts"
import transformImages from "lume/plugins/transform_images.ts"
import { docsNavHtml, siteDataJson } from "./scripts/site-collections.ts"

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
site.use(picture())
site.use(transformImages())
site.add("public/assets/img/dashboard-2-1024x594.png", "assets/img/dashboard-2-1024x594.png")
site.add("public/assets/img/toexceptionless.png", "assets/img/toexceptionless.png")
site.add("public/assets/img/logs-2.jpg", "assets/img/logs-2.jpg")
site.add("public/assets/img/slider-github.jpg", "assets/img/slider-github.jpg")

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
})

site.data("docsNavHtml", docsNavHtml)
site.data("siteDataJson", siteDataJson)
site.data("copyrightYear", new Date().getFullYear())
site.data("exceptionlessClientScriptSrc", exceptionlessClientScriptSrc())

export default site

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
