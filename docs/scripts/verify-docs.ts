/**
 * Verifies the docs rewrite rules against the built site and the page sources:
 * - every URL in docs-urls.txt still resolves to a page or a redirect stub,
 * - docs_nav.yml lists exactly the published docs pages,
 * - every docs page has a title, a short description, a known plan, and `sources` paths that exist,
 * - published pages use one API hostname (see the constants below).
 *
 * Run it after `deno task build`.
 */
import { parse } from "lume/deps/yaml.ts"
import { navUrls, parseDocsNav } from "./docs-nav.ts"

/** The only hostname for the hosted API. The value lives in _data/site.json as `apiUrl`. */
const site = JSON.parse(Deno.readTextFileSync("_data/site.json")) as { apiUrl: string; appUrl: string }
const apiHostname = new URL(site.apiUrl).hostname
const appHostname = new URL(site.appUrl).hostname
/** Hostnames that must never appear in published pages outside /news/. */
const bannedHostnames = ["collector.exceptionless.io"]
/** Paths that belong to the API. The web app hostname must not serve them in docs. */
const apiPathsOnAppHost = ["/api", "/docs"]
/** Routes allowed to use an API path on the app host. The contact form posts to the app origin that allows its CORS. */
const appHostApiRoutes = new Set(["/contact/"])
/**
 * The hosted OAuth resource identifier for the REST API. It names the resource on the configured origin (BaseURL,
 * because production doesn't set ApiUrl); it isn't a URL that requests are sent to.
 */
const appHostOAuthResource = `https://${appHostname}/api/v2`

const siteRoot = "_site"
const repoRoot = ".."
const maxDescriptionLength = 160
const externalSourcePrefixes = ["Exceptionless.Net:", "Exceptionless.JavaScript:"]

const errors: string[] = []

const navYaml = await Deno.readTextFile("_data/docs_nav.yml")
const plans = (JSON.parse(await Deno.readTextFile("_data/plans.json")) as { name: string }[]).map((plan) => plan.name)

await verifyDocsUrls()
await verifyNav()
await verifyFrontmatter()
await verifyHostnames()

if (errors.length) {
  console.error(`Docs verification failed with ${errors.length} problem(s):`)
  for (const error of errors) {
    console.error(`- ${error}`)
  }
  Deno.exit(1)
}

console.log("Docs verification passed.")

/** Every URL published before the rewrite must still resolve, as a page or a redirect stub. */
async function verifyDocsUrls(): Promise<void> {
  const urls = (await Deno.readTextFile("scripts/docs-urls.txt"))
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith("#"))

  for (const url of urls) {
    const path = url.endsWith("/") ? `${siteRoot}${url}index.html` : `${siteRoot}${url}`
    if (!isFile(path)) {
      errors.push(`${url}: no longer resolves (expected ${path}). Keep the page or add a redirectFrom.`)
    }
  }
}

async function verifyNav(): Promise<void> {
  let listed: string[]
  try {
    listed = navUrls(parseDocsNav(navYaml))
  } catch (error) {
    errors.push(error instanceof Error ? error.message : String(error))
    return
  }

  const published = new Set(await publishedDocsRoutes())
  const listedSet = new Set(listed)

  for (const url of listed) {
    if (!published.has(url)) {
      errors.push(`docs_nav.yml lists ${url}, which is not a published page.`)
    } else if (!isFile(`${siteRoot}${markdownPath(url)}`)) {
      errors.push(`docs_nav.yml lists ${url}, but its Markdown copy ${markdownPath(url)} is missing.`)
    }
  }

  for (const route of published) {
    if (!listedSet.has(route)) {
      errors.push(`${route} is published but missing from docs_nav.yml.`)
    }
  }
}

/** Docs routes that are real pages: the built HTML files that are not redirect stubs. */
async function publishedDocsRoutes(): Promise<string[]> {
  const routes: string[] = []
  for await (const path of walk(`${siteRoot}/docs`)) {
    if (!path.endsWith("/index.html")) {
      continue
    }

    const html = await Deno.readTextFile(path)
    if (html.includes('http-equiv="refresh"')) {
      continue
    }

    routes.push(path.slice(siteRoot.length, -"index.html".length))
  }

  return routes.sort()
}

async function verifyFrontmatter(): Promise<void> {
  for await (const path of walk("docs")) {
    if (!path.endsWith(".md")) {
      continue
    }

    const text = await Deno.readTextFile(path)
    const match = text.match(/^---\r?\n([\s\S]*?)\r?\n---/)
    if (!match) {
      errors.push(`${path}: missing frontmatter.`)
      continue
    }

    const data = parse(match[1]) as Record<string, unknown> | null
    if (!data || data.hidden === true || data.draft === true || data.url === false) {
      continue
    }

    if (typeof data.title !== "string" || !data.title.trim()) {
      errors.push(`${path}: missing title.`)
    }

    if (typeof data.description !== "string" || !data.description.trim()) {
      errors.push(`${path}: missing description.`)
    } else if (data.description.length > maxDescriptionLength) {
      errors.push(`${path}: description is ${data.description.length} characters (max ${maxDescriptionLength}).`)
    }

    if (data.plan !== undefined && (typeof data.plan !== "string" || !plans.includes(data.plan))) {
      errors.push(`${path}: plan ${JSON.stringify(data.plan)} is not in _data/plans.json (${plans.join(", ")}).`)
    }

    if (data.sources !== undefined) {
      if (!Array.isArray(data.sources) || data.sources.some((source) => typeof source !== "string")) {
        errors.push(`${path}: sources must be a list of paths.`)
        continue
      }

      for (const source of data.sources as string[]) {
        if (externalSourcePrefixes.some((prefix) => source.startsWith(prefix))) {
          continue
        }

        if (!exists(`${repoRoot}/${source}`)) {
          errors.push(`${path}: source ${source} does not exist in the repository.`)
        }
      }
    }
  }
}

/** Published pages (not news, not redirect stubs) use the API hostname, never the old collector or API paths on the app host. */
async function verifyHostnames(): Promise<void> {
  const apiPathPattern = new RegExp(
    `${appHostname.replaceAll(".", "\\.")}(?::\\d+)?(${
      apiPathsOnAppHost.map((path) => `\\${path}`).join("|")
    })(?![\\w-])`,
    "g",
  )

  for await (const path of walk(siteRoot)) {
    const relative = path.slice(siteRoot.length)
    if (relative.startsWith("/news/") || !/\.(?:html|md|txt)$/.test(relative)) {
      continue
    }

    const route = relative.replace(/index\.html$/, "")
    const allowAppHostApi = appHostApiRoutes.has(route)

    const text = await Deno.readTextFile(path)
    if (text.includes('http-equiv="refresh"') && text.length < 2000) {
      continue
    }

    for (const hostname of bannedHostnames) {
      if (text.includes(hostname)) {
        errors.push(`${relative}: mentions ${hostname}. Use ${site.apiUrl} for the hosted API.`)
      }
    }

    const checkedText = text.replaceAll(appHostOAuthResource, "")
    for (const match of allowAppHostApi ? [] : checkedText.matchAll(apiPathPattern)) {
      errors.push(`${relative}: ${appHostname}${match[1]} is an API path. Use ${apiHostname}${match[1]}.`)
      break
    }
  }
}

function markdownPath(url: string): string {
  return url === "/docs/" ? "/docs.md" : `${url.replace(/\/$/, "")}.md`
}

function isFile(path: string): boolean {
  try {
    return Deno.statSync(path).isFile
  } catch {
    return false
  }
}

function exists(path: string): boolean {
  try {
    Deno.statSync(path)
    return true
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
