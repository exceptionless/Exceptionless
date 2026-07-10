---
name: backend-architecture
description: >
  Use this skill when working on the ASP.NET Core backend — adding controllers, services,
  repositories, validators, authorization, WebSocket endpoints, jobs, Foundatio infrastructure,
  configuration, or Aspire orchestration. Prefer this as the backend entrypoint for project
  layering, C# conventions, logging, ProblemDetails, security-sensitive config, and OpenAPI
  baseline updates.
---

# Backend Architecture

## Quick Start

Run `Exceptionless.AppHost` from your IDE, or start everything from the repo root:

```bash
aspire run
```

## Project Layering

```text
Exceptionless.Core        → Domain logic, services, repositories, validation
Exceptionless.Insulation  → Infrastructure implementations (Redis, GeoIP, Mail, HealthChecks)
Exceptionless.Web         → ASP.NET Core host, controllers, WebSocket hubs
Exceptionless.Job         → Background job workers
```

**Dependency Direction:** `Web → Core ← Insulation` / `Job → Core ← Insulation`

## Exceptionless.Core

### Services (`src/Exceptionless.Core/Services/`)

`UsageService`, `EventPostService`, `StackService`, `OrganizationService`, `MessageService`, `SlackService`

### Repositories

Repositories derive from the local repository base classes over `ElasticRepositoryBase<T>` and use `MiniValidationValidator` plus `AppOptions`. They use Foundatio Parsers for query parsing. See `foundatio-repositories` for query, pagination, patch, and aggregation patterns.

### Validation

Use MiniValidator with DataAnnotations on API and domain models:

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

## C# Project Conventions

- Follow `.editorconfig`, use file-scoped namespaces, and keep diffs minimal.
- Always use braces for control flow and never add `#region` / `#endregion`.
- Async methods use the `Async` suffix and pass `CancellationToken` through call chains when available.
- Prefer constructor injection with `readonly` fields.
- Use `ValueTask<T>` only for hot paths that often complete synchronously.
- `ConfigureAwait(false)` is not required in ASP.NET Core code.

## Logging

Use structured message templates with named placeholders. Do not use string interpolation in log messages.

```csharp
_logger.LogInformation("Saving org ({OrganizationId}-{OrganizationName}) event usage",
    organizationId, organization.Name);
```

For cross-cutting context, use `ExceptionlessState` scopes:

```csharp
using var _ = _logger.BeginScope(new ExceptionlessState()
    .Organization(organizationId)
    .Project(projectId));
```

Never log passwords, API keys, full tokens, or sensitive user data. Log identifiers and safe prefixes only.

## Foundatio Infrastructure

Use Foundatio abstractions rather than provider-specific clients:

| Need | Use |
| ---- | --- |
| Distributed cache | `ICacheClient` |
| Queues | `IQueue<T>` |
| Pub/sub | `IMessageBus` |
| File storage | `IFileStorage` |
| Distributed locks | `ILockProvider` |
| Retry/circuit breaker | `IResiliencePolicyProvider` |

Queue jobs usually derive from `QueueJobBase<T>`. Scheduled jobs generally derive Foundatio job base classes such as `JobWithLockBase` and use `[Job]` attributes for `InitialDelay`, `Interval`, and related scheduling options. Queue entries should be completed only after durable processing succeeds; abandon transient failures and do not retry validation failures.

Use `foundatio-repositories` for Elasticsearch repository querying, patching, aggregations, and pagination rules.

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

// Usage
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class OrganizationController : RepositoryApiController<...> { }

[Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
public class AdminController : ExceptionlessApiController { }
```

## Controller Patterns

Most controllers extend `RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel>`. Auth/special-case controllers extend `ExceptionlessApiController` directly.

```csharp
[Route(API_PREFIX + "/organizations")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class OrganizationController : RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization>
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ViewOrganization>>> GetAllAsync(string? mode = null)
    {
        var organizations = await GetModelsAsync(GetAssociatedOrganizationIds().ToArray());
        return Ok(await MapCollectionAsync<ViewOrganization>(organizations, true));
    }
}
```

## ProblemDetails and Error Handling

Return helpers from `ExceptionlessApiController`: `Ok()`, `Created()`, `NoContent()`, `Unauthorized()`, `Forbidden()`, `NotFound()`, `ValidationProblem(ModelState)`.

Exceptions auto-convert via `ExceptionToProblemDetailsHandler`: `MiniValidatorException`/`ValidationException` → 422, others → 500.

## OpenAPI Baseline

After any API change (new endpoint, changed status codes, modified request/response models), **always regenerate the OpenAPI baseline**:

```powershell
# Requires the API to be running (`aspire run` or the AppHost)
Invoke-WebRequest -Uri "https://api-ex.dev.localhost:7111/docs/v2/openapi.json" -OutFile "tests/Exceptionless.Tests/Controllers/Data/openapi.json"
```

Then include the updated `openapi.json` in the same commit as the API change (or amend). The `OpenApiControllerTests.GetOpenApiJson_Default_ReturnsExpectedBaseline` test will fail if the baseline is stale.
If local TLS tooling fails, use the Aspire-described HTTP endpoint: `http://api-ex.dev.localhost:7110/docs/v2/openapi.json`.

## WebSocket Hubs (NOT SignalR)

Custom WebSocket implementation using Foundatio `IMessageBus`. `MessageBusBroker` subscribes to `EntityChanged`, `PlanChanged`, `UserMembershipChanged` and broadcasts to connected WebSocket clients via `WebSocketConnectionManager`.

## Configuration

Uses YAML files (`appsettings.yml`) + `AddCustomEnvironmentVariables()`. All config binds to `AppOptions` with nested options (`EmailOptions`, `AuthOptions`, `IntercomOptions`, `SlackOptions`, `StripeOptions`). Inject `AppOptions` directly — not `IOptions<T>`.

Secrets come from environment variables or deployment secrets, never committed config. Non-secret configuration belongs in `appsettings.yml`; environment overrides use the `EX_` prefix.
