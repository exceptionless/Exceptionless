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
  const nav = parseDocsNav(yaml)
  assertEquals(navUrls(nav), [
    "/docs/",
    "/docs/getting-started/",
    "/docs/clients/",
    "/docs/clients/dotnet/",
    "/docs/clients/dotnet/aspnet-core/",
    "/docs/clients/dotnet/guides/one/",
    "/docs/clients/custom-clients/",
  ])
  assertEquals(sectionPages(nav, "Get started"), ["/docs/", "/docs/getting-started/"])
  assertEquals(flattenNav(nav)[5].groups.map((group) => group.title), [".NET", "Guides"])
})

Deno.test("nav: previous and next skip entries with no page yet", () => {
  const nav = parseDocsNav(yaml)
  assertEquals(pagerFor(nav, "/docs/clients/dotnet/aspnet-core/", titleFor), {
    previous: { url: "/docs/clients/dotnet/", title: ".NET" },
    next: { url: "/docs/clients/custom-clients/", title: "Other languages" },
  })
  assertEquals(pagerFor(nav, "/docs/", titleFor).previous, undefined)
  assertEquals(pagerFor(nav, "/docs/missing/", titleFor), {})
})

Deno.test("nav: breadcrumbs come from the section and the groups", () => {
  const nav = parseDocsNav(yaml)
  assertEquals(crumbsFor(nav, "/docs/clients/dotnet/aspnet-core/", titleFor), [
    { name: "Send data", url: "/docs/clients/" },
    { name: ".NET", url: "/docs/clients/dotnet/" },
  ])
  assertEquals(crumbsFor(nav, "/docs/clients/dotnet/", titleFor), [{ name: "Send data", url: "/docs/clients/" }])
  assertEquals(crumbsFor(nav, "/docs/", titleFor), [])
})

Deno.test("nav: client overview pages are the clients index and each language overview", () => {
  assertEquals(clientOverviewUrls(parseDocsNav(yaml)), ["/docs/clients/", "/docs/clients/dotnet/"])
})

Deno.test("nav: duplicates, bad URLs, and unknown keys fail", () => {
  assertThrows(
    () => parseDocsNav("- section: A\n  items:\n    - /docs/a/\n    - /docs/a/\n"),
    Error,
    "listed more than once",
  )
  assertThrows(() => parseDocsNav("- section: A\n  items:\n    - /docs/a\n"), Error, "must be a /docs/ URL")
  assertThrows(() => parseDocsNav("- section: A\n  items:\n    - /other/\n"), Error, "must be a /docs/ URL")
  assertThrows(() => parseDocsNav("- section: A\n  items: []\n  extra: 1\n"), Error, "unknown keys")
  assertThrows(() => parseDocsNav("- section: A\n  items:\n    - 5\n"), Error, "neither a URL nor a group")
})

Deno.test("nav: the real docs_nav.yml has the eight sections in order", async () => {
  const nav = parseDocsNav(await Deno.readTextFile("_data/docs_nav.yml"))
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
