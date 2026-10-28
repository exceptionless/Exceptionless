---
title: "What's new in Exceptionless 9.0"
navTitle: "What's new"
description: "Exceptionless 9.0 for returning users: the new app, saved views, Exie, the MCP server, environments, Microsoft sign-in, and where things moved."
sources:
  - src/Exceptionless.Web/ClientApp/src/lib/features/navigation/legacy-links.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/(components)/layouts/sidebar-user.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/routes.svelte.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/keyboard-shortcuts.ts
  - src/Exceptionless.Web/ClientApp/src/lib/features/shared/components/date-range-picker/date-range-picker.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/assistant/components/assistant-fix-button.svelte
  - src/Exceptionless.Web/ClientApp/src/routes/(auth)/oauth/authorize/+page.svelte
  - src/Exceptionless.Core/Seed/predefined-saved-views.json
  - src/Exceptionless.Core/Models/Event.cs
  - src/Exceptionless.Core/Configuration/AuthOptions.cs
  - src/Exceptionless.Core/Authorization/AuthorizationRoles.cs
  - src/Exceptionless.Core/Migrations/010_AddEventEnvironment.cs
  - src/Exceptionless.Web/Api/Handlers/AuthHandler.cs
  - src/Exceptionless.Web/Api/Endpoints/AuthEndpoints.cs
  - src/Exceptionless.Web/Assistant/AssistantAccessService.cs
  - src/Exceptionless.Web/Assistant/AssistantToolDefinitions.cs
  - src/Exceptionless.Web/Mcp/ExceptionlessMcpTools.cs
  - src/Exceptionless.Web/Security/TrustedForwardedHeadersOptions.cs
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Web/appsettings.yml
  - Dockerfile
  - docker/docker-compose.yml
  - Exceptionless.Net:src/Platforms/Exceptionless.AspNetCore/ExceptionlessExtensions.cs
  - Exceptionless.JavaScript:packages/react-native/package.json
---

# What's new in Exceptionless 9.0

Exceptionless 9.0 replaces the classic app with the new one. During the 8.x releases the new app ran as a preview at
`/next` next to the classic app, and several of the features below arrived there first. If you last used the classic
app, everything on this page is new to you.

Your data, projects, API keys, and clients carry over. The event submission endpoints are unchanged, so apps that send
events today keep working without a client update.

## A new app

The new app is now the only app, served at the root of the site. The classic app and the `/next` preview are gone.

- **Your old links still work.** Bookmarks to classic dashboards, account notification settings, organization billing,
  and project integrations open the matching page in the new app. See [Where things moved](#where-things-moved).
- **Command palette.** Press <kbd>/</kbd> to search events and stacks, jump to a page, switch organizations, or run an
  action.
- **Keyboard shortcuts.** Press <kbd>?</kbd> to see them all. For example, <kbd>I</kbd> opens **Stacks**, <kbd>E</kbd>
  opens **Events**, and <kbd>A</kbd> opens Exie.
- **Guided tours.** Short walkthroughs of setup and investigation. Start one from **Help** > **Guided Tours** in your
  user menu.
- **Light and dark themes.** Pick one, or follow your system setting, on your account's **Appearance** page.

See [Navigating the app](/docs/navigation/).

## Saved views replace the fixed dashboards

The classic app had a fixed set of dashboards for each event type: a dashboard, most frequent, new, and recent. The new
app has three pages, **Stacks**, **Events**, and **Sessions**, and each one shows a saved view: a filter, a time range,
a sort order, and the columns and charts to show.

Every organization starts with built-in views that cover the classic dashboards:

- **Stacks**: All, Most Frequent Errors, Most Frequent 404s, and Most Used Features.
- **Events**: All, Errors, and Logs.
- **Sessions**: All.

Change the filter or date range, then save your own view. Views are shared with the organization unless you mark them
**Private**, and they appear in the sidebar under their page. The project and date range are filters on each view, and
they are saved with it, instead of a global picker at the top of the app. See [Saved views](/docs/saved-views/).

### Custom date ranges

Choose **Custom range** in the date filter to pick dates on a calendar or type them into **From** and **To** fields on
the **Absolute** tab. The **Relative** tab keeps the date math expressions, such as `now-7d`. The quick ranges, from
**Last 5 minutes** to **Last 90 days**, still apply with one click. See
[Filtering and searching](/docs/filtering-and-searching/).

## Exie, the AI assistant

Exie is an assistant built into the app. Ask it which errors matter this week, what changed after a release, or why a
stack keeps happening. It searches your stacks and events, counts events over time, and reads stack traces. When you ask
it to, it also marks a stack fixed, snoozes or ignores it, marks it critical, or adds a reference link. It changes
nothing unless you ask for that exact change.

- Open it in a side panel with **Ask Exie** in the top bar or <kbd>A</kbd>, or full page with **Exie** in the sidebar.
- On a stack, **Fix with Exie** starts a conversation about that error.
- In 9.0, Exie shows each lookup and change as it works, lets you stop, retry, or edit a question, and keeps recent
  chats in your browser.

Exie is on Medium plans and up. See [Exie](/docs/ai/exie/).

## The MCP server and connected apps

The Exceptionless MCP server lets your coding agent, such as Claude Code or Codex, work with your Exceptionless data on
every plan. The agent signs in through your browser: you approve the request, choose the organizations it can use, and
choose what it can do. It can read projects, stacks, and events. Changing stacks needs the separate `stacks:write`
permission.

- **AI Tools** in your user menu shows the setup command for each agent.
- The **Applications** page in your account lists the apps you've connected, and lets you revoke them.

See [MCP server](/docs/ai/mcp-server/) and [AI privacy and security](/docs/ai/privacy/).

## Environments

Events can now carry a top-level `environment`, such as `production` or `staging`. **Stacks**, **Events**, and
**Sessions** have an **Environment** filter, including **Unspecified** for events without one, and the filter is saved
with your views. Environments don't split stacks: one error in staging and production is still one stack with one
status. See [Environments](/docs/environments/) for how to send it.

## JavaScript source maps

Exceptionless downloads public source maps for minified JavaScript errors and shows the original file names, functions,
and lines in the stack trace. For maps that are private or not published with your scripts, choose **Manage source
maps** on the project's **Settings** page and upload them, or upload them from CI with a project-scoped token. See
[Source maps](/docs/clients/javascript/source-maps/).

## Sign-in changes

- **Microsoft sign-in** replaces Windows Live. If you used Windows Live before, sign in with **Microsoft** using the
  same email address, and your account picks up the new login.
- **Facebook sign-in is gone.** If you signed in with Facebook, sign in with Microsoft, Google, or GitHub using the same
  email address, or use **Forgot password?** on the login page to set a password.

Manage your logins on your account's **External Logins** page.

## API changes

- **API reference.** The interactive reference is at `/docs` on the API host, for example
  `https://api.exceptionless.io/docs`, with the OpenAPI document at `/docs/v2/openapi.json`. Choose **Help** > **API
  Reference** in your user menu to open it.
- **OAuth access tokens.** Besides API keys and user tokens, the REST API accepts OAuth access tokens issued to apps you
  connect, limited by scopes such as `projects:read`, `stacks:read`, `stacks:write`, and `events:read`. See
  [Authentication](/docs/api/authentication/).
- **Environment.** Events accept and return a top-level `environment` string, and webhook payloads include it. A valid
  root `environment` is no longer stored as `data.environment`. See the [event format](/docs/reference/event-format/).
- **Saved views.** Saved views have their own endpoints under `/api/v2/organizations/{organizationId}/saved-views` and
  `/api/v2/saved-views/{id}`.
- **Sign-in endpoints.** `POST /api/v2/auth/microsoft` replaces `/api/v2/auth/live`, and `/api/v2/auth/facebook` is
  removed.

## Clients

The current clients are Exceptionless.Net 6.2.0 and Exceptionless.JavaScript 3.2.1.

- **.NET 6.2.** The `Exceptionless.AspNetCore`, `Exceptionless.Extensions.Hosting`, and
  `Exceptionless.Extensions.Logging` packages require .NET 8 or later. ASP.NET Core setup uses
  `builder.AddExceptionless()` and `app.UseExceptionless()`; `app.UseExceptionless(Configuration)` is gone. See
  [Upgrading the .NET client](/docs/clients/dotnet/upgrading/).
- **JavaScript 3.2.** Adds `@exceptionless/react-native` for React Native and Expo apps. See
  [React Native and Expo](/docs/clients/javascript/guides/react-native/).
- **Agent skills.** Both client repositories include an agent skill that teaches coding agents how to set up the client.
  See [Coding agents](/docs/ai/coding-agents/).

## Self-hosting

- **Runtime.** Exceptionless runs on .NET 10 and Elasticsearch 8.19. The Docker images and Compose files use
  `exceptionless/elasticsearch:8.19.22`.
- **One app.** The `app` and all-in-one images serve only the new app.
- **Trusted reverse proxies.** The API now accepts `X-Forwarded-For` and `X-Forwarded-Proto` only from loopback or from
  proxies you list under `ForwardedHeaders` (`KnownProxies` or `KnownNetworks`). If a reverse proxy or load balancer
  sits in front of the API on another host, configure it before you upgrade, or client IPs and HTTPS detection will be
  wrong. Don't set `ASPNETCORE_FORWARDEDHEADERS_ENABLED`; the API refuses to start when it's enabled. See
  [Reverse proxies and HTTPS](/docs/self-hosting/reverse-proxy/).
- **Login providers.** Microsoft sign-in still reads `MicrosoftId` and `MicrosoftSecret` from the OAuth connection
  string. `FacebookId` and `FacebookSecret` are no longer used. See [Configuration](/docs/self-hosting/configuration/).
- **Environments migration.** A migration adds the `environment` field to existing event indexes. Let it finish before
  the new API and job instances process events.
- **AI features.** Exie needs an `Assistant` endpoint, model, and API key, and the MCP server uses the built-in OAuth
  server. See [AI features](/docs/self-hosting/ai/).

To upgrade, follow [Upgrading a self-hosted instance](/docs/self-hosting/upgrading-self-hosted-instance/).

## Where things moved {#where-things-moved}

| In the classic app                             | In 9.0                                                                                                                           |
| ---------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Most frequent and new dashboards               | **Stacks**, with a saved view for each kind of event                                                                             |
| Dashboard and recent (timeline) pages          | **Events**                                                                                                                       |
| Sessions dashboard                             | **Sessions**                                                                                                                     |
| Project and date pickers at the top of the app | Filters on each page, saved with the view                                                                                        |
| Download & Configure Client                    | **Client Setup**, in the project's actions menu under **Settings** > **Projects**                                                |
| Project settings tabs                          | Project pages: **General**, **API Keys**, **Settings**, **Configuration**, **Integrations**, **Stack Management**, and **Usage** |
| My Account                                     | **Account**, **Notifications**, and **AI Tools** in your user menu                                                               |
| Organization billing tab                       | **Billing** in your user menu                                                                                                    |

## Ask Exie

```prompt exie
What are my top errors this week?
```

```prompt exie
Which stacks regressed in the last 7 days?
```

With the [MCP server](/docs/ai/mcp-server/) connected, your coding agent can do the same:

```prompt agent
Find the most frequent open error in my Exceptionless project, fix it in this codebase, and mark the stack fixed.
```
