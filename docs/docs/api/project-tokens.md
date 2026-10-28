---
title: "Client-scoped tokens"
description: "Create, disable, and delete client API keys for an Exceptionless project in the app or with the REST API."
sources:
  - src/Exceptionless.Web/Api/Endpoints/TokenEndpoints.cs
  - src/Exceptionless.Web/Api/Handlers/TokenHandler.cs
  - src/Exceptionless.Web/Api/Endpoints/ProjectEndpoints.cs
  - src/Exceptionless.Web/Models/Token/NewToken.cs
  - src/Exceptionless.Web/Models/Token/ViewToken.cs
  - src/Exceptionless.Web/Models/Token/UpdateToken.cs
  - src/Exceptionless.Web/Models/Project/ViewProject.cs
  - src/Exceptionless.Web/ClientApp/src/routes/(app)/project/[projectId]/api-keys/+page.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/tokens/components/table/token-actions-cell.svelte
  - src/Exceptionless.Web/ClientApp/src/lib/features/tokens/project-token.ts
---

# Client-scoped tokens

A client-scoped token, or client API key, lets an app send events to one project and nothing else. Create one for each
app or environment so you can turn it off on its own. This page shows how to manage keys in the app and with the API.
For what each kind of token can do, see [Authentication](/docs/api/authentication/).

## Manage keys in the app {#in-the-app}

1. Open **Settings** > **Projects** in the sidebar and choose your project.
2. Select the **API Keys** tab.
3. Choose **Add token**, then **Client API key**. Choose **Source map upload token** instead for a key that can only
   upload source maps.

![The API Keys tab with the Add token menu open](/assets/img/docs/project-settings-api-keys-light.webp)

Each key's menu has **Copy Token**, **Edit Notes**, **Disable Token** (or **Enable Token**), and **Delete**. A disabled
key gets `401` until you enable it again, so disabling is a safe way to cut off one app.

The **Client setup** link at the top of the tab opens the project's setup steps with the project's default key already
filled in.

## Manage keys with the API {#with-the-api}

Creating and managing keys needs a [user token](/docs/api/authentication/#user-tokens). Requests made with an API key
can't create or list keys and get `403`.

### Get projects {#get-projects}

You need the project's ID. List your projects:

```bash
curl https://api.exceptionless.io/api/v2/projects \
  --header "Authorization: Bearer YOUR_USER_TOKEN"
```

The response is an array of projects. This example is trimmed:

```json
[
  {
    "id": "537650f3b77efe23a47914f4",
    "created_utc": "2026-01-11T20:05:59.718Z",
    "organization_id": "537650f3b77efe23a47914f3",
    "organization_name": "Acme",
    "name": "Checkout API",
    "delete_bot_data_enabled": false,
    "is_configured": true,
    "stack_count": 42,
    "event_count": 1873,
    "has_premium_features": true,
    "has_slack_integration": false
  }
]
```

Use `limit` (1 to 100, default 10) and `page` to see more; see [Paging](/docs/api/getting-events/#paging). The project
ID is also in the app's URL when you open the project's settings.

### Create a client API key {#create}

```bash
curl https://api.exceptionless.io/api/v2/projects/YOUR_PROJECT_ID/tokens \
  --request POST \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --header "Content-Type: application/json" \
  --data '{ "scopes": ["client"], "notes": "Checkout API, production" }'
```

The server responds `201 Created`. The `id` is the key:

```json
{
  "id": "LhhP1C9gijpSKCslHHCvwdSIz298twx271nTest",
  "organization_id": "537650f3b77efe23a47914f3",
  "project_id": "537650f3b77efe23a47914f4",
  "scopes": ["client"],
  "notes": "Checkout API, production",
  "is_disabled": false,
  "is_suspended": false,
  "created_utc": "2026-10-27T14:02:54.186Z",
  "updated_utc": "2026-10-27T14:02:54.186Z"
}
```

- With no body, or no `scopes`, the key gets the `client` scope.
- Use `"scopes": ["source-maps:write"]` for a source map upload token.
- `notes` is optional. `expires_utc` sets an expiration date.

`GET /api/v2/projects/{projectId}/tokens/default` returns the project's default client key, and creates one if the
project has none.

### List, disable, and delete keys {#list-disable-delete}

| Task                  | Request                                                        |
| --------------------- | -------------------------------------------------------------- |
| List a project's keys | `GET /api/v2/projects/{projectId}/tokens`                      |
| Disable a key         | `PATCH /api/v2/tokens/{token}` with `{ "is_disabled": true }`  |
| Enable a key          | `PATCH /api/v2/tokens/{token}` with `{ "is_disabled": false }` |
| Change notes          | `PATCH /api/v2/tokens/{token}` with `{ "notes": "New notes" }` |
| Delete a key          | `DELETE /api/v2/tokens/{token}`                                |

```bash
curl https://api.exceptionless.io/api/v2/tokens/YOUR_API_KEY \
  --request PATCH \
  --header "Authorization: Bearer YOUR_USER_TOKEN" \
  --header "Content-Type: application/json" \
  --data '{ "is_disabled": true }'
```

## Use a coding agent {#coding-agent}

```prompt agent
Write a script that uses my Exceptionless user token from an environment variable to create a client API key for each project in my organization with the note "rotated" and print the project name and new key.
```
