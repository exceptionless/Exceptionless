# Spec: OAuth API Access

## ADDED: OAuth authorization server metadata

### Requirement: Clients can discover OAuth authorization metadata

Given an unauthenticated client
When it sends `GET /.well-known/oauth-authorization-server`
Then the response is 200 with OAuth metadata including authorization endpoint, token endpoint, revocation endpoint, supported grant types, supported code challenge methods, supported scopes, and whether Client ID Metadata Documents are supported.

### Requirement: Clients can discover protected resource metadata

Given an unauthenticated client
When it sends `GET /.well-known/oauth-protected-resource`
Then the response is 200 with the resource identifier and at least one authorization server.

## ADDED: Authorization-code PKCE flow

### Requirement: Global admins can manage OAuth applications

Given a global admin
When they use the new Svelte system OAuth Apps page
Then they can create, list, update, disable, and delete OAuth applications backed by the Exceptionless data store.

#### Scenario: Duplicate client id is rejected

Given an existing OAuth application
When a global admin creates or updates another OAuth application with the same client id
Then the response is 422 Unprocessable Entity with a validation error for `client_id`.

#### Scenario: Non-global users cannot manage OAuth applications

Given an authenticated non-global user
When they call an admin OAuth application endpoint
Then the response is 403 Forbidden.

### Requirement: User can approve OAuth access for a registered client

Given an authenticated Exceptionless user
And a registered OAuth client with the requested redirect URI
When the user completes `GET /api/v2/oauth/authorize` with a valid `client_id`, `redirect_uri`, `scope`, `state`, `code_challenge`, `code_challenge_method=S256`, and `resource`
Then Exceptionless redirects back to the redirect URI with an authorization code and the original state.

### Requirement: User can approve OAuth access for a CIMD client

Given an authenticated Exceptionless user
And an unknown OAuth client id that is an HTTPS Client ID Metadata Document URL
When the user completes `GET /api/v2/oauth/authorize` with values matching the fetched client metadata document
Then Exceptionless persists the observed OAuth application and redirects back to the redirect URI with an authorization code and the original state.

#### Scenario: Insecure CIMD client id is rejected

Given an authenticated Exceptionless user
When the authorization request uses an unknown `client_id` that is not an HTTPS metadata document URL
Then the response is 400 Bad Request.

#### Scenario: Mismatched CIMD client id is rejected

Given an authenticated Exceptionless user
And the fetched client metadata document has a `client_id` that does not exactly match the requested client id
When the authorization request is validated
Then the response is 400 Bad Request and no OAuth application is persisted.

#### Scenario: Missing or unsupported PKCE is rejected

Given an authenticated Exceptionless user
When the authorization request omits `code_challenge` or uses a method other than `S256`
Then the response is 400 Bad Request.

#### Scenario: Unregistered redirect URI is rejected

Given an authenticated Exceptionless user
When the authorization request uses a redirect URI not registered for the client
Then the response is 400 Bad Request.

#### Scenario: Unauthorized scope is rejected

Given an authenticated Exceptionless user
When the authorization request includes a scope not allowed for the client
Then the response is 400 Bad Request.

#### Scenario: Disabled client is rejected

Given a registered or observed OAuth client is disabled
When a user or client attempts to use that client for authorization, token exchange, refresh, or OAuth bearer authentication
Then the request is rejected.

## ADDED: OAuth token exchange

### Requirement: Client can exchange authorization code for scoped tokens

Given a valid authorization code issued to a registered client
When the client sends `POST /api/v2/oauth/token` with grant type `authorization_code`, the original resource, and a matching PKCE verifier
Then the response is 200 with an access token, token type `Bearer`, expiration, scopes, and refresh token when `offline_access` was granted.

#### Scenario: Invalid PKCE verifier is rejected

Given a valid authorization code
When the client exchanges it with the wrong `code_verifier`
Then the response is 400 Bad Request.

#### Scenario: Authorization code cannot be reused

Given a previously exchanged authorization code
When the client exchanges it again
Then the response is 400 Bad Request.

### Requirement: Client can refresh access

Given a valid refresh token
When the client sends `POST /api/v2/oauth/token` with grant type `refresh_token`
Then the response is 200 with a new access token and rotated refresh token.

#### Scenario: Reused refresh token is rejected

Given a refresh token that has already been rotated
When the client uses it again
Then the response is 400 Bad Request.

## ADDED: OAuth bearer access to MCP/API

### Requirement: OAuth bearer token can access MCP read tools

Given an OAuth access token with `mcp:read`, `projects:read`, `stacks:read`, and `events:read`
When the client calls `POST /mcp` with `Authorization: Bearer <token>`
Then MCP tool listing and read-only project, stack, and event tools can run for the authenticated user's accessible organizations and projects.

#### Scenario: Missing scope is forbidden

Given an OAuth access token missing `mcp:read`
When the client calls `POST /mcp`
Then the response is 403 Forbidden.

#### Scenario: Mismatched resource is unauthorized

Given an OAuth access token issued for a different resource
When the client calls `POST /mcp`
Then the response is 401 Unauthorized.

## ADDED: OAuth revocation

### Requirement: Client can revoke OAuth tokens

Given a valid OAuth access or refresh token
When the client sends `POST /api/v2/oauth/revoke`
Then the token is disabled or otherwise made unusable and subsequent use fails.

## MODIFIED: Existing API token compatibility

### Requirement: Existing API tokens continue to work

Given an existing Exceptionless API key or authentication token
When it is sent using the existing supported authentication mechanisms
Then existing API endpoints continue to authenticate as before.

### Requirement: HTTP samples reflect new endpoints

Given OAuth endpoints are added
When endpoint samples are updated
Then `tests/http/*.http` includes localhost-only examples for OAuth metadata, token exchange, refresh, revocation, and MCP access.
