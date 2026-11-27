import { clientOverviewUrls, crumbsFor, flattenNav, navUrls, pagerFor, parseDocsNav, sectionPages } from "./docs-nav.ts"
import { assertEquals, assertThrows } from "./test-helpers.ts"

const yaml = `
- section: Get started
  items:
    - /docs/
    - /docs/getting-started/
- section: Send data
  items:
    - /docs/clients/
    - group: .NET
      url: /docs/clients/dotnet/
      items:
        - /docs/clients/dotnet/aspnet-core/
        - group: Guides
          items:
            - /docs/clients/dotnet/guides/one/
    - group: Other languages
      items:
        - /docs/clients/custom-clients/
`

const titles = new Map([
  ["/docs/", "Overview"],
  ["/docs/getting-started/", "Quickstart"],
  ["/docs/clients/", "Choose a client"],
  ["/docs/clients/dotnet/", ".NET"],
  ["/docs/clients/dotnet/aspnet-core/", "ASP.NET Core"],
  ["/docs/clients/custom-clients/", "Other languages"],
])
const titleFor = (url: string) => titles.get(url)

Deno.test("nav: pages flatten in reading order, with a group's overview first", () => {
  // Arrange
  const nav = parseDocsNav(yaml)

  // Act
  const urls = navUrls(nav)
  const pages = sectionPages(nav, "Get started")
  const groups = flattenNav(nav)[5].groups.map((group) => group.title)

  // Assert
  assertEquals(urls, [
    "/docs/",
    "/docs/getting-started/",
    "/docs/clients/",
    "/docs/clients/dotnet/",
    "/docs/clients/dotnet/aspnet-core/",
    "/docs/clients/dotnet/guides/one/",
    "/docs/clients/custom-clients/",
  ])
  assertEquals(pages, ["/docs/", "/docs/getting-started/"])
  assertEquals(groups, [".NET", "Guides"])
})

Deno.test("nav: previous and next skip entries with no page yet", () => {
  // Arrange
  const nav = parseDocsNav(yaml)

  // Act
  const pager = pagerFor(nav, "/docs/clients/dotnet/aspnet-core/", titleFor)
  const first = pagerFor(nav, "/docs/", titleFor)
  const missing = pagerFor(nav, "/docs/missing/", titleFor)

  // Assert
  assertEquals(pager, {
    previous: { url: "/docs/clients/dotnet/", title: ".NET" },
    next: { url: "/docs/clients/custom-clients/", title: "Other languages" },
  })
  assertEquals(first.previous, undefined)
  assertEquals(missing, {})
})

Deno.test("nav: breadcrumbs come from the section and the groups", () => {
  // Arrange
  const nav = parseDocsNav(yaml)

  // Act
  const nested = crumbsFor(nav, "/docs/clients/dotnet/aspnet-core/", titleFor)
  const overview = crumbsFor(nav, "/docs/clients/dotnet/", titleFor)
  const root = crumbsFor(nav, "/docs/", titleFor)

  // Assert
  assertEquals(nested, [
    { name: "Send data", url: "/docs/clients/" },
    { name: ".NET", url: "/docs/clients/dotnet/" },
  ])
  assertEquals(overview, [{ name: "Send data", url: "/docs/clients/" }])
  assertEquals(root, [])
})

Deno.test("nav: client overview pages are the clients index and each language overview", () => {
  // Arrange
  const nav = parseDocsNav(yaml)

  // Act
  const urls = clientOverviewUrls(nav)

  // Assert
  assertEquals(urls, ["/docs/clients/", "/docs/clients/dotnet/"])
})

Deno.test("nav: duplicates, bad URLs, and unknown keys fail", () => {
  // Arrange
  const sources = [
    "- section: A\n  items:\n    - /docs/a/\n    - /docs/a/\n",
    "- section: A\n  items:\n    - /docs/a\n",
    "- section: A\n  items:\n    - /other/\n",
    "- section: A\n  items: []\n  extra: 1\n",
    "- section: A\n  items:\n    - 5\n",
  ]

  // Act
  const parse = sources.map((source) => () => parseDocsNav(source))

  // Assert
  assertThrows(parse[0], Error, "listed more than once")
  assertThrows(parse[1], Error, "must be a /docs/ URL")
  assertThrows(parse[2], Error, "must be a /docs/ URL")
  assertThrows(parse[3], Error, "unknown keys")
  assertThrows(parse[4], Error, "neither a URL nor a group")
})

Deno.test("nav: the real docs_nav.yml has the eight sections in order", async () => {
  // Arrange
  const source = await Deno.readTextFile("_data/docs_nav.yml")

  // Act
  const nav = parseDocsNav(source)

  // Assert
  assertEquals(nav.map((section) => section.title), [
    "Get started",
    "AI",
    "Send data",
    "Use Exceptionless",
    "How it works",
    "Reference",
    "REST API",
    "Self-hosting",
  ])
})
