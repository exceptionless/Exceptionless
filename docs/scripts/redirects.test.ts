import {
  collectRedirects,
  markdownPathFor,
  redirectTarget,
  renderRedirectHtml,
  renderRedirectMarkdown,
} from "./redirects.ts"
import { assert, assertEquals, assertStringIncludes, assertThrows } from "./test-helpers.ts"

const stacking = {
  url: "/docs/concepts/stacking/",
  title: "Stacking",
  sourcePath: "docs/concepts/stacking.md",
  redirectFrom: ["/docs/manual-stacking/", { from: "/docs/bulk-actions/", anchor: "bulk-actions" }],
}

Deno.test("redirects: strings and { from, anchor } entries are collected and sorted", () => {
  const redirects = collectRedirects([stacking, { url: "/docs/", title: "Docs", sourcePath: "docs/index.md" }])
  assertEquals(redirects.map((redirect) => [redirect.from, redirect.to, redirect.anchor]), [
    ["/docs/bulk-actions/", "/docs/concepts/stacking/", "bulk-actions"],
    ["/docs/manual-stacking/", "/docs/concepts/stacking/", undefined],
  ])
  assertEquals(redirects[0].toTitle, "Stacking")
})

Deno.test("redirects: Vento pages can declare redirects too", () => {
  const redirects = collectRedirects([{
    url: "/why/",
    title: "Why",
    sourcePath: "why.vto",
    redirectFrom: ["/docs/comparison/"],
  }])
  assertEquals(redirects.map((redirect) => redirectTarget(redirect)), ["/why/"])
})

Deno.test("redirects: a path that collides with a real page or its Markdown copy fails the build", () => {
  const manualStacking = {
    url: "/docs/manual-stacking/",
    title: "Manual stacking",
    sourcePath: "docs/manual-stacking.md",
  }
  assertThrows(() => collectRedirects([stacking, manualStacking]), Error, "collides with the real page")
  assertThrows(
    () => collectRedirects([stacking, { url: "/docs/bulk-actions/", sourcePath: "docs/bulk-actions.md" }]),
    Error,
    "collides with the real page",
  )
})

Deno.test("redirects: two pages cannot claim the same old path", () => {
  const other = { url: "/docs/other/", sourcePath: "docs/other.md", redirectFrom: ["/docs/manual-stacking/"] }
  assertThrows(() => collectRedirects([stacking, other]), Error, "already declared by docs/concepts/stacking.md")
})

Deno.test("redirects: malformed entries are rejected with the source file", () => {
  const page = (redirectFrom: unknown) => [{ url: "/docs/new/", sourcePath: "docs/new.md", redirectFrom }]
  assertThrows(() => collectRedirects(page("/docs/old/")), Error, "redirectFrom must be a list")
  assertThrows(() => collectRedirects(page(["docs/old/"])), Error, "must be a site path")
  assertThrows(() => collectRedirects(page(["/docs/old"])), Error, "must be a site path")
  assertThrows(() => collectRedirects(page(["/docs/old.md"])), Error, "must be a site path")
  assertThrows(() => collectRedirects(page([{ from: "/docs/old/", anchor: "#x" }])), Error, 'without "#"')
  assertThrows(() => collectRedirects(page([{ from: "/docs/old/", extra: 1 }])), Error, "unknown keys")
  assertThrows(() => collectRedirects(page(["/docs/new/"])), Error, "page's own URL")
})

Deno.test("redirect stub: HTML redirects, keeps the hash, and is not indexable", () => {
  const html = renderRedirectHtml({ to: "/docs/concepts/stacking/", anchor: "manual-stacking" })
  assertStringIncludes(html, '<meta http-equiv="refresh" content="0; url=/docs/concepts/stacking/#manual-stacking">')
  assertStringIncludes(html, '<link rel="canonical" href="https://exceptionless.com/docs/concepts/stacking/">')
  assertStringIncludes(html, '<meta name="robots" content="noindex">')
  assertStringIncludes(html, 'location.replace("/docs/concepts/stacking/" + (location.hash || "#manual-stacking"))')
  assertStringIncludes(html, '<a href="/docs/concepts/stacking/#manual-stacking">')
})

Deno.test("redirect stub: without an anchor the script keeps only the visitor's hash", () => {
  const html = renderRedirectHtml({ to: "/why/" })
  assertStringIncludes(html, 'location.replace("/why/" + (location.hash || ""))')
  assertStringIncludes(html, 'content="0; url=/why/"')
})

Deno.test("redirect stub: Markdown says where the page moved and links the new .md URL", () => {
  const [redirect] = collectRedirects([stacking]).filter((candidate) => candidate.from === "/docs/bulk-actions/")
  const markdown = renderRedirectMarkdown(redirect)
  assertStringIncludes(markdown, "https://exceptionless.com/docs/bulk-actions/ moved to")
  assertStringIncludes(markdown, "[Stacking](https://exceptionless.com/docs/concepts/stacking.md#bulk-actions)")

  const [why] = collectRedirects([{ url: "/why/", title: "Why Exceptionless?", redirectFrom: ["/docs/comparison/"] }])
  assertStringIncludes(renderRedirectMarkdown(why), "(https://exceptionless.com/why/)")
})

Deno.test("Markdown paths follow the docs convention", () => {
  assertEquals(markdownPathFor("/docs/"), "/docs.md")
  assertEquals(markdownPathFor("/docs/manual-stacking/"), "/docs/manual-stacking.md")
  assert(markdownPathFor("/docs/clients/dotnet/") === "/docs/clients/dotnet.md")
})
