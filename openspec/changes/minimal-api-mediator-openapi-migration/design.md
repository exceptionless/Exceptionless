# Design: Minimal API + Mediator + OpenAPI Migration

## Architecture Overview

```
HTTP Request
  → ASP.NET Minimal API endpoint (route + auth + filters)
    → Foundatio.Mediator dispatch (message → handler)
      → Handler (reuses Core repositories/services)
    → IResult (typed result with headers/status)
  → Response
```

## File Layout

All new code lives under `src/Exceptionless.Web/Api/`:

```
Api/
  ApiEndpoints.cs              # Extension method: app.MapApiEndpoints()
  ApiEndpointGroups.cs         # Shared group configuration (prefix, auth, filters)
  Endpoints/                   # One file per feature area
  Messages/                    # Request/response message records
  Handlers/                    # Mediator handlers (one per feature area)
  Middleware/                  # ValidationMiddleware, LoggingMiddleware
  Filters/                     # Endpoint filters (ConfigurationResponse, ApiResponseHeaders)
  Results/                     # Custom IResult types and mapping helpers
  Infrastructure/              # Shared utilities (pagination, time range, etc.)
  OpenApi/                     # OpenAPI customization and conventions
```

## Endpoint Registration Pattern

### ApiEndpoints.cs

Single extension method called from `Program.cs`:

```csharp
public static class ApiEndpoints
{
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapStatusEndpoints();
        app.MapUtilityEndpoints();
        app.MapTokenEndpoints();
        // ... all feature endpoint groups
        return app;
    }
}
```

### ApiEndpointGroups.cs

Shared group builder configuration:

```csharp
public static class ApiEndpointGroups
{
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder routes, string prefix)
    {
        return routes.MapGroup($"api/v2/{prefix}")
            .RequireAuthorization(AuthorizationRoles.UserPolicy)
            .AddEndpointFilter<ApiResponseHeadersEndpointFilter>()
            .WithOpenApi();
    }
}
```

### Individual Endpoint Files

Each `*Endpoints.cs` file:
1. Creates a route group with appropriate prefix and auth.
2. Maps all routes for that feature (GET, POST, PUT, PATCH, DELETE).
3. Includes v1 legacy aliases in the same file as the canonical v2 route.
4. Delegates to Foundatio.Mediator for business logic dispatch.

```csharp
public static class StatusEndpoints
{
    public static WebApplication MapStatusEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup("");

        group.MapGet("about", async (IMediator mediator) =>
        {
            var result = await mediator.SendAsync(new GetAboutQuery());
            return Results.Ok(result);
        }).AllowAnonymous();

        // ... other status routes
        return app;
    }
}
```

## Mediator Dispatch Pattern

### Messages

Records in `Messages/*.cs` representing commands and queries:

```csharp
// Messages/StatusMessages.cs
public record GetAboutQuery : ICommand<AboutResponse>;
public record GetQueueStatsQuery : ICommand<QueueStatsResponse>;
public record PostReleaseNotificationCommand(string Message, bool Critical) : ICommand<ReleaseNotification>;
```

### Handlers

Classes in `Handlers/*.cs` that implement `ICommandHandler<TMessage, TResponse>`:

```csharp
// Handlers/StatusHandler.cs
public class StatusHandler :
    ICommandHandler<GetAboutQuery, AboutResponse>,
    ICommandHandler<GetQueueStatsQuery, QueueStatsResponse>
{
    // Inject existing Core services/repositories
    private readonly AppOptions _appOptions;
    private readonly IQueue<EventPost> _eventQueue;
    // ...
}
```

### Handler Reuse of Existing Logic

Handlers do NOT duplicate repository/service logic. They:
1. Accept the message.
2. Call existing `Core` repositories (`IEventRepository`, `IStackRepository`, etc.) and services.
3. Map results to response DTOs or return domain models directly.
4. Return the result (handler does not create HTTP responses).

The endpoint lambda is responsible for mapping handler results to HTTP semantics (status codes, headers, pagination links).

## Validation Strategy

### Automatic Validation (DataAnnotation)

The endpoint validation filter validates request DTOs with DataAnnotation metadata and returns 422 for automatic semantic validation failures. Endpoints preserve any legacy 400 response contract for missing implicitly required request fields at the endpoint boundary.

### MiniValidation (Complex Cases)

For validation that cannot be expressed with DataAnnotations (cross-field, conditional, post-patch):

```csharp
var (isValid, errors) = MiniValidator.TryValidate(model);
if (!isValid)
    return Results.ValidationProblem(errors);
```

Used for:
- JSON Patch validation (validate operations and the merged model after applying the patch to a copy).
- Complex cross-field rules.
- Conditional validation based on AppOptions/feature flags.

### Patch Compatibility

- RFC 6902 JSON Patch is the canonical format advertised by OpenAPI and used by first-party clients.
- Legacy `application/json` partial object bodies are converted to equivalent replace operations at the endpoint boundary.
- Partial update semantics are preserved: only specified fields are modified and explicit null values clear nullable fields.
- Operation and model validation run before the stored entity is modified.

## ProblemDetails Centralization

Configure `AddProblemDetails()` with a customizer that ensures:

- `instance` field set to request path.
- `extensions["reference-id"]` set to trace ID.
- `errors` map uses `lower_underscore` keys.
- Legacy missing required-field responses remain 400 with an errors map; automatic semantic validation errors produce 422 with an errors map.
- Not-found produces 404.
- Auth failures produce 401/403.

This is configured once in DI and applies to all endpoints.

## OpenAPI Generation

### Runtime

- `Microsoft.AspNetCore.OpenApi` generates `/docs/v2/openapi.json` at runtime.
- Scalar UI served at `/docs` (or existing Scalar path).
- Operation IDs derived from endpoint metadata.

### Build-Time

- `Microsoft.Extensions.ApiDescription.Server` generates `openapi.json` during `dotnet build`.
- Artifact committed or CI-compared for drift detection.
- Snapshot test compares build-time artifact against known-good baseline.

### Route Manifest Tests

- Test enumerates all registered endpoints at startup.
- Compares `{method} {path}` list against a checked-in manifest file.
- Any route addition/removal/change fails the test until manifest is updated.

## Migration Strategy

### Incremental, Controller-by-Controller

1. **Baseline**: Capture OpenAPI snapshot and route manifest from existing controllers.
2. **Infrastructure**: Build Api/ folder, registration, filters, results, infrastructure utilities.
3. **Per-controller migration** (ordered by complexity, simplest first):
   - Create endpoint file + messages + handler.
   - Wire up in ApiEndpoints.cs.
   - Run integration tests against new endpoints.
   - Verify OpenAPI snapshot unchanged.
   - Remove old controller.
4. **Final cleanup**: Remove `AddControllers()` / `MapControllers()`, delete `Controllers/` folder.

### Coexistence During Migration

During migration, both controllers and new endpoints exist. Route conflicts are avoided by:
- Only activating the new endpoint after the controller is deleted.
- OR: Using a feature flag / conditional registration (prefer delete-and-replace approach).

## Rollback Approach

- Each controller migration is a separate PR.
- Reverting a PR restores the controller and removes the Minimal API endpoint.
- OpenAPI snapshot and route manifest tests confirm the revert is clean.
- No database/index migrations are involved; rollback is purely code.

## Security Considerations

- Auth policies are applied identically via `.RequireAuthorization()`.
- `ApiKeyAuthenticationHandler` remains unchanged in the pipeline.
- Endpoint filters replicate any per-action auth checks from controller action methods.
- No new attack surface introduced.

## Performance Considerations

- Minimal APIs have slightly lower overhead than MVC controllers (no model binding pipeline, no action filters reflection).
- Mediator dispatch adds negligible overhead (in-process, no serialization).
- No performance regression expected.
