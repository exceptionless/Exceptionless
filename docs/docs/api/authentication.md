---
title: "Authentication"
description: "Authenticate with the Exceptionless REST API using client API keys, user tokens, or OAuth bearer tokens, and learn what each token can do and how to send it."
redirectFrom:
  - /docs/api/api-getting-started/
sources:
  - src/Exceptionless.Web/Security/ApiKeyAuthenticationHandler.cs
  - src/Exceptionless.Web/Program.cs
  - src/Exceptionless.Web/Api/Endpoints/AuthEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/AuthHandler.cs
  - src/Exceptionless.Web/Api/Endpoints/OAuthEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/OAuthHandler.cs
  - src/Exceptionless.Web/Api/Handlers/TokenHandler.cs
  - src/Exceptionless.Core/Authorization/AuthorizationRoles.cs
  - src/Exceptionless.Core/Extensions/IdentityUtils.cs
  - src/Exceptionless.Core/Services/OAuthService.cs
  - src/Exceptionless.Core/Configuration/OAuthServerOptions.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/account/security/+page.svelte
---

# Authentication

Every API request needs a token. This page explains the three kinds of token, what each one can do, and the ways to send
it.

## Choose a token {#choose-a-token}

| Token                                       | What it can do                                                                                                              | Get it from                                                     |
| ------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| [Client API key](#client-api-keys)          | Send events to one project, attach user descriptions, send session heartbeats, and read the project's configuration values. | The project's **API Keys** settings, or the API.                |
| [Source map upload token](#client-api-keys) | Upload JavaScript source maps to one project.                                                                               | The project's **API Keys** settings, or the API.                |
| [User token](#user-tokens)                  | Anything you can do in the app, across your organizations.                                                                  | `POST /api/v2/auth/login` with your email address and password. |
| [OAuth access token](#oauth-access-tokens)  | Read projects, stacks, and events, and update stacks, in the organizations you approve.                                     | The OAuth authorization flow, for apps that act on your behalf. |

Use the smallest token that does the job. Put client API keys in apps, a user token or OAuth token in tools that read
your data, and never ship a user token in an app.

## Client API keys {#client-api-keys}

A client API key belongs to one project and has the `client` scope. It's the key you give to a client library, a script,
or a server that sends events. A key can only reach its own project: posting to another project's route returns `404`.

A source map upload token is a project key with the `source-maps:write` scope instead. It can only upload source maps
for its project, so it's safe to keep in a CI/CD pipeline. See [Source maps](/docs/clients/javascript/source-maps/).

Create both kinds in the app or with the API; see [Client-scoped tokens](/docs/api/project-tokens/). You can disable or
delete a key at any time from the same page.

## User tokens {#user-tokens}

A user token acts as you. It can read and change everything you can in the app, in every organization you belong to, so
keep it out of source control and out of apps you ship.

Get one by logging in with your email address and password:

```bash
curl https://api.exceptionless.io/api/v2/auth/login \
  --request POST \
  --header "Content-Type: application/json" \
  --data '{ "email": "you@example.com", "password": "YOUR_PASSWORD" }'
```

```json
{ "token": "dqsWDGpHmaRkDB5kU8y9Tf0uqGfOBhC6sW8BnSlr" }
```

- The token is valid for three months. Logging in again while it's valid returns the same token.
- Wrong credentials return `401` with the title `Login failed.`. Repeated failures are throttled.
- To revoke the token you're using, call `GET /api/v2/auth/logout` with it.

> [!NOTE]
> If you signed up with GitHub, Google, or Microsoft, you don't have a password yet. In the app, open your account's
> **Password** page and choose **Set Password**, then log in with your email address and that password.

## OAuth access tokens {#oauth-access-tokens}

OAuth lets an app or tool use the API on a user's behalf without seeing their password. The user signs in, chooses the
organizations to share, and approves the scopes the app asks for.

For the REST API, request the resource `https://be.exceptionless.io/api/v2` and one or more of these scopes:

| Scope            | Allows                                                                                                   |
| ---------------- | -------------------------------------------------------------------------------------------------------- |
| `projects:read`  | Listing and reading projects.                                                                            |
| `stacks:read`    | Listing and reading stacks.                                                                              |
| `stacks:write`   | Changing stack status, snoozing, marking stacks critical or not, and adding or removing reference links. |
| `events:read`    | Listing, counting, and reading events and sessions.                                                      |
| `offline_access` | A refresh token, so the app can get new access tokens without asking the user again.                     |

An OAuth access token works only on the routes its scopes allow, and only in the approved organizations that the user
still belongs to. It can't send events or manage projects, tokens, or users. Access tokens last one hour and refresh
tokens 30 days by default.

The server supports the authorization code flow with PKCE (`S256`) for public clients. The OAuth server and its
resources are identified by the web app's origin, `https://be.exceptionless.io`, even though you send the API requests
themselves to `https://api.exceptionless.io`. Apps find the endpoints through discovery:

- `https://be.exceptionless.io/.well-known/oauth-protected-resource/api/v2` describes the REST API resource and its
  scopes.
- `https://be.exceptionless.io/.well-known/oauth-authorization-server` lists the authorization, token, registration, and
  revocation endpoints.

> [!NOTE]
> On a self-hosted instance, the origin is `EX_ApiUrl` if you set it, otherwise `EX_BaseURL`.

Users see and revoke the apps they've approved under their account's **Applications** page. AI tools use the same OAuth
server with the MCP resource; see [MCP server](/docs/ai/mcp-server/).

## Send a token {#send-a-token}

Use the `Authorization` header with the `Bearer` scheme:

```bash
curl https://api.exceptionless.io/api/v2/projects \
  --header "Authorization: Bearer YOUR_USER_TOKEN"
```

The API also accepts API keys and user tokens in these forms, for tools that can't set a bearer header:

| Form                      | Example                                                                  |
| ------------------------- | ------------------------------------------------------------------------ |
| `Token` scheme            | `Authorization: Token YOUR_API_KEY`                                      |
| Query string              | `?access_token=YOUR_API_KEY` (also `api_key` or `apikey`)                |
| Basic, token as password  | User name `client`, password `YOUR_API_KEY`                              |
| Basic, token as user name | User name `YOUR_API_KEY`, password empty or `x-oauth-basic`              |
| Basic, email and password | Your email address and password. Works like logging in, for one request. |

OAuth access tokens only work in the `Authorization: Bearer` header.

Prefer the header. Query strings end up in server and proxy logs, so use `access_token` only where you have no other
choice, such as a webhook URL or `GET /api/v2/events/submit`.

## When authentication fails {#errors}

| Status | Cause                                                                                                                             |
| ------ | --------------------------------------------------------------------------------------------------------------------------------- |
| `401`  | No token, or the token is wrong, expired, disabled, or belongs to a suspended organization.                                       |
| `403`  | The token is valid but can't call this route, such as a client API key reading events or an OAuth token without the needed scope. |
| `404`  | The resource doesn't exist, or it's in an organization or project the token can't reach.                                          |

## Use a coding agent {#coding-agent}

```prompt agent
Write a small script that logs in to the Exceptionless API at https://api.exceptionless.io with my email and password from environment variables, prints my organizations from GET /api/v2/organizations, and never writes the token to disk or logs.
```
