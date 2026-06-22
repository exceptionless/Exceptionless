# Proposal: Add OAuth API Access

## Summary

Add OAuth 2.1-compatible API access for Exceptionless so external tools, including remote MCP clients, can request user-approved, scoped access without users pasting long-lived Exceptionless API tokens.

The initial implementation will be built into `Exceptionless.Web` and will support authorization-code with PKCE, refresh tokens, OAuth discovery metadata, Client ID Metadata Documents for zero-config AI client onboarding, resource/audience validation, and read-only scopes for MCP project, stack, and event access.

## Classification

- **Type:** Feature + API change + security-sensitive auth change
- **Affected areas:** Backend/API, auth/token model, MCP endpoint, HTTP samples, integration tests
- **OpenSpec justification:** Adds public auth endpoints, new bearer token semantics, external client integration, token lifecycle behavior, and scope enforcement.

## Compatibility Risks

| Risk | Mitigation |
|------|------------|
| Existing API key and login token behavior | Preserve current token auth; OAuth support is additive |
| Existing `Authorization: Bearer` handling | Extend validation for OAuth tokens without removing current behavior |
| MCP endpoint access | Continue accepting existing authenticated user tokens while adding scoped OAuth tokens |
| Token persistence/indexing | Reuse existing `Token` storage where possible; add fields additively |
| Public API surface | Add endpoints under `/api/v2/oauth/*` plus well-known metadata; update HTTP samples |

## Rollback Plan

- OAuth endpoints are additive and can be disabled or removed without breaking existing API key clients.
- Existing API key and authentication-token routes remain unchanged.
- If scoped OAuth enforcement causes integration issues, MCP can temporarily continue using `UserPolicy` while OAuth token issuance remains disabled.
