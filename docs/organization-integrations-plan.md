# Organization Integrations Plan

## Current State

- External logins are user identity links on `User.OAuthAccounts`. They are not organization integrations.
- OAuth applications are global client definitions stored in `OAuthApplication` and managed through `OAuthApplicationController` at `/api/v2/admin/oauth-applications`. This is a system administrator registry.
- Hosted MCP access is authorized through OAuth. Issued OAuth access tokens store `UserId`, `OAuthClientId`, `OAuthResource`, scopes, expiration, and disabled state.
- OAuth tokens do not currently store the selected organization. Consent displays the organizations a user can access, but the token grants user-scoped access across the user's accessible organizations.

## Product Model

- Keep OAuth applications as global client definitions.
- Add organization integrations as organization-owned installations or grants.
- Treat AI tools as integration setup surfaces that initiate OAuth, not as the same object as a registered OAuth application.
- Treat external logins separately because they answer "how this user signs in," not "what this organization connected."

## Backend Work

1. Introduce an organization-scoped OAuth grant or integration model.
    - Recommended fields: `OrganizationId`, `UserId`, `OAuthClientId`, `OAuthResource`, scopes, status, created and updated timestamps, last-used timestamp, and optional display metadata from `OAuthApplication`.
    - Either add `OrganizationId` to OAuth access tokens and require organization selection during authorization, or create a separate durable installation record that tokens reference.

2. Update OAuth authorization consent.
    - Require an explicit organization selection when an OAuth client requests MCP/API access.
    - Persist the selected organization with the authorization code and issued token.
    - Keep the existing scope validation and PKCE behavior.

3. Add organization integrations API endpoints.
    - `GET /api/v2/organizations/{organizationId}/integrations`
    - `GET /api/v2/organizations/{organizationId}/integrations/{id}`
    - `DELETE /api/v2/organizations/{organizationId}/integrations/{id}` or a disable endpoint for revocation.
    - Return a view model that joins grant/token state with OAuth application display data.

4. Add repository support and indexing.
    - If tokens become the source of truth, map `OAuthType`, `OAuthClientId`, and `OAuthResource` in `TokenIndex`, then add focused repository methods for active OAuth grants by organization.
    - If a new integration model is introduced, add a repository, index mapping, and entity-changed messages scoped by organization.

5. Add tests and API samples.
    - Controller tests for listing, viewing, and revoking organization integrations.
    - OAuth flow tests proving authorization is organization-scoped.
    - Authorization tests proving users cannot list or revoke another organization's integrations.
    - Update `tests/http/oauth.http` and add organization integrations samples when endpoints land.

## Frontend Work

- The organization settings area now has an Integrations tab with AI tool setup, including GitHub Copilot.
- The Integrations page also has an OAuth Applications tab that reuses the same list, create, edit, disable, and delete management flow as the System OAuth Apps page.
- Once the backend endpoints exist, add an organization integrations feature slice with TanStack Query hooks and render connected OAuth/MCP clients on the same tab.
- Keep the System OAuth Apps screen for global client registration and administration.
