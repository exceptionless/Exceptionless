---
name: backend-architecture
description: >
  Use this skill when working on the ASP.NET Core backend — adding controllers, repositories,
  validators, authorization, WebSocket endpoints, or Aspire orchestration. Apply when modifying
  project layering (Core, Insulation, Web, Job), configuring services, returning ProblemDetails
  errors, or understanding how the backend is structured.
---

# Backend Architecture

## Quick Start

Run `Exceptionless.AppHost` from your IDE, or start everything from the repo root:

```bash
aspire run --project src/Exceptionless.AppHost
```

## Project Layering

```text
Exceptionless.Core        → Domain logic, services, repositories, validation
Exceptionless.Insulation  → Infrastructure implementations (Redis, GeoIP, Mail, HealthChecks)
Exceptionless.Web         → ASP.NET Core host, Minimal API endpoints, Mediator handlers, WebSocket hubs
Exceptionless.Job         → Background job workers
```

**Dependency Direction:** `Web → Core ← Insulation` / `Job → Core ← Insulation`

## Exceptionless.Core

### Services (`src/Exceptionless.Core/Services/`)

`UsageService`, `EventPostService`, `StackService`, `OrganizationService`, `MessageService`, `SlackService`

### Repositories

Repositories extend `ElasticRepositoryBase<T>` with optional `IValidator<T>` and `AppOptions` injection. They use Foundatio Parsers for query parsing. See `foundatio-repositories` skill for query/pagination/patch patterns.

### Validation

**Two patterns** (transitioning to MiniValidator for new code):

**FluentValidation** — domain models in repositories (`src/Exceptionless.Core/Validation/`):

```csharp
public class OrganizationValidator : AbstractValidator<Organization>
{
    public OrganizationValidator(BillingPlans plans)
    {
        RuleFor(o => o.Name).NotEmpty().WithMessage("Please specify a valid name.");
        RuleFor(o => o.PlanId).NotEmpty().WithMessage("Please specify a valid plan id.");
    }
}
```

**MiniValidator** — API request models with DataAnnotations (preferred for new code):

```csharp
public record Login
{
    [Required]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }
}
```

`AutoValidationActionFilter` handles API model validation automatically. `MiniValidationValidator` wraps `MiniValidator.TryValidateAsync` and throws `MiniValidatorException` on failure.

## Exceptionless.Insulation

Infrastructure only — `Configuration/` (YAML), `Geo/` (MaxMind), `HealthChecks/`, `Mail/` (MailKit), `Redis/`.

## Authorization

Use `AuthorizationRoles` constants (NOT string literals):

```csharp
public static class AuthorizationRoles
{
    public const string ClientPolicy = nameof(ClientPolicy);
    public const string Client = "client";
    public const string UserPolicy = nameof(UserPolicy);
    public const string User = "user";
    public const string GlobalAdminPolicy = nameof(GlobalAdminPolicy);
    public const string GlobalAdmin = "global";
}

// Minimal API endpoint groups (NEW pattern)
var group = endpoints.MapGroup("api/v2")
    .RequireAuthorization(AuthorizationRoles.UserPolicy)  // Group default
    .WithTags("Tokens");

// Override on specific endpoints
group.MapGet("tokens/me", ...).RequireAuthorization(AuthorizationRoles.ClientPolicy);
group.MapPost("auth/login", ...).AllowAnonymous();
```

## Controller Patterns

> **DEPRECATED**: Controllers are being migrated to Minimal API endpoints + Mediator handlers (see below).
> Do NOT add new controllers. Use the Endpoint + Handler pattern instead.

Legacy controllers extend `RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel>`. Auth/special-case controllers extend `ExceptionlessApiController` directly.

## Minimal API + Mediator Architecture (NEW)

All new API work uses Minimal API endpoints with Foundatio.Mediator for command/query dispatch.

### Structure

```text
src/Exceptionless.Web/Api/
├── Endpoints/          ← Thin HTTP adapters (routing, auth, response mapping)
├── Messages/           ← Command/query records (mediator messages)
├── Handlers/           ← Use-case logic (transport-agnostic, return Result<T>)
├── Middleware/         ← Mediator pipeline middleware (validation, logging)
├── Filters/            ← Endpoint filters (HTTP-specific cross-cutting)
├── Results/            ← Result→IResult mapping, pagination, response types
├── Infrastructure/     ← Shared utilities (validation, pagination, links)
└── OpenApi/            ← OpenAPI conventions and transformers
```

### Endpoint Pattern

```csharp
public static class TokenEndpoints
{
    public static IEndpointRouteBuilder MapTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("api/v2")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .WithTags("Tokens");

        group.MapGet("tokens/{id}", async (string id, IMediator mediator)
            => (await mediator.InvokeAsync<Result<ViewToken>>(new GetTokenById(id))).ToHttpResult())
            .WithName("GetTokenById")
            .Produces<ViewToken>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
```

### Handler Pattern (CRITICAL: Transport-Agnostic)

Handlers MUST return `Result<T>` or `Result` — NEVER `IResult` or HTTP types.

```csharp
public class TokenHandler(ITokenRepository repository, ...) : HandlerBase
{
    public async Task<Result<ViewToken>> Handle(GetTokenById message)
    {
        var model = await repository.GetByIdAsync(message.Id);
        if (model is null)
            return Result.NotFound("Token not found.");
        return mapper.MapToViewToken(model);
    }
}
```

### Result→HTTP Status Code Mapping

| Result Type | HTTP Status | Notes |
|---|---|---|
| `Result<T>` success | 200 OK | Default success |
| `Result<T>.Created(val, loc)` | 201 Created | With Location header |
| `Result.NotFound(msg)` | 404 | Message in `title` field |
| `Result.Forbidden(msg)` | 403 | Message in `title` field |
| `Result.BadRequest(msg)` | 400 | Message in `title` field |
| `Result.Invalid(ValidationError)` | 422 | Errors in `errors` dict |
| `Result.Invalid("plan_limit", msg)` | 426 | Upgrade Required |
| `Result.Invalid("not_implemented", msg)` | 501 | Not Implemented |
| `Result.Invalid("rate_limit", msg)` | 429 | Too Many Requests |
| `WorkInProgressResult` | 202 Accepted | Bulk operations |
| `ModelActionResults` (has failures) | 400 | Per-ID failure details |
| `PagedResult<T>` | 200 + Link headers | Auto-pagination |
| `NotModifiedResponse` | 304 | No body |

### Key Rules

- Handlers MUST NOT import `Microsoft.AspNetCore.Http`
- Handlers CAN accept `HttpContext` as a method parameter (auto-resolved by mediator) for auth
- Pagination link URLs MUST be built in the endpoint/mapper layer
- `ProblemDetails` shape MUST be preserved: `instance`, `reference-id`, `errors`, `lower_underscore` keys
- Messages go in `title` field (NOT `detail`) — matches original controller behavior
- Use `ApiValidation.ValidateAsync(model, serviceProvider)` at endpoint level (returns 422 by default)
- Keep v1 legacy route aliases in the same endpoint file as canonical v2 routes

## ProblemDetails and Error Handling

### Endpoint-level validation (Minimal API)

Use `ApiValidation.ValidateAsync(model, serviceProvider)` — returns `ValidationProblemDetails` at 422 for DataAnnotation failures. For MVC-model-binding-compatible 400 responses, pass explicit status code or add endpoint-level checks.

### Handler-level errors

Return `Result.NotFound()`, `Result.Forbidden()`, `Result.BadRequest()`, or `Result.Invalid(ValidationError)`. The `ResultExtensions.ToHttpResult()` method converts these to proper `IResult` with ProblemDetails shape.

### Exception handling

Exceptions auto-convert via `ExceptionToProblemDetailsHandler`: `MiniValidatorException`/`ValidationException` → 422, `UnauthorizedAccessException` → 401, `VersionConflictDocumentException` → 409, others → 500.

## OpenAPI Baseline

After any API change (new endpoint, changed status codes, modified request/response models), **always regenerate the OpenAPI baseline**:

```bash
# Requires the API to be running (aspire run --project src/Exceptionless.AppHost)
curl -s http://localhost:7110/docs/v2/openapi.json | jq . > tests/Exceptionless.Tests/Controllers/Data/openapi.json
```

Then include the updated `openapi.json` in the same commit as the API change. The `OpenApiSnapshotTests.GetOpenApiJson_Default_MatchesSnapshot` test will fail if the baseline is stale.

The endpoint manifest test (`EndpointManifestTests`) verifies all registered routes haven't changed — update `tests/Exceptionless.Tests/Controllers/Data/endpoint-manifest.txt` when adding/removing routes.

## WebSocket Hubs (NOT SignalR)

Custom WebSocket implementation using Foundatio `IMessageBus`. `MessageBusBroker` subscribes to `EntityChanged`, `PlanChanged`, `UserMembershipChanged` and broadcasts to connected WebSocket clients via `WebSocketConnectionManager`.

## Configuration

Uses YAML files (`appsettings.yml`) + `AddCustomEnvironmentVariables()`. All config binds to `AppOptions` with nested options (`EmailOptions`, `AuthOptions`, `IntercomOptions`, `SlackOptions`, `StripeOptions`). Inject `AppOptions` directly — not `IOptions<T>`.
