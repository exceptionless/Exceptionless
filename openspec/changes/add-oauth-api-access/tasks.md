# Tasks: Add OAuth API Access

## Spec

- [x] **Task 1: Create OpenSpec artifacts**
  - Proposal, design, tasks, and spec created under `openspec/changes/add-oauth-api-access`.
  - Verification: inspect files and run OpenSpec validation if CLI is available.

## Backend

- [x] **Task 1.5: Add repository-backed OAuth applications**
  - Add OAuth application model, Elasticsearch index, repository, and DI registration.
  - Validate redirect URIs, allowed scopes, disabled state, and audit metadata.
  - Verification: build and focused integration tests.

- [x] **Task 2: Add OAuth domain models and validation**
  - Add registered OAuth client representation and OAuth token metadata.
  - Keep all model changes additive.
  - Verification: targeted model/validator tests or build.

- [x] **Task 3: Add OAuth service**
  - Validate clients, redirect URIs, PKCE, scopes, resource, authorization codes, access tokens, refresh tokens, and revocation.
  - Verification: service/unit tests for happy path and negative paths.

- [x] **Task 4: Add OAuth controller and metadata endpoints**
  - Add well-known metadata, authorize, token, and revoke routes.
  - Verification: integration/API tests plus `tests/http/oauth.http`.

- [x] **Task 4.5: Add Client ID Metadata Document support**
  - Fetch unknown HTTPS URL client ids during authorization only.
  - Validate metadata client id, redirect URIs, grant type, response type, token auth method, and supported scopes.
  - Persist observed clients to the OAuth application repository and expose them in the Svelte management page.
  - Block insecure/private metadata document targets and require persisted records for token refresh and bearer validation.
  - Verification: focused OAuth controller tests for metadata discovery, persistence, token exchange, and rejection paths.

- [x] **Task 5: Extend bearer authentication for OAuth tokens**
  - Map OAuth token scopes to claims and validate expiration/resource.
  - Preserve existing API key and authentication-token behavior.
  - Verification: authentication integration tests.

- [x] **Task 6: Apply scope checks to MCP**
  - Require `mcp:read` and relevant read scopes for OAuth-backed MCP calls.
  - Keep existing user-token MCP calls working during the initial pass.
  - Verification: MCP HTTP smoke tests with OAuth bearer token.

## New UI

- [x] **Task 6.5: Add Svelte system OAuth Apps management**
  - Add global-admin route under `/system/oauth-applications`.
  - Support create, list, edit, disable, copy client id, and delete.
  - Verification: `npm run check` and `npm run build`.

## Tests And Samples

- [x] **Task 7: Add HTTP samples**
  - Add OAuth application management, metadata, authorize, token, refresh, revoke, and MCP bearer examples.
  - Verification: `tests/http/oauth.http` is present and uses localhost.

- [x] **Task 8: Run focused verification**
  - `dotnet build src/Exceptionless.Web/Exceptionless.Web.csproj --no-restore -v:minimal -p:SkipSpaPublish=true -m:1`
  - `dotnet test tests/Exceptionless.Tests/Exceptionless.Tests.csproj --no-build -- --filter-class Exceptionless.Tests.Controllers.OAuthApplicationControllerTests --filter-class Exceptionless.Tests.Controllers.OAuthControllerTests`
  - `npm run check`
  - `npm run build`
  - local Aspire smoke test if feasible
  - OpenSpec validation if CLI is available
