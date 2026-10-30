# Docs rewrite plan

A new set of Exceptionless docs that ships with Exceptionless 9.0.0 and the site rebrand (#2685). This plan does not
follow the current structure. It starts from what the product does today, what readers and AI agents need, and how the
best developer-tool docs are organized.

## Goals

1. **Accurate for the release.** Every page matches the code that ships. Nothing describes the removed Angular UI, .NET
   Framework-era setup as the default, or features that are not released.
2. **AI first.** Exie, the MCP server, and coding agents are a headline section, and every task page shows how to do it
   with AI as well as by hand. The docs themselves are built to be read by agents.
3. **Fast to first event.** A new user goes from sign-up to a test error in the app in a few minutes, by hand or by
   pasting one prompt into their coding agent.
4. **Nothing breaks.** Every current docs URL, including the 12 the app links to, still resolves after the rewrite.
5. **Easy to keep current.** Clear page types, explicit nav, single sources for facts like plan limits and MCP tools,
   and checks that catch drift.
6. **Ready for many languages.** .NET and JavaScript are the first two clients, not special cases. Adding a language
   means adding a data entry and a folder of pages that follow a template; the docs and the marketing site pick it up
   without hand edits.

## What we found

Research is summarized here; details are in the PR discussion.

- **Current docs:** 63 published pages and about 34,000 words. 13 pages were rewritten for the Svelte UI in #2685; the
  rest range from fine to badly stale. 11 clusters of duplicate content and 12 pages under 150 words.
- **Old UI and stale content:**
  - Several pages still describe the Angular UI: project tokens, custom clients, Kubernetes base URL, and the getting
    started page.
  - The FAQ, .NET configuration, .NET troubleshooting, and self-hosted upgrading pages are mostly .NET Framework or
    2015-2017 content.
  - The Kubernetes page includes internal hosted-ops runbook details.
- **Client SDKs:**
  - Current versions are Exceptionless.Net 6.2.0 and Exceptionless.JavaScript 3.2.1.
  - In 6.2.0, `Exceptionless.AspNetCore`, `.Extensions.Hosting`, and `.Extensions.Logging` require .NET 8+, and
    `app.UseExceptionless(Configuration)` is gone. The docs still use it.
  - React Native and Expo shipped in 3.2 and are not documented.
  - `Exceptionless.Nancy` is deprecated but still listed.
  - **Environment support is not released in either SDK.** It is in Exceptionless.Net #371 and Exceptionless.JavaScript
    #193, but `environments.md` on main already documents it.
- **Product:** large parts of the product have no docs at all:
  - saved views, which replaced the fixed dashboards;
  - event detail tabs;
  - the command palette, keyboard shortcuts, and guided tours;
  - organization and billing settings;
  - the account AI Tools and connected applications pages;
  - admin system pages.
- **Hosting and redirects:** the site deploys to GitHub Pages, which ignores `_redirects`. Only the HTML redirect stubs
  that `postbuild.ts` writes actually redirect, and it only writes them for news URLs. There is no way to redirect a
  moved docs page today.
- **Other docs sites:**
  - The best developer-tool docs organize navigation by product and platform, and use page types (tutorial, how-to,
    reference, explanation) as a writing rule rather than as the nav.
  - AI-forward sites put an agent setup prompt next to the quickstart and give MCP its own section with per-client
    install steps, scopes, example prompts, and a security section. They also publish llms.txt and Markdown for every
    page.
  - Patterns to avoid: AI buried under "Resources", MCP docs on a separate site, and instructions aimed at agents inside
    llms.txt.

## Principles

- **Keep a URL when the page keeps its purpose.** Move or merge only when the structure needs it, and redirect
  everything that moves.
- **One page, one type.** A page is a tutorial, a how-to guide, reference, or an explanation. Split pages that mix them,
  such as filtering and searching, which is a how-to plus a field reference.
- **One fact, one place.** Plan limits come from `_data/plans.json`. Client configuration values, data exclusions, and
  private information each live on one page, and other pages link to them.
- **Hosted first, self-hosted alongside.** Pages describe the hosted service. A self-hosting note appears only where
  behavior differs, and the self-hosting section covers running it.
- **Every task page answers "how do I do this with AI?"** with copyable Exie prompts and, where it applies, the MCP tool
  an agent would use.
- **Modern setup first.** For example, ASP.NET Core minimal hosting and `appsettings.json` first, with .NET Framework
  and `web.config` on a separate legacy page.

## Information architecture

The sidebar order is explicit (see [Site changes](#site-changes)). URLs are under `/docs/`. **Keep** means the URL
stays; **New** pages are new; merged and moved pages are in the [redirect map](#redirect-map).

### 1. Get started

| Page          | URL                      | Status        | Type        | What it covers                                                                                                                                           |
| ------------- | ------------------------ | ------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Overview      | `/docs/`                 | Keep, rewrite | —           | Three doors: send your first event, connect your AI agent, self-host. What's new in this version.                                                        |
| Quickstart    | `/docs/getting-started/` | Keep, rewrite | Tutorial    | Sign up, create a project, install a client (one tab per language), throw a test error, and see it. Includes an "Install with your coding agent" prompt. |
| Core concepts | `/docs/concepts/`        | New           | Explanation | Events, stacks, projects and organizations, environments, saved views, and stack statuses, in about five minutes.                                        |
| What's new    | `/docs/whats-new/`       | New           | Explanation | 9.0 for returning users: the new UI, saved views replacing dashboards, Exie, MCP, environments, source maps, and where things moved.                     |

### 2. AI

| Page                    | URL                       | Status        | Type               | What it covers                                                                                                                                                                                                 |
| ----------------------- | ------------------------- | ------------- | ------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AI overview             | `/docs/ai/`               | Keep, rewrite | Explanation        | What Exie and the MCP server each do, and when to use which.                                                                                                                                                   |
| Exie                    | `/docs/ai/exie/`          | Keep, rewrite | How-to             | Opening Exie (side panel, full page, the `a` shortcut, Fix with Exie), what it can answer, which actions it can take and when, plans and limits, and good prompts.                                             |
| MCP server              | `/docs/ai/mcp-server/`    | Keep, rewrite | How-to + reference | Install steps for each client that passes [verification](#mcp-client-verification). Also covers consent and scopes (read vs `stacks:write`), managing connected apps, the tool reference, and example prompts. |
| Coding agents           | `/docs/ai/coding-agents/` | New           | How-to             | Set up Exceptionless with an agent; the AGENTS.md snippet; the agent skills in the SDK repos; recipes such as "fix my top new error", "triage regressions after a deploy", and "verify a fix in production".   |
| AI privacy and security | `/docs/ai/privacy/`       | New           | Explanation        | What Exie and MCP can see and do, how tool results are cached, chat sharing, the model provider, prompt-injection risk and how tools treat untrusted data, read-only grants, and revoking access.              |
| Docs for AI             | `/docs/ai/docs-for-ai/`   | New           | Reference          | `llms.txt`, `llms-full.txt`, the Markdown copy of every page, and the page actions.                                                                                                                            |

### 3. Send data (clients)

More clients are coming, so the section is built around languages as peers:

- **One list of clients.** `_data/clients.json` holds each client's language, packages, install command, minimal setup
  snippet, frameworks, minimum runtime, support level, repo, and docs URL. The client chooser, the quickstart tabs, the
  feature matrix, the `llms.txt` client section, and the marketing home page's code tabs and FAQ answer all read from
  it.
- **The same pages for every language.** Each language folder has an overview, app-type or framework guides,
  configuration, sending events, client configuration values, plugins, private information, troubleshooting, and
  upgrading, each from a shared template. A language that lacks a feature says so on that page instead of dropping it.
- **Shared behavior is explained once.** What a session is, how configuration values sync, and what data exclusions do
  live in Use Exceptionless and How it works. Language pages cover only setup and API.
- **A client specification.** One reference page defines what every Exceptionless client does. It is the contract for
  new official clients and for community ones.
- **Support levels.** Official, Community, and Preview. Only clients that work with 9.0 are listed, so the Java, Go, and
  Python clients stay off until they are updated.

| Page                  | URL                       | Status        | Type      | What it covers                                                                                       |
| --------------------- | ------------------------- | ------------- | --------- | ---------------------------------------------------------------------------------------------------- |
| Choose a client       | `/docs/clients/`          | Keep, rewrite | —         | Language chooser with support levels, from `clients.json`.                                           |
| Client feature matrix | `/docs/clients/features/` | New           | Reference | Which features each client supports: sessions, logs, feature usage, configuration values, and so on. |

**.NET** (`/docs/clients/dotnet/`)

| Page                             | URL                                   | Status                 | Type      | What it covers                                                                                                                             |
| -------------------------------- | ------------------------------------- | ---------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| .NET overview                    | `dotnet/`                             | Keep, rewrite          | —         | Packages, requirements (.NET 8+ for the ASP.NET Core, Hosting, and Logging packages; core on netstandard2.0), and a link to each app type. |
| ASP.NET Core                     | `dotnet/aspnet-core/`                 | New                    | How-to    | `builder.AddExceptionless()`, `app.UseExceptionless()`, `IExceptionHandler`, problem details, and `appsettings.json`.                      |
| Worker services and generic host | `dotnet/worker-services/`             | New                    | How-to    | `Host.CreateApplicationBuilder` and flushing on shutdown.                                                                                  |
| Console apps                     | `dotnet/console/`                     | New                    | How-to    | `Startup()` and `ProcessQueueAsync`.                                                                                                       |
| Desktop apps                     | `dotnet/desktop/`                     | New                    | How-to    | WPF, WinForms, and MAUI.                                                                                                                   |
| Blazor WebAssembly               | `dotnet/blazor/`                      | New                    | How-to    | Using the core package.                                                                                                                    |
| Serverless                       | `dotnet/serverless/`                  | New                    | How-to    | AWS Lambda and Azure Functions, and processing the queue before returning.                                                                 |
| Logging integrations             | `dotnet/logging/`                     | New                    | How-to    | Microsoft.Extensions.Logging, Serilog, NLog, and log4net.                                                                                  |
| Configuration                    | `dotnet/configuration/`               | Keep, rewrite          | Reference | `appsettings.json`, environment variables, and code. The `web.config` and attribute options move to the legacy page.                       |
| Sending events                   | `dotnet/sending-events/`              | Keep, rewrite          | How-to    | Errors, logs, feature usage, 404s, custom events, the fluent builder, user identity, and sessions.                                         |
| Client configuration values      | `dotnet/client-configuration-values/` | Keep, merge `settings` | How-to    | Reading server-synced settings and reacting to changes.                                                                                    |
| Plugins                          | `dotnet/plugins/`                     | Keep, rewrite          | How-to    | Enriching and excluding events, with a cross-platform example instead of `PerformanceCounter`.                                             |
| Private information              | `dotnet/private-information/`         | Keep, rewrite          | Reference | The `Include*` flags and data exclusions.                                                                                                  |
| Legacy ASP.NET                   | `dotnet/legacy-aspnet/`               | New                    | How-to    | MVC, Web API, and WebForms on .NET Framework with `web.config`.                                                                            |
| Troubleshooting                  | `dotnet/troubleshooting/`             | Keep, rewrite          | How-to    | Client logging, the queue, storage, proxies, and certificates. Drops PCL and isolated storage paths.                                       |
| Upgrading                        | `dotnet/upgrading/`                   | Keep, rewrite          | How-to    | 6.2 breaking changes first, older majors condensed.                                                                                        |

**JavaScript** (`/docs/clients/javascript/`)

| Page                        | URL                                       | Status              | Type      | What it covers                                                           |
| --------------------------- | ----------------------------------------- | ------------------- | --------- | ------------------------------------------------------------------------ |
| JavaScript overview         | `javascript/`                             | Keep, rewrite       | —         | Packages, ESM and ES2022, the CDN, and a link to each framework.         |
| Framework guides            | `javascript/guides/`                      | Keep, rewrite       | —         | Index of the guides below.                                               |
| Browser                     | `javascript/guides/browser/`              | New                 | How-to    | npm and CDN setup.                                                       |
| Node.js and serverless      | `javascript/guides/node/`                 | New                 | How-to    | `processQueue` for short-lived processes.                                |
| Express                     | `javascript/guides/express/`              | Keep, merge example | How-to    | —                                                                        |
| React                       | `javascript/guides/react/`                | Keep, merge example | How-to    | `ExceptionlessErrorBoundary` with `fallback` (React 18+).                |
| React Native and Expo       | `javascript/guides/react-native/`         | New                 | How-to    | The 3.2 package, the Expo config plugin, and native iOS crash reporting. |
| Next.js                     | `javascript/guides/nextjs/`               | New                 | How-to    | From the SDK's App Router example.                                       |
| SvelteKit                   | `javascript/guides/sveltekit/`            | New                 | How-to    | From the SDK example.                                                    |
| Vue                         | `javascript/guides/vue/`                  | Keep, merge example | How-to    | —                                                                        |
| Angular                     | `javascript/guides/angular/`              | Keep, rewrite       | How-to    | Standalone components and an `ErrorHandler` provider.                    |
| Configuration               | `javascript/client-configuration/`        | Keep, rewrite       | Reference | —                                                                        |
| Sending events              | `javascript/sending-events/`              | Keep, rewrite       | How-to    | —                                                                        |
| Client configuration values | `javascript/client-configuration-values/` | Keep                | How-to    | —                                                                        |
| Source maps                 | `javascript/source-maps/`                 | Moved               | How-to    | From `/docs/source-maps/`.                                               |
| Troubleshooting             | `javascript/troubleshooting/`             | Keep, rewrite       | How-to    | —                                                                        |

**Other languages**

| Page                               | URL                             | Status        | Type   | What it covers                                                                                                                                                                                |
| ---------------------------------- | ------------------------------- | ------------- | ------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Other languages and custom clients | `/docs/clients/custom-clients/` | Keep, rewrite | How-to | Send events from any language over HTTP, and from shell scripts (Bash and PowerShell, which the app's setup page already offers). The event format and the client contract move to Reference. |

### 4. Use Exceptionless

| Page                      | URL                              | Status                     | Type   | What it covers                                                                                                                                                |
| ------------------------- | -------------------------------- | -------------------------- | ------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Events                    | `/docs/events/`                  | New                        | How-to | Event lists, the detail panel and its tabs, live updates, and finding an event by reference ID.                                                               |
| Stacks                    | `/docs/managing-stacks/`         | Keep, merge `bulk-actions` | How-to | Statuses, fix, snooze, ignore, discard, critical, reference links, promote to webhook, and bulk actions.                                                      |
| Filtering and searching   | `/docs/filtering-and-searching/` | Keep, split                | How-to | The filter bar, filters, raw filters, and the date range. The field table moves to Reference.                                                                 |
| Saved views               | `/docs/saved-views/`             | New                        | How-to | Built-in views, creating and sharing views, columns, charts, default views, and ordering.                                                                     |
| Environments              | `/docs/environments/`            | Keep, rewrite              | How-to | Setting the environment in each client once the SDKs ship it. Remove the release-notes section.                                                               |
| Releases and regressions  | `/docs/versioning/`              | Keep, rewrite              | How-to | Versions, mark fixed in a version, and regression rules.                                                                                                      |
| Logs and log levels       | `/docs/setting-log-levels/`      | Keep, rewrite              | How-to | Log views and project and per-stack levels.                                                                                                                   |
| Feature usage             | `/docs/feature-usage/`           | New                        | How-to | Sending usage events and the Most Used Features view.                                                                                                         |
| Sessions                  | `/docs/user-sessions/`           | Keep, rewrite              | How-to | —                                                                                                                                                             |
| Reference IDs             | `/docs/references-ids/`          | Keep, rewrite              | How-to | A support workflow with a modern ASP.NET Core sample.                                                                                                         |
| Notifications             | `/docs/notifications/`           | Keep, rewrite              | How-to | Email and Slack, plus rate and budget notifications if #2271 and #2270 ship.                                                                                  |
| Integrations              | `/docs/integrations/`            | Keep, rewrite              | How-to | Slack, webhooks (with a current payload), and Zapier.                                                                                                         |
| Navigating the app        | `/docs/navigation/`              | New                        | How-to | The command palette, keyboard shortcuts, and guided tours.                                                                                                    |
| Projects                  | `/docs/project-settings/`        | Keep, rewrite              | How-to | API keys, client configuration, data exclusions, stacking settings, spam detection, source maps, reset, and delete. Keeps the `#client-configuration` anchor. |
| Organizations and billing | `/docs/organizations/`           | New                        | How-to | Users and invites, plans, upgrading, usage, and invoices.                                                                                                     |
| Your account              | `/docs/account/`                 | New                        | How-to | Profile, appearance, email notifications, AI Tools, connected applications, external logins, and password.                                                    |

### 5. How it works

| Page                     | URL                        | Status                        | Type        | What it covers                                                                                                       |
| ------------------------ | -------------------------- | ----------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------- |
| Stacking                 | `/docs/concepts/stacking/` | New, merges `manual-stacking` | Explanation | Signatures, stacking settings, manual stacking keys, and the Stacking information dialog.                            |
| Limits and deduplication | `/docs/concepts/limits/`   | New, merges `deduplication`   | Explanation | Plan limits from `plans.json`, retention, throttling, overage, client-side deduplication, and bursts.                |
| Security and privacy     | `/docs/security/`          | Keep, rewrite                 | Explanation | Data handling, encryption in transit, private information, data exclusions, and access control. Links to AI privacy. |

### 6. Reference

| Page                 | URL                              | Status                   | Type      | What it covers                                                                                                                                                                                        |
| -------------------- | -------------------------------- | ------------------------ | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Search syntax        | `/docs/reference/search-syntax/` | New, from filtering      | Reference | Every field, operators, ranges, and examples.                                                                                                                                                         |
| Event format         | `/docs/reference/event-format/`  | New, from custom clients | Reference | Event JSON, known types, and reserved data keys. Includes `environment`, `count`, and the browser and OS keys.                                                                                        |
| Client specification | `/docs/reference/client-spec/`   | New, from custom clients | Reference | What every client does: submission and batching, the offline queue, retries and `429`/`503` handling, configuration value sync, session heartbeats, deduplication, data exclusions, and environments. |
| FAQ                  | `/docs/FAQ/`                     | Keep, trim               | Reference | Only questions not answered elsewhere. The URL keeps its uppercase path.                                                                                                                              |

### 7. REST API

| Page                       | URL                         | Status                            | Type      | What it covers                                                                                                                                                     |
| -------------------------- | --------------------------- | --------------------------------- | --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| API overview               | `/docs/api/`                | Keep, rewrite                     | Reference | Base URLs, the Scalar reference at `/docs` and the OpenAPI JSON, status codes (402, 413, 429, 503), and rate limits. The MCP OAuth details move to the AI section. |
| Authentication             | `/docs/api/authentication/` | New, merges `api-getting-started` | How-to    | API keys, user tokens, OAuth bearer tokens, scopes, and the ways to pass a token.                                                                                  |
| Client-scoped tokens       | `/docs/api/project-tokens/` | Keep, rewrite                     | How-to    | In the new UI, plus the correct endpoint.                                                                                                                          |
| Posting events             | `/docs/api/posting-events/` | Keep, rewrite                     | How-to    | —                                                                                                                                                                  |
| Querying events and stacks | `/docs/api/getting-events/` | Keep, rewrite                     | How-to    | snake_case responses and paging.                                                                                                                                   |

### 8. Self-hosting

| Page                      | URL                                                  | Status             | Type        | What it covers                                                                                                                                                                                                               |
| ------------------------- | ---------------------------------------------------- | ------------------ | ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Overview and requirements | `/docs/self-hosting/`                                | Keep, rewrite      | Explanation | Architecture (api, job, app, and all-in-one images), Elasticsearch and Redis versions, and storage and email options. A short reverse proxy section keeps the `#reverse-proxies-and-https` anchor and links to the new page. |
| Docker                    | `/docs/self-hosting/docker/`                         | Keep, rewrite      | Tutorial    | Docker Compose as the recommended path, plus the all-in-one image for trying it out. Fixes the SSL port example.                                                                                                             |
| Kubernetes                | `/docs/self-hosting/kubernetes/`                     | Keep, rewrite      | How-to      | The Helm chart, values, and ingress. Removes internal hosted-ops details and the Angular-era base URL advice.                                                                                                                |
| Configuration             | `/docs/self-hosting/configuration/`                  | New                | Reference   | `EX_` settings, connection strings, email, storage, OAuth login providers, LDAP, and account creation.                                                                                                                       |
| Reverse proxies and HTTPS | `/docs/self-hosting/reverse-proxy/`                  | New, from overview | How-to      | Trusted forwarded-header peers, written for operators.                                                                                                                                                                       |
| AI features               | `/docs/self-hosting/ai/`                             | New                | How-to      | Enabling Exie (`Assistant` settings and model choice), the MCP server (`OAuthServer`, `Mcp:AllowedOrigins`), and turning AI off.                                                                                             |
| Administration            | `/docs/self-hosting/administration/`                 | New                | How-to      | System pages: migrations, runtime settings and the event submission kill switch, Exie usage, OAuth apps, and notifications.                                                                                                  |
| Upgrading                 | `/docs/self-hosting/upgrading-self-hosted-instance/` | Keep, rewrite      | How-to      | The current path: automatic migrations and reruns, Elasticsearch 8.19 to 9, and trusted proxies. The v1 to v5 content is dropped.                                                                                            |

That is about 75 pages, up from 63, with less total text: duplicate clusters and example-only pages are merged.

## AI throughout the docs

- **AI is the second section** in the sidebar, right after Get started.
- **Agent setup path.** The quickstart and every client overview get an "Install with your coding agent" block: a
  copyable prompt that points the agent at the page's Markdown and the SDK's agent skill (both SDK repos already have
  `.agents/skills/...`). The prompt is opt-in for the reader. `llms.txt` stays factual and never contains instructions
  aimed at agents.
- **"Ask Exie" blocks.** Task pages end with two or three copyable Exie prompts. Where it applies, they also show the
  MCP tool and prompt an agent would use. This needs a small `prompt` component with a copy button.
- **Plan badges.** Exie pages and blocks show "Medium plans and up". The limits table comes from `plans.json`.
- **Security next to capability.** The MCP and Exie pages link to AI privacy and security. The MCP page has its own
  scopes and prompt-injection section.
- **Docs for agents.**
  - Split `llms.txt` by the new sections.
  - Keep `llms-full.txt`.
  - Add a small `llms-small.txt` with the get started, AI, and client overview pages.
  - Keep Markdown copies of every page, and add redirect stubs for the old `.md` URLs.
- **Product follow-ups** (outside this docs PR, worth doing for launch):
  - A `search_docs` MCP tool so agents can look things up while working.
  - A server test that fails when an MCP tool exists that the MCP docs page does not list.

### MCP client verification

The MCP page documents a client only after it works end to end against a real deployment: sign-in and consent, a
read-only tool call, and a `stacks:write` action after granting it.

What the server supports today: OAuth with Client ID Metadata Documents and dynamic client registration for public
clients, HTTPS or loopback redirect URIs, and requests with no `Origin` header, which server-side clients send.

| Client                      | Evidence in the code today                                                                          | Check                             |
| --------------------------- | --------------------------------------------------------------------------------------------------- | --------------------------------- |
| Claude Code                 | Tests use Claude Code's client metadata document                                                    | End to end                        |
| Codex                       | Tests register a Codex-style loopback client                                                        | End to end                        |
| VS Code and Copilot         | Tests register a Copilot-style loopback client                                                      | End to end                        |
| Cursor                      | None                                                                                                | End to end                        |
| OpenCode                    | None                                                                                                | End to end                        |
| Claude.ai custom connectors | None. It should work on paper (registration with an HTTPS callback, calls from Anthropic's servers) | End to end on a public deployment |
| ChatGPT connectors          | None. Same shape as Claude.ai                                                                       | Worth trying                      |

Claude.ai and ChatGPT cannot reach a local AppHost, so they need a public test deployment. A client that fails stays off
the page and gets an issue.

## Site changes

These come first, before content.

1. **Redirects.**
   - Add `redirectFrom: [...]` frontmatter. `postbuild.ts` writes an HTML stub at each old path and a Markdown stub at
     each old `.md` path.
   - Stubs keep the URL fragment, so `/docs/bulk-actions/` goes to `/docs/managing-stacks/#bulk-actions` and app links
     with anchors keep working.
2. **A URL check that never forgets.**
   - Commit today's list of docs URLs (`scripts/docs-urls.txt`).
   - `deno task verify` fails if any of them stops resolving to a page or a stub.
3. **Explicit navigation.**
   - A `_data/docs_nav.yml` file (or `section` and `order` frontmatter) defines sections and order, instead of folders
     plus alphabetical titles.
   - Previous and next links are generated from the nav, so the hand-written "Next >" links go away.
4. **Frontmatter.**
   - Required: `title` and `description`.
   - Optional: `metaTitle`, `redirectFrom`, and `plan` (renders a badge).
   - `sources`: code paths the page is checked against, used in review and not rendered.
5. **Components.**
   - A copyable `prompt` block for Exie and agent prompts.
   - A plan badge.
   - Language tabs built from `clients.json` plus curl, so a new language shows up on shared pages automatically.
6. **llms output.** Section mapping follows the new nav, plus `llms-small.txt`.
7. **Templates.** Every client app-type page follows the same outline: install, configure, verify with a test error,
   what is captured automatically, next steps, and Ask Exie. Each language folder starts from the same set of page
   templates.
8. **Client data.** Add `_data/clients.json`, and switch the marketing home page's code tabs and FAQ answer to read from
   it.
9. **One API hostname.** Docs use `https://api.exceptionless.io` for the hosted API. `be.exceptionless.io` stays only
   where a page means the web app. A verify check fails on `collector.exceptionless.io` or API paths on
   `be.exceptionless.io`, and holds the allowed hostname in one constant, so a move to `api.exceptionless.com` is one
   find-and-replace and one constant.

## Accuracy

- **Check every page against the code** listed in its `sources`, not against the old docs.
- **Snippets come from the SDK samples.**
  - Exceptionless.Net `samples/` and the JavaScript `example/` apps.
  - JavaScript's `packages/core/test/DocumentationExamples.test.ts`, which already tests documented usage.
  - Each snippet notes its source in a comment in the Markdown source.
- **No unreleased APIs** unless they ship by launch, such as the environment APIs in the SDKs.
- **Review pass per section.** A second agent or a person checks claims against the code and runs the steps against a
  local AppHost.
- **Screenshots go last,** once the UI is frozen, using the existing light and dark capture process.
- **App links.** The 12 docs links in the app keep working through redirects. A follow-up app PR points them at the new
  pages. The app's Configure page also still shows `web.config` instructions as a main path, which is worth fixing at
  the same time.

## Launch dependencies

Several in-flight PRs land before writing starts. When writing starts, check each one against main: document what has
merged and leave out what has not.

| Change                                                                                       | Affects                                                                                          |
| -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Environment support in Exceptionless.Net #371 and Exceptionless.JavaScript #193 (unreleased) | Environments, client configuration. The SDKs must ship before 9.0, or the SDK sections come out. |
| Exie rebuild #2684                                                                           | Exie, AI privacy, navigation (the `a` shortcut)                                                  |
| Date range fields #2683                                                                      | Filtering                                                                                        |
| Microsoft sign-in #2403                                                                      | Account, self-hosting configuration                                                              |
| .NET 11 #2665 and Elasticsearch 9 #2416                                                      | Self-hosting requirements and upgrading                                                          |
| Event telemetry and charts #2623 (draft)                                                     | Saved views, event format, MCP tools                                                             |
| Rate and budget notifications #2271 and #2270                                                | Notifications, organizations                                                                     |
| Retry-After on 503 #2646                                                                     | API status codes, client troubleshooting                                                         |
| SSE push #2265 (draft)                                                                       | Self-hosting reverse proxy                                                                       |
| Typed custom fields #1815 (draft)                                                            | Filtering, saved views, event format                                                             |
| Connection string names #2273 (draft)                                                        | Self-hosting configuration                                                                       |

## Style guide

A short `docs/STYLE.md` that every writer and agent follows:

- **Voice.** Second person and present tense, with plain words and short paragraphs. Write for a developer in a hurry
  and for an agent reading the Markdown.
- **Titles and headings.** Titles describe the task ("Send your first event"). Headings use sentence case.
- **Page shape.** Start with what the page lets you do, then the steps or facts. No history and no release notes in
  pages; those go in What's new or the changelog.
- **Code.** Complete and runnable, with imports. Use the `YOUR_API_KEY` placeholder and hosted URLs, with self-hosted
  overrides in a note.
- **UI names.** Match the UI exactly and set them in bold. Keyboard keys use `<kbd>`.
- **Exie's voice.** Friendly and direct inside Exie's speech bubbles and prompt examples. Docs prose stays neutral.
- **Links.** Use relative docs links; never `blob/master` links with line anchors. Link to a heading only if it is
  stable.
- **Screenshots.** Use the existing `![alt](/assets/img/docs/name-light.webp)` light and dark pairs, only when the UI is
  hard to describe.

## Phases

1. **Land the in-flight PRs** that are part of 9.0, then check the launch dependency table against main.
2. **Foundation.** Redirects and the URL check, explicit nav, frontmatter, components, llms sections, the style guide,
   and page templates. The site still builds, and every current URL resolves.
3. **Skeleton.**
   - Create every page in the IA with its outline and `sources`.
   - Move and merge pages with redirects.
   - Park old content that has not been rewritten yet behind its new URL, so nothing is lost mid-rewrite.
4. **Write, in priority order:**
   1. Get started and AI.
   2. Clients.
   3. Use Exceptionless.
   4. How it works and reference.
   5. REST API.
   6. Self-hosting.

   Sections are independent and can be written in parallel against the style guide and templates.
5. **Review.** Check accuracy against the code, run the steps, run the snippets, verify each MCP client, and do an
   editorial pass.
6. **Screenshots** once the UI is frozen.
7. **Launch.**
   - Ship the app PR with updated docs links and any SDK releases.
   - Run `deno task verify`.
   - Submit the sitemap.
   - Tag the release, which deploys the site.

## Decisions

1. **Scope.** Several in-flight PRs land before writing starts. The docs cover what is on main when writing starts.
2. **Version.** The launch is Exceptionless 9.0.0.
3. **Legacy upgrades.** The v1 to v5 self-hosted upgrade steps are dropped.
4. **Comparison page.** It goes away and redirects to `/why/`.
5. **Clients.** The docs and the marketing site are built for many languages (see [Send data](#3-send-data-clients)).
   Only clients that work with 9.0 are listed.
6. **API hostname.** `api.exceptionless.io`, kept in one place so it can move to `api.exceptionless.com`. The MCP server
   URL and OAuth resource identifiers use `be.exceptionless.io`, because production derives them from `BaseURL` (no
   `EX_ApiUrl` is set). Setting `EX_ApiUrl` in production would let them move to the API host too.
7. **MCP clients.** Document only the clients that pass [verification](#mcp-client-verification).

## Redirect map

Every current page and where it goes. Pages not listed keep their URL.

| Old URL                                                  | New URL                                             |
| -------------------------------------------------------- | --------------------------------------------------- |
| `/docs/bulk-actions/`                                    | `/docs/managing-stacks/#bulk-actions`               |
| `/docs/manual-stacking/`                                 | `/docs/concepts/stacking/#manual-stacking`          |
| `/docs/deduplication/`                                   | `/docs/concepts/limits/#deduplication`              |
| `/docs/comparison/`                                      | `/why/`                                             |
| `/docs/source-maps/`                                     | `/docs/clients/javascript/source-maps/`             |
| `/docs/api/api-getting-started/`                         | `/docs/api/authentication/`                         |
| `/docs/clients/dotnet/settings/`                         | `/docs/clients/dotnet/client-configuration-values/` |
| `/docs/clients/dotnet/supported-platforms/`              | `/docs/clients/dotnet/`                             |
| `/docs/clients/dotnet/guides/`                           | `/docs/clients/dotnet/`                             |
| `/docs/clients/dotnet/guides/web-server-example/`        | `/docs/clients/dotnet/aspnet-core/`                 |
| `/docs/clients/dotnet/guides/console-apps-example/`      | `/docs/clients/dotnet/console/`                     |
| `/docs/clients/dotnet/guides/logging-with-generic-host/` | `/docs/clients/dotnet/logging/`                     |
| `/docs/clients/javascript/javascript-example/`           | `/docs/clients/javascript/guides/browser/`          |
| `/docs/clients/javascript/node-example/`                 | `/docs/clients/javascript/guides/node/`             |
| `/docs/clients/javascript/react-example/`                | `/docs/clients/javascript/guides/react/`            |
| `/docs/clients/javascript/vue-example/`                  | `/docs/clients/javascript/guides/vue/`              |
| `/docs/clients/javascript/express-example/`              | `/docs/clients/javascript/guides/express/`          |
| `/docs/clients/javascript/guides/more-guides/`           | `/docs/clients/javascript/guides/`                  |

Anchors that the app or other pages link to stay on their new pages:

- `/docs/project-settings/#client-configuration`
- `#data-exclusions` on project settings and security
- `/docs/self-hosting/#reverse-proxies-and-https`
- `/docs/api/project-tokens/#get-projects`
- `/docs/self-hosting/docker/#simple-setup-wssl-support-and-smtp`
- the Kubernetes `#configuration` and `#active-directory-authentication` anchors
