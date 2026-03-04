---
name: Backend Architecture
description: |
  Backend architecture for Exceptionless. Project layering, repositories, validation,
  controllers, authorization, WebSockets, configuration, and Aspire orchestration.
  Keywords: Core, Insulation, repositories, FluentValidation, MiniValidator, controllers,
  AuthorizationRoles, ProblemDetails, Aspire, WebSockets, AppOptions
---

# Backend Architecture

## Quick Start

Run `Exceptionless.AppHost` from your IDE. Aspire automatically starts all services (Elasticsearch, Redis) with proper ordering. The dashboard opens at the assigned localhost port.

```bash
dotnet run --project src/Exceptionless.AppHost
```

Use the Aspire MCP for listing resources, viewing logs, and executing commands.

## Project Layering

```text
Exceptionless.Core        → Domain logic, services, repositories, validation
Exceptionless.Insulation  → Infrastructure implementations (Redis, GeoIP, Mail, HealthChecks)
Exceptionless.Web         → ASP.NET Core host, controllers, WebSocket hubs
Exceptionless.Job         → Background job workers
```

### Dependency Direction

```text
Web → Core ← Insulation
Job → Core ← Insulation
```

## Exceptionless.Core

Contains all domain logic, services, and repositories.

### Services

Real services in the codebase (see `src/Exceptionless.Core/Services/`):

- `UsageService` — Tracks event usage per organization/project
- `EventPostService` — Handles event post storage and retrieval
- `StackService` — Stack management and status updates
- `OrganizationService` — Organization lifecycle management
- `MessageService` — WebSocket message coordination
- `SlackService` — Slack integration

### Repositories

Repositories extend `Foundatio.Repositories.Elasticsearch` and use validation:

```csharp
// From src/Exceptionless.Core/Repositories/Base/RepositoryBase.cs
public abstract class RepositoryBase<T> : ElasticRepositoryBase<T> where T : class, IIdentity, new()
{
    protected readonly IValidator<T>? _validator;
    protected readonly AppOptions _options;

    public RepositoryBase(IIndex index, IValidator<T>? validator, AppOptions options) : base(index)
    {
        _validator = validator;
        _options = options;
        NotificationsEnabled = options.EnableRepositoryNotifications;
    }

    protected override Task ValidateAndThrowAsync(T document)
    {
        if (_validator is null)
            return Task.CompletedTask;
        return _validator.ValidateAndThrowAsync(document);
    }
}
```

Repositories use Foundatio Parsers for query parsing against Elasticsearch.

### Validation

**Two validation patterns** are used (transitioning to MiniValidator for new code):

#### FluentValidation for Domain Models

Used by repositories (see `src/Exceptionless.Core/Validation/`):

```csharp
// From src/Exceptionless.Core/Validation/OrganizationValidator.cs
public class OrganizationValidator : AbstractValidator<Organization>
{
    public OrganizationValidator(BillingPlans plans)
    {
        RuleFor(o => o.Name).NotEmpty().WithMessage("Please specify a valid name.");
        RuleFor(o => o.PlanId).NotEmpty().WithMessage("Please specify a valid plan id.");
        RuleFor(o => o.SuspensionCode).NotEmpty().When(o => o.IsSuspended);
    }
}
```

#### MiniValidator for API Request Models

Uses DataAnnotations with `MiniValidator` (preferred for new code — repositories are migrating to this):

```csharp
// From src/Exceptionless.Web/Models/Login.cs
public record Login
{
    [Required]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 6)]
    public required string Password { get; init; }
}
```

MiniValidator integration (see `src/Exceptionless.Core/Validation/MiniValidationValidator.cs`):

```csharp
public class MiniValidationValidator(IServiceProvider serviceProvider)
{
    public async Task ValidateAndThrowAsync<T>(T instance)
    {
        (bool isValid, var errors) = await MiniValidator.TryValidateAsync(instance, serviceProvider, recurse: true);
        if (!isValid)
            throw new MiniValidatorException("Please correct the specified errors and try again", errors);
    }
}

public class MiniValidatorException(string message, IDictionary<string, string[]> errors) : Exception(message)
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
```

Auto-validation via `AutoValidationActionFilter` handles API model validation automatically.

## Exceptionless.Insulation

Infrastructure implementations only — NOT services or repositories:

- `Configuration/` — YAML configuration extensions
- `Geo/` — MaxMind GeoIP service
- `HealthChecks/` — Elasticsearch, Cache, Queue, Storage health checks
- `Mail/` — MailKit mail sender
- `Redis/` — Redis connection mapping

## Authorization with Policy Constants

Use `AuthorizationRoles` constants (NOT string literals):

```csharp
// From src/Exceptionless.Core/Authorization/AuthorizationRoles.cs
public static class AuthorizationRoles
{
    public const string ClientPolicy = nameof(ClientPolicy);
    public const string Client = "client";
    public const string UserPolicy = nameof(UserPolicy);
    public const string User = "user";
    public const string GlobalAdminPolicy = nameof(GlobalAdminPolicy);
    public const string GlobalAdmin = "global";
}
```

Apply to controllers:

```csharp
// From src/Exceptionless.Web/Controllers/AuthController.cs
[Route(API_PREFIX + "/auth")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class AuthController : ExceptionlessApiController
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResult>> LoginAsync(Login model) { }
}

// From src/Exceptionless.Web/Controllers/AdminController.cs
[Authorize(Policy = AuthorizationRoles.GlobalAdminPolicy)]
public class AdminController : ExceptionlessApiController { }
```

## Controller Patterns

### CRUD via RepositoryApiController

Most controllers extend `RepositoryApiController<TRepository, TModel, TViewModel, TNewModel, TUpdateModel>`:

```csharp
// From src/Exceptionless.Web/Controllers/OrganizationController.cs
[Route(API_PREFIX + "/organizations")]
[Authorize(Policy = AuthorizationRoles.UserPolicy)]
public class OrganizationController : RepositoryApiController<IOrganizationRepository, Organization, ViewOrganization, NewOrganization, NewOrganization>
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ViewOrganization>>> GetAllAsync(string? mode = null)
    {
        var organizations = await GetModelsAsync(GetAssociatedOrganizationIds().ToArray());
        var viewOrganizations = await MapCollectionAsync<ViewOrganization>(organizations, true);
        return Ok(viewOrganizations);
    }
}
```

### Thin Controllers for Auth/Special Cases

```csharp
// From src/Exceptionless.Web/Controllers/AuthController.cs
public class AuthController : ExceptionlessApiController
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<TokenResult>> LoginAsync(Login model)
    {
        string email = model.Email.Trim().ToLowerInvariant();
        using var _ = _logger.BeginScope(new ExceptionlessState()
            .Tag("Login")
            .Identity(email)
            .SetHttpContext(HttpContext));

        var user = await _userRepository.GetByEmailAddressAsync(email);
        if (user is null || !user.IsActive)
            return Unauthorized();

        return Ok(new TokenResult { Token = await GetOrCreateAuthenticationTokenAsync(user) });
    }
}
```

## ProblemDetails and Error Handling

### Return Helpers

```csharp
// Success responses
return Ok(data);
return Created(uri, await MapAsync<TViewModel>(model, true));
return NoContent();

// Error responses from ExceptionlessApiController
return Unauthorized();                         // 401
return Forbidden();                            // 403 - custom helper
return NotFound();                             // 404
return ValidationProblem(ModelState);          // 422 with validation errors
```

### Exception to ProblemDetails Mapping

Exceptions are automatically converted via `ExceptionToProblemDetailsHandler`:

```csharp
// From src/Exceptionless.Web/Startup.cs
MiniValidatorException => StatusCodes.Status422UnprocessableEntity,
ValidationException => StatusCodes.Status422UnprocessableEntity,
// Other exceptions map to 500
```

## WebSocket Hubs (NOT SignalR)

Uses custom WebSocket implementation with Foundatio message bus:

```csharp
// From src/Exceptionless.Web/Hubs/MessageBusBroker.cs
public sealed class MessageBusBroker : IStartupAction
{
    private readonly WebSocketConnectionManager _connectionManager;
    private readonly IMessageSubscriber _subscriber;

    public async Task RunAsync(CancellationToken shutdownToken = default)
    {
        await Task.WhenAll(
            _subscriber.SubscribeAsync<EntityChanged>(OnEntityChangedAsync, shutdownToken),
            _subscriber.SubscribeAsync<PlanChanged>(OnPlanChangedAsync, shutdownToken),
            _subscriber.SubscribeAsync<UserMembershipChanged>(OnUserMembershipChangedAsync, shutdownToken)
        );
    }
}
```

Key files:

- `Hubs/MessageBusBroker.cs` — Subscribes to message bus, broadcasts to WebSocket clients
- `Hubs/WebSocketConnectionManager.cs` — Manages WebSocket connections

## Configuration Pattern

Uses YAML files with custom environment variable binding:

```csharp
// From src/Exceptionless.Web/Program.cs
var config = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddYamlFile("appsettings.yml", optional: true, reloadOnChange: true)
    .AddYamlFile($"appsettings.{environment}.yml", optional: true, reloadOnChange: true)
    .AddCustomEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
```

### AppOptions

All configuration binds to `AppOptions` class with nested options:

- `AppOptions.EmailOptions`
- `AppOptions.AuthOptions`
- `AppOptions.IntercomOptions`
- `AppOptions.SlackOptions`
- `AppOptions.StripeOptions`

Access via direct injection (not `IOptions<T>`):

```csharp
public class UsageService
{
    public UsageService(AppOptions options, ILoggerFactory loggerFactory)
    {
        _options = options;
    }
}
```

## Service Discovery

Services reference each other by name in Aspire:

```csharp
// AppHost topology
var elasticsearch = builder.AddElasticsearch("elasticsearch");
var api = builder.AddProject<Projects.Exceptionless_Web>("api")
    .WithReference(elasticsearch);

// In service, get connection by resource name
var esConnection = builder.Configuration.GetConnectionString("elasticsearch");
```

## Dependencies

- NuGet feeds configured in [NuGet.Config](NuGet.Config)
- Version alignment in `src/Directory.Build.props`
- Avoid deprecated APIs — check for alternatives before using legacy methods

## Route Patterns

```csharp
[Route(API_PREFIX + "/organizations")]           // Collection
[HttpGet("{id}")]                                // Single resource
[Route("~/" + API_PREFIX + "/admin/organizations")] // Admin override
```
