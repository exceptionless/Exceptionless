# Design: Add OAuth API Access

## Endpoint Surface

Add OAuth endpoints to `Exceptionless.Web`:

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/.well-known/oauth-authorization-server` | Authorization server metadata |
| GET | `/.well-known/oauth-protected-resource` | Resource metadata for MCP/API clients |
| GET | `/api/v2/oauth/authorize` | Authorization-code + PKCE consent entrypoint |
| POST | `/api/v2/oauth/token` | Exchange authorization code or refresh token |
| POST | `/api/v2/oauth/revoke` | Revoke access or refresh token |
| GET | `/api/v2/admin/oauth-applications` | List registered OAuth clients for global admins |
| POST | `/api/v2/admin/oauth-applications` | Create a registered OAuth client |
| PUT | `/api/v2/admin/oauth-applications/{id}` | Update a registered OAuth client |
| DELETE | `/api/v2/admin/oauth-applications/{id}` | Delete a registered OAuth client |

The first pass exposes admin-only OAuth client management in the new Svelte system UI. It also supports Client ID Metadata Documents (CIMD) for AI clients that use an HTTPS metadata-document URL as `client_id`, so clients can be observed and persisted without manual registration. Full dynamic client registration is intentionally deferred.

## Domain Model

Reuse existing `Token` behavior where practical because it already supports:

- user-scoped tokens
- scopes
- expiration
- refresh token value
- disabled/suspended state

Add additive metadata if needed:

- `ClientId`
- `Audience`
- `TokenUse` or equivalent discriminator for OAuth access/refresh/code tokens
- hashed refresh/access token material for new OAuth tokens when feasible

Add an OAuth client model for registered clients:

- client id
- display name
- redirect URIs
- allowed scopes
- disabled state
- audit metadata

CIMD-observed clients use the same model. Exceptionless fetches the metadata document only for unknown HTTPS URL client ids during authorization, validates the returned `client_id`, redirect URIs, grant type, response type, token endpoint auth method, and scopes, then persists the observed client for later token exchange and bearer validation.

## Authorization Flow

1. Client sends `GET /api/v2/oauth/authorize` with `client_id`, `redirect_uri`, `scope`, `state`, `code_challenge`, `code_challenge_method=S256`, and `resource`.
2. Exceptionless requires a logged-in user.
3. Exceptionless validates the client, exact redirect URI, resource, and scopes.
4. User approves the request.
5. Exceptionless redirects to the client with `code` and original `state`.
6. Client exchanges `code` at `/api/v2/oauth/token` with `code_verifier` and `resource`.
7. Exceptionless issues a short-lived access token and a refresh token.

## Scopes

Initial scopes:

- `mcp:read`
- `projects:read`
- `stacks:read`
- `events:read`
- `offline_access`

OAuth tokens should map scopes into claims. Existing `user`, `client`, and `global` role semantics remain available for existing tokens.

## MCP Integration

The MCP endpoint currently maps to `/mcp`. The OAuth implementation should:

- advertise the MCP/API resource URI in protected resource metadata
- require bearer tokens in the `Authorization` header
- reject OAuth access tokens with a mismatched resource/audience
- require `mcp:read` for MCP access
- require project/stack/event scopes in MCP tool handlers or endpoint policy

Existing user-authentication tokens can keep working during the initial pass to avoid breaking local testing.

## Security

- Require PKCE S256 for authorization-code flow.
- Validate exact redirect URIs.
- Preserve and return `state`.
- Require `resource` and validate audience/resource on token use.
- Use short-lived access tokens.
- Rotate refresh tokens.
- Do not log token values, authorization codes, or refresh tokens.
- Return 401 for invalid/expired tokens and 403 for insufficient scopes.
- Do not accept OAuth tokens in query strings for MCP OAuth usage.
- Only fetch client metadata documents over HTTPS and block loopback, private, link-local, and userinfo-bearing targets.
- Do not auto-discover clients during token refresh or OAuth bearer authentication; those paths require the persisted OAuth application record.

## Operations

- No production secrets are committed.
- Manual and CIMD-observed client registration is repository-backed and managed by global admins in the new Svelte system UI.
- Well-known metadata should use request host/scheme so local Aspire and production hosts both work.

## Tests

Add targeted integration tests for metadata, authorize validation, token exchange, refresh, revocation, resource validation, scope validation, MCP calls with OAuth bearer tokens, and admin OAuth client CRUD.
