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
  // Arrange
  const pages = [stacking, { url: "/docs/", title: "Docs", sourcePath: "docs/index.md" }]

  // Act
  const redirects = collectRedirects(pages)

  // Assert
  assertEquals(redirects.map((redirect) => [redirect.from, redirect.to, redirect.anchor]), [
    ["/docs/bulk-actions/", "/docs/concepts/stacking/", "bulk-actions"],
    ["/docs/manual-stacking/", "/docs/concepts/stacking/", undefined],
  ])
  assertEquals(redirects[0].toTitle, "Stacking")
})

Deno.test("redirects: Vento pages can declare redirects too", () => {
  // Arrange
  const pages = [{
    url: "/why/",
    title: "Why",
    sourcePath: "why.vto",
    redirectFrom: ["/docs/comparison/"],
  }]

  // Act
  const redirects = collectRedirects(pages)

  // Assert
  assertEquals(redirects.map((redirect) => redirectTarget(redirect)), ["/why/"])
})

Deno.test("redirects: a path that collides with a real page or its Markdown copy fails the build", () => {
  // Arrange
  const manualStacking = {
    url: "/docs/manual-stacking/",
    title: "Manual stacking",
    sourcePath: "docs/manual-stacking.md",
  }
  const bulkActions = { url: "/docs/bulk-actions/", sourcePath: "docs/bulk-actions.md" }

  // Act
  const manualCollision = () => collectRedirects([stacking, manualStacking])
  const bulkCollision = () => collectRedirects([stacking, bulkActions])

  // Assert
  assertThrows(manualCollision, Error, "collides with the real page")
  assertThrows(
    bulkCollision,
    Error,
    "collides with the real page",
  )
})

Deno.test("redirects: two pages cannot claim the same old path", () => {
  // Arrange
  const other = { url: "/docs/other/", sourcePath: "docs/other.md", redirectFrom: ["/docs/manual-stacking/"] }

  // Act
  const collect = () => collectRedirects([stacking, other])

  // Assert
  assertThrows(collect, Error, "already declared by docs/concepts/stacking.md")
})

Deno.test("redirects: malformed entries are rejected with the source file", () => {
  // Arrange
  const page = (redirectFrom: unknown) => [{ url: "/docs/new/", sourcePath: "docs/new.md", redirectFrom }]
  const inputs = [
    "/docs/old/",
    ["docs/old/"],
    ["/docs/old"],
    ["/docs/old.md"],
    [{ from: "/docs/old/", anchor: "#x" }],
    [{ from: "/docs/old/", extra: 1 }],
    ["/docs/new/"],
  ]

  // Act
  const attempts = inputs.map((input) => () => collectRedirects(page(input)))

  // Assert
  assertThrows(attempts[0], Error, "redirectFrom must be a list")
  assertThrows(attempts[1], Error, "must be a site path")
  assertThrows(attempts[2], Error, "must be a site path")
  assertThrows(attempts[3], Error, "must be a site path")
  assertThrows(attempts[4], Error, 'without "#"')
  assertThrows(attempts[5], Error, "unknown keys")
  assertThrows(attempts[6], Error, "page's own URL")
})

Deno.test("redirect stub: HTML redirects, keeps the hash, and is not indexable", () => {
  // Arrange
  const redirect = { to: "/docs/concepts/stacking/", anchor: "manual-stacking" }

  // Act
  const html = renderRedirectHtml(redirect)

  // Assert
  assertStringIncludes(html, '<meta http-equiv="refresh" content="0; url=/docs/concepts/stacking/#manual-stacking">')
  assertStringIncludes(html, '<link rel="canonical" href="https://exceptionless.com/docs/concepts/stacking/">')
  assertStringIncludes(html, '<meta name="robots" content="noindex">')
  assertStringIncludes(html, 'location.replace("/docs/concepts/stacking/" + (location.hash || "#manual-stacking"))')
  assertStringIncludes(html, '<a href="/docs/concepts/stacking/#manual-stacking">')
})

Deno.test("redirect stub: without an anchor the script keeps only the visitor's hash", () => {
  // Arrange
  const redirect = { to: "/why/" }

  // Act
  const html = renderRedirectHtml(redirect)

  // Assert
  assertStringIncludes(html, 'location.replace("/why/" + (location.hash || ""))')
  assertStringIncludes(html, 'content="0; url=/why/"')
})

Deno.test("redirect stub: Markdown says where the page moved and links the new .md URL", () => {
  // Arrange
  const [redirect] = collectRedirects([stacking]).filter((candidate) => candidate.from === "/docs/bulk-actions/")
  const [why] = collectRedirects([{ url: "/why/", title: "Why Exceptionless?", redirectFrom: ["/docs/comparison/"] }])

  // Act
  const markdown = renderRedirectMarkdown(redirect)
  const whyMarkdown = renderRedirectMarkdown(why)

  // Assert
  assertStringIncludes(markdown, "https://exceptionless.com/docs/bulk-actions/ moved to")
  assertStringIncludes(markdown, "[Stacking](https://exceptionless.com/docs/concepts/stacking.md#bulk-actions)")

  assertStringIncludes(whyMarkdown, "(https://exceptionless.com/why/)")
})

Deno.test("Markdown paths follow the docs convention", () => {
  // Arrange
  const urls = ["/docs/", "/docs/manual-stacking/", "/docs/clients/dotnet/"]

  // Act
  const paths = urls.map(markdownPathFor)

  // Assert
  assertEquals(paths[0], "/docs.md")
  assertEquals(paths[1], "/docs/manual-stacking.md")
  assert(paths[2] === "/docs/clients/dotnet.md")
})
