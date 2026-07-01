# Acceptance Criteria: Minimal API + Mediator + OpenAPI Migration

## Route Preservation

- **SHALL** preserve all existing v2 routes with identical HTTP methods, paths, and parameter binding.
- **SHALL** preserve existing v1 compatibility aliases with identical behavior.
- **SHALL** produce identical response status codes for all success and error cases.
- **SHALL** preserve response body shapes (JSON property names, nesting, types).
- **SHALL** preserve response headers (pagination, configuration version, rate-limit headers).
- **SHALL** preserve query parameter behavior (filtering, sorting, paging, time ranges).

## Authentication and Authorization

- **SHALL** preserve auth/authorization behavior for all endpoints.
- **SHALL** preserve `ApiKeyAuthenticationHandler` behavior (API key via header, query string, and bearer token).
- **SHALL** preserve role-based policies (UserPolicy, GlobalAdminPolicy) on all endpoints.
- **SHALL** preserve anonymous access on endpoints currently marked `[AllowAnonymous]`.

## Middleware

- **SHALL** preserve `ThrottlingMiddleware` behavior (rate limiting, response codes, headers).
- **SHALL** preserve `OverageMiddleware` behavior (plan enforcement).
- **SHALL NOT** replace existing middleware implementations.
- **SHALL NOT** change middleware pipeline ordering for existing middleware.

## Validation and Error Handling

- **SHALL** preserve ProblemDetails shape: `instance` field, `reference-id` extension, `errors` map.
- **SHALL** preserve `lower_underscore` error keys in validation error responses.
- **SHALL** produce 422 for validation failures with errors map.
- **SHALL** produce 401 for unauthenticated requests.
- **SHALL** produce 403 for unauthorized requests.
- **SHALL** produce 404 for not-found resources.

## Patching

- **SHALL** preserve `Delta<T>` patch behavior (partial update semantics, unchanged fields not modified).
- **SHALL NOT** introduce JSON Patch in this change.

## Event Ingestion

- **SHALL** preserve raw event ingestion behavior (multipart, compressed, raw body).
- **SHALL** preserve event submission via API key authentication.
- **SHALL** preserve batch event submission.

## Mediator Pattern

- **SHALL NOT** use generated mediator endpoints (MapMediatorEndpoints) for existing public API routes.
- **SHALL** use Foundatio.Mediator for command/query dispatch from endpoint lambdas.
- **SHALL** register all handlers via DI auto-discovery.

## OpenAPI

- **SHALL** preserve `/docs/v2/openapi.json` serving Scalar docs.
- **SHALL** generate build-time OpenAPI artifact during `dotnet build`.
- **SHALL** add route manifest snapshot tests that fail on route addition/removal/change.
- **SHALL** add OpenAPI snapshot tests that fail on schema drift.

## Architecture

- **SHALL** place all new endpoint code under `src/Exceptionless.Web/Api/`.
- **SHALL** keep v1 legacy aliases in the same endpoint file as the canonical v2 route.
- **SHALL** remove `AddControllers()` and `MapControllers()` after all controllers are migrated.
- **SHALL** delete `Controllers/` folder after all controllers are migrated.

## Testing

- **SHALL** pass all existing integration tests without modification (unless test infrastructure needs updating for host changes).
- **SHALL** update `tests/http/*.http` files if endpoint paths or parameters change (they should not).
