# Spec: Organizations, Projects, Users & Auth

Baseline spec for multi-tenancy, access control, and user management.

> **Implementation-derived values** are included to guide future changes, but they are not public compatibility contracts unless explicitly stated in the relevant requirement.

## Organization

The top-level tenant entity. Owns projects, users, billing plans, and usage limits.

### Key Operations

- `GET /api/v2/organizations` — list (supports `?filter=`)
- `GET /api/v2/organizations/{id}` — get by ID
- `POST /api/v2/organizations` — create `{ name }`
- `PATCH /api/v2/organizations/{id}` — update
- `DELETE /api/v2/organizations/{id}` — delete
- `GET /api/v2/organizations/check-name/{name}` — name availability
- `POST /api/v2/organizations/{id}/change-plan` — change billing plan (body: `{ plan_id }` or query: `?planId=`)
- `GET /api/v2/organizations/{id}/invoices` — billing history
- `POST /api/v2/organizations/{id}/users/{email}` — invite user
- `DELETE /api/v2/organizations/{id}/users/{email}` — remove user
- `POST /api/v2/organizations/{id}/suspend` — admin: suspend org
- `DELETE /api/v2/organizations/{id}/suspend` — admin: unsuspend
- `POST /api/v2/organizations/{id}/data/{key}` — set custom data
- `DELETE /api/v2/organizations/{id}/data/{key}` — remove custom data
- `POST/DELETE /api/v2/organizations/{id}/features/{feature}` — toggle features

## Project

Belongs to an organization. Scopes events, tokens, and configuration.

### Key Operations

- `GET /api/v2/projects?organizationId={id}` — list by org
- `GET /api/v2/projects/{id}` — get by ID
- `POST /api/v2/projects` — create `{ OrganizationId, Name }`
- `GET /api/v2/projects/{id}/tokens/default` — default API key
- `POST /api/v2/projects/{id}/tokens` — create new API key
- `GET /api/v2/projects/{id}/config` — project configuration
- `POST /api/v2/projects/{id}/config?key={key}` — set config value
- `DELETE /api/v2/projects/{id}/config?key={key}` — delete config value
- `POST /api/v2/projects/{id}/sample-data` — generate sample events
- `POST /api/v2/projects/{id}/reset-data` — reset project data
- `GET /api/v2/projects/check-name/{name}` — name availability

## User

- `GET /api/v2/users/me` — current user profile
- `GET /api/v2/users/{id}` — get user by ID

## Auth

- `POST /api/v2/auth/login` — email/password login → `{ token }`
- `POST /api/v2/auth/signup` — create account `{ email, password, name }`
- `POST /api/v2/auth/{provider}` — OAuth (google, etc.) with `{ code, clientId, redirectUri }`
- `GET /api/v2/auth/intercom` — Intercom identity verification
- Password reset flows (forgot-password, reset-password with token)

## Tokens

- Scoped to organization and/or project.
- Types: user bearer tokens (from login) and client API keys (for event submission).
- `GET /api/v2/tokens/{id}`, `POST /api/v2/tokens`, `DELETE /api/v2/tokens/{id}`
- Organization tokens: `GET /api/v2/organizations/{id}/tokens`
- Project tokens: `GET /api/v2/projects/{id}/tokens`

## Authorization Policies

- `AuthorizationRoles.UserPolicy` — standard authenticated user
- Admin endpoints require global admin role
- Organization/project membership enforced per request

## Billing Plans

- Plan changes via `change-plan` endpoint.
- Plans identified by string IDs (e.g., `EX_FREE`).
- Stripe integration via `StripeController` webhook receiver.

## Compatibility Boundaries

- Organization/project/user IDs are ObjectId format strings.
- Token format and authentication mechanisms (Bearer header, `access_token` param) are SDK contracts.
- Plan IDs and billing flows are integration contracts with Stripe.
- User invitation is email-based (not username).

## Requirements

### Requirement: Organizations support unlimited users

Exceptionless must not impose a documented fixed maximum user count per organization unless a future plan or self-hosting limit explicitly defines one.

#### Scenario: Organization membership grows

Given an organization has existing users
When additional users are invited or added
Then the system must not reject the operation solely because of a documented hard maximum user count.

### Requirement: API authorization scopes remain compatible

Exceptionless API authorization must preserve documented `client` and `user` token scope behavior and source-defined global administrative access unless an intentional compatibility-impacting change is approved.

#### Scenario: Token scope behavior changes

Given a change modifies token scopes, authorization policies, or API key authentication
When the change is proposed
Then the change must document compatibility impact for existing project tokens, user tokens, and global administrative access.

### Requirement: External authentication provider setup remains documented where supported

Exceptionless self-hosting documentation includes setup guidance for Slack and LDAP/Active Directory authentication. Source configuration also supports additional OAuth providers (Microsoft, Facebook, GitHub, Google).

#### Scenario: OAuth provider behavior changes

Given a change modifies OAuth provider configuration, required scopes, redirect behavior, or provider enablement
When the change is proposed
Then the change must document affected provider setup requirements and update self-hosting documentation when needed.

**Note:** Microsoft, Facebook, GitHub, and Google OAuth provider configuration exists in source. Public setup documentation is not required for these providers at this time.
