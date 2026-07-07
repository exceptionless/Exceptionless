# Exceptionless Site Migration Plan

## Primary Goal

Build the marketing, docs, and news site with Lume on Deno. The maintained source should be Markdown, Vento pages for
complex routes, shared Vento layouts, static assets, and small TypeScript helpers that operate on Lume page data.

The normal authoring workflow should be:

1. Add or edit Markdown or a route-level Vento page.
2. Run `deno task build` from `docs/`.
3. Navigation, RSS, sitemap, and docs-only LLMS files update automatically.

Pixel parity matters, but copied HTML is a migration aid, not the long-term authoring model.

## Chosen Stack

- Lume is the SSG, configured by `_config.ts`.
- Deno is the runtime, task runner, formatter, checker, dependency cache, and CI entrypoint.
- Markdown is the default authoring format for docs, news posts, and simple pages.
- Vento is used for shared layouts and complex route pages.
- TypeScript is used only for small Lume-facing collection helpers, postbuild generation, local serving, and
  verification scripts.
- Avoid a Node/npm SSG pipeline for this site.

Commit the site tooling inputs:

- `deno.json`
- `deno.lock`
- `_config.ts`
- `_data/site.json`
- `_includes/layouts/*.vto`
- `scripts/*.ts`
- Markdown and Vento source pages

Do not commit generated `_site`, `.cache`, Deno caches, temporary parity snapshots, or downloaded remote pages unless a
snapshot is intentionally promoted to a regression fixture.

## Current Project Layout

The Lume project root is `docs/`.

```text
docs/
  deno.json
  deno.lock
  _config.ts
  _data/
    site.json
  _includes/
    layouts/
      base.vto              # shared HTML shell, header, footer, metadata, scripts
      docs.vto              # reused by docs Markdown pages
      page.vto              # reused by legal/about/404-style pages
      post.vto              # reused by news posts
  docs/                     # documentation Markdown
    index.md
    getting-started.md
    clients/
      index.md
      dotnet/
      javascript/
  news/                     # dated news Markdown plus post assets
    2026/
      2026-06-26-example-post.md
    index.vto               # news index route
  public/                   # static assets copied to output
  scripts/
    site-collections.ts     # normalizes Lume search page data into docs/news collections
    postbuild.ts            # copies assets, rewrites refs, writes feeds/sitemap/LLMS
    serve.ts                # serves final _site output with clean URLs
    verify-links.ts         # verifies generated local links, anchors, CSS URLs, srcset, and assets
  site-data.json.vto        # temporary Lume-rendered metadata page consumed by postbuild
  snapshots/                # ignored migration reference snapshots
```

One-use fragments should not live under `_includes`. If HTML is used by one route only, keep it in that route's `.vto`
file. Use `_includes/layouts` only for templates shared by multiple pages or by an entire content type.

## Deno Tasks

Run commands from `docs/`.

```json
{
  "tasks": {
    "lume": "deno run -P=lume lume/cli.ts",
    "build": "deno task lume && deno task postbuild && deno task bundle:client && deno task pagefind",
    "bundle:client": "deno bundle --platform=browser --minify --config=deno.json scripts/browser/exceptionless-client.ts -o _site/assets/js/exceptionless-client.js",
    "pagefind": "npx -y pagefind@1.5.2 --site _site",
    "serve": "deno task build && deno run --allow-read=_site --allow-net=localhost,127.0.0.1 --allow-env=PORT scripts/serve.ts",
    "postbuild": "deno run --allow-read --allow-write scripts/postbuild.ts",
    "check": "deno check _config.ts scripts/browser/exceptionless-client.ts scripts/site-collections.ts scripts/postbuild.ts scripts/serve.ts scripts/verify-links.ts scripts/find-unused.ts",
    "verify": "deno task build && deno run --allow-read scripts/verify-links.ts",
    "unused": "deno task build && deno run --allow-read scripts/find-unused.ts"
  }
}
```

`serve` intentionally builds first and then serves `_site`, so local QA uses the same postbuild output that would be
deployed. It accepts `--port` and the `PORT` environment variable for Codex/Aspire integration. Do not use a
long-running serve command for normal static checks; use `deno task build`, `deno task check`, and `deno task verify`
first.

## Lume Configuration

`_config.ts` should stay small and explicit:

- Source is `docs/` and destination is `_site/`.
- Site location is `https://exceptionless.com/`.
- Ignore generated output, scripts, snapshots, planning docs, raw package files, and non-published migration artifacts.
- Ignore `news/index.md` and any intentionally excluded legacy posts.
- Expose collection helpers with `site.data("docsNavHtml", docsNavHtml)` and `site.data("siteDataJson", siteDataJson)`.
- Let Lume render Markdown and Vento files, parse frontmatter, discover source paths, and provide page data through
  `search.pages()`.
- Do not reintroduce a separate `prepare.ts` content scan. Collection helpers should consume Lume-discovered page data
  unless there is a specific capability Lume cannot provide.

## Content Model

Use frontmatter for metadata consumed by the build. Avoid decorative fields that are not used.

Docs frontmatter:

```yaml
title: JavaScript Client
description: Set up Exceptionless in a JavaScript application.
order: 20
hidden: false
draft: false
llms: true
```

News frontmatter:

```yaml
title: Exceptionless 9.0 Released
date: 2026-06-26
author: Exceptionless
description: Release notes and upgrade details.
```

Marketing route frontmatter:

```yaml
title: Pricing
description: Exceptionless hosted and self-hosted pricing.
layout: layouts/base.vto
```

Only `title` is required for normal docs and news authoring. The other fields are optional overrides.

## Docs Navigation

Docs navigation is generated by `docsNavHtml(search, url)` in `scripts/site-collections.ts` from Lume's parsed page
data.

Rules:

- Adding a Markdown file under `docs/docs/` automatically renders the page.
- Visible docs pages automatically appear in the docs table of contents.
- `index.md` is the landing page for its folder and becomes the section link.
- Folders become nested TOC sections.
- Files become TOC items.
- Titles come from frontmatter `title`, falling back to the filename.
- Sort uses optional numeric `order`, then URL path.
- `hidden: true` or `draft: true` keeps a page out of the TOC.
- `llms: false` keeps a visible docs page out of LLMS files when needed.

A new docs page should require only this:

1. Add `docs/docs/some-folder/new-page.md`.
2. Set `title` if the filename is not enough.
3. Run `deno task build`.
4. The page appears in the generated TOC, sitemap, and LLMS files automatically unless hidden, draft, or `llms: false`.

## News Workflow

News and blog posts live as Markdown under `docs/news/YYYY/`.

Rules:

- Adding a dated Markdown file under `docs/news/YYYY/` automatically renders the post.
- Posts are sorted by the date prefix in the filename, with frontmatter `date` available as an override for metadata.
- `hidden: true` or `draft: true` keeps a post out of generated news collections.
- `site-data.json.vto` renders the Lume-discovered news collection for `postbuild.ts`.
- `postbuild.ts` writes the news index from the newest posts and removes `_site/site-data.json` afterward.
- `postbuild.ts` writes `feed.xml` from the newest news posts.
- Sitemap generation includes rendered published news post pages.
- News posts are never inputs to `llms.txt` or `llms-full.txt`.

A new news post should require only this:

1. Add `docs/news/2026/2026-06-26-new-post.md`.
2. Set `title`, `date`, and optional `description`.
3. Run `deno task build`.
4. The post appears in the news index, RSS feed, and sitemap automatically unless hidden or draft.

## LLMS Scope

LLMS files are docs-only.

Generated files:

- `llms.txt` - docs page index.
- `llms-full.txt` - docs page index plus docs Markdown bodies.

Inputs:

- Visible, non-draft documentation Markdown from `docs/docs/**/*.md` discovered through Lume page data.
- Excludes docs with `hidden: true`, `draft: true`, or `llms: false`.

Excluded as LLMS inputs:

- Marketing pages.
- Pricing and landing pages.
- Legal pages.
- News and blog post Markdown.
- Raw HTML snapshots.
- Generated output.
- Planning docs.

Docs feed LLMS. News feeds RSS and sitemap.

## Marketing Pages

Marketing pages can use Markdown where simple, but design-heavy routes should be route-level `.vto` files.

Current complex routes such as home, pricing, tour, and why keep their page-specific HTML directly in their route files
because each composition is used once. Shared site chrome stays in `layouts/base.vto`.

Create a reusable component only when it is genuinely reused by multiple pages or removes meaningful duplication. Do not
create a fragment just to move a single route's HTML elsewhere.

## Site Search

`/search/` is backed by Pagefind. The normal docs build renders the Lume site, runs `postbuild.ts`, and then generates
the Pagefind index into `_site/pagefind/`.

Commands:

- `deno task build` builds the site and generates the Pagefind index.
- `deno task bundle:client` bundles the static-site Exceptionless browser bootstrap from the Deno npm dependency.
- `deno task pagefind` regenerates only the Pagefind index from the current `_site` output.
- The Codex environment exposes a `Run Lume Docs` action on port 7141.
- The Aspire AppHost exposes a `Docs` resource at `http://localhost:7141` unless a scoped worktree assigns an ephemeral
  free port.

Search implementation notes:

- The shared base layout marks real page content with `data-pagefind-body` so headers, navigation, and footer content do
  not dominate the index.
- Generated redirect alias pages use `data-pagefind-ignore="all"` and should not appear as search results.
- The docs layout's generic `Documentation` page heading is ignored so results use the actual content page title.
- `public/assets/js/search.js` uses Pagefind's browser search API directly for query-string support, debounced input,
  result excerpts, sub-results, clear, and load-more behavior.
- The docs sidebar search form navigates to `/search/?q=...`; it is not a table-of-contents-only filter.
- The docs site uses the migrated Bootstrap-era theme CSS, not Tailwind. Keep search-specific CSS limited to result
  presentation that the legacy theme does not already provide.

## Static Site Error Reporting

The migrated static site can load a small Exceptionless browser-client bootstrap at `/assets/js/exceptionless-client.js`.
`deno task build` bundles this file from `scripts/browser/exceptionless-client.ts` and the Deno npm dependency
`@exceptionless/browser@3.2.1`; it does not import the browser client from a runtime CDN. The layout emits this script
only when configured with a public browser API key at build time.

Build-time configuration:

- `EXCEPTIONLESS_SITE_API_KEY` or `PUBLIC_EXCEPTIONLESS_API_KEY`
- `EXCEPTIONLESS_SITE_SERVER_URL` or `PUBLIC_EXCEPTIONLESS_SERVER_URL`
- `EXCEPTIONLESS_SITE_ENVIRONMENT` or `EX_AppMode`
- `EXCEPTIONLESS_SITE_VERSION`, `PUBLIC_APP_VERSION`, or `GITHUB_SHA`

Google Tag Manager is part of the public site layout and loads by default. Local Codex and Aspire docs runs do not force
any Exceptionless configuration; the browser client script is not emitted unless an API key is supplied.

Do not commit production keys. The Svelte app has its own `@exceptionless/browser` startup path in
`src/Exceptionless.Web/ClientApp/src/hooks.client.ts`.

## Build Pipeline

`deno task build` does this automatically:

1. Lume discovers Markdown and Vento source files, parses frontmatter, and renders routes.
2. Lume templates call `docsNavHtml(search, url)` for the docs TOC.
3. Lume renders `site-data.json.vto` as a temporary metadata artifact from `search.pages()`.
4. `postbuild.ts` reads `_site/site-data.json` and removes it before the final output is complete.
5. `postbuild.ts` copies public assets and content-local assets.
6. `postbuild.ts` rewrites migrated image references.
7. `postbuild.ts` generates the news index from Markdown posts.
8. `postbuild.ts` generates `feed.xml` from news Markdown.
9. `postbuild.ts` generates `sitemap.xml` from rendered routes.
10. `postbuild.ts` generates docs-only `llms.txt` and `llms-full.txt`.
11. `postbuild.ts` generates redirect aliases for legacy links.
12. Pagefind indexes `_site` into `_site/pagefind/`.

## Verification Gates

Do not call the migration done unless:

- `deno task check` passes.
- `deno task build` passes.
- `deno task verify` passes.
- There are no one-use fragments or one-use include files.
- Docs TOC is generated from Lume-discovered `docs/docs/**/*.md` pages.
- New docs Markdown appears in navigation and LLMS automatically.
- New news Markdown appears in the news index, RSS feed, and sitemap automatically.
- Both LLMS files use docs pages only as inputs.
- Generated sitemap includes published docs, news, and marketing pages.
- All local links and assets resolve from `_site`.
- Full desktop and mobile browser parity audits compare every live sitemap route against local output, with no same-origin failures, console errors, visible broken images, horizontal overflow, or visual diffs above the accepted migration threshold.
- Interactive legacy behaviors that remain in the markup, such as the tour Fancybox gallery, are browser-tested.
- Non-site planning Markdown, raw snapshots, temporary metadata, generated output, and caches are not published.


## Verified Snapshot - 2026-06-27

Current implementation checks completed from `docs/`:

- `deno task check` passed.
- `deno task verify` passed and reported no broken local links, anchors, CSS URLs, `srcset` references, or assets.
- Fresh `https://exceptionless.com/sitemap.xml` comparison found 214 live routes and 214 local routes, with 0 missing and 0 extra paths.
- Full desktop live/local parity audit covered all 214 sitemap routes with `issueCount: 0`.
- Full mobile live/local parity audit covered all 214 sitemap routes with `issueCount: 0`.
- The parity audits verify each local route returns 200, has no same-origin request failures, no console errors, no visible broken images, and no horizontal overflow; visual diffs stayed under the 8% threshold. Dynamic `/why/` video/counter pixels are masked because live content moves.
- Source/output audit found 59 documentation Markdown source files, 58 published `llms.txt` docs links, 58 published `llms-full.txt` docs links, 0 internal non-doc page links in LLMS, and 1 intentionally unpublished docs page (`demo-formatting.md`).
- Source/output audit found 147 news Markdown posts; `feed.xml` contains the latest 25 posts and the news index contains the latest 10 posts.
- Temporary add/remove probe proved a normal new docs Markdown file is generated, appears in the docs TOC, and appears in LLMS automatically.
- Temporary add/remove probe proved a normal new news Markdown file is generated, appears in the news index, appears in RSS, and becomes the first RSS item when newest.
- `_includes` contains only shared layouts (`base`, `docs`, `page`, `post`); one-off marketing HTML stays in route-level `.vto` files.
- Tour Fancybox behavior is wired with local legacy assets and opens the first gallery image without navigating away from `/tour/`.

Implementation notes from the verification pass:

- Markdown headings receive deterministic legacy-style IDs during Lume Markdown rendering so existing same-page anchors continue to work.
- `docs/docs/demo-formatting.md` is `url: false` and is intentionally not published.
- `/search/` uses Pagefind for local static search while keeping sitemap parity exact.
- `postbuild.ts` contains the small migration normalizers required for parity: migrated image-reference rewrites, duplicate news H1 removal, standalone YouTube URL embed rendering, Prism `<pre>` class normalization, news index/RSS/sitemap/LLMS generation, internal non-doc link stripping for `llms-full.txt`, and redirect alias generation.
- The legacy Fancybox stylesheet references `/assets/images/fancybox.png` and `/assets/images/fancybox-x.png`; both are committed static assets.
- Fancybox uses the original 1.3.4 plugin plus a tiny jQuery 1.9 browser-compat shim because the WordPress-patched Easy FancyBox script requires `DOMPurify`.
- A mobile footer override removes the legacy 20px horizontal overflow caused by the old negative-margin footer rule.

## Verified Search Upgrade - 2026-07-06

- `deno task check` passed.
- `deno task build` passed and Pagefind indexed 230 real pages while ignoring 37 redirect alias pages.
- `deno task verify` passed with no broken local links, anchors, or assets.
- Local Chrome dogfood at `http://127.0.0.1:7141/search/?q=javascript` returned 114 Pagefind results with
  `JavaScript Example` first, loaded the local Pagefind script and worker, had no inline scripts, loaded Google Tag
  Manager from the base layout, did not emit the Exceptionless client script without an API key, and had no console
  errors, page errors, or failed local resources.
- Interactive local Chrome dogfood updated the URL to `/search/?q=multiple+queries`, returned 17 results with
  `Filtering & Searching` first, and cleared back to the empty state.
- Mobile Chrome dogfood at a 390px viewport returned the same `/search/?q=javascript` results with no horizontal overflow.

## Core Principle

The maintained source should be Markdown, metadata, shared Vento layouts, route-level Vento pages where needed, and
small TypeScript helpers that operate on Lume's page model. Raw HTML snapshots are useful for migration confidence, but
they should not become the long-term authoring model.
