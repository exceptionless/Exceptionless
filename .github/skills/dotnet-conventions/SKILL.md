---
name: .NET Conventions
description: |
  C# coding standards for the Exceptionless codebase. Naming conventions, async patterns,
  structured logging, nullable reference types, and formatting rules.
  Keywords: C# style, naming conventions, _camelCase, PascalCase, async suffix,
  CancellationToken, nullable annotations, structured logging, ExceptionlessState
---

# .NET Conventions

## Style & Formatting

- Follow `.editorconfig` rules strictly
- Run `dotnet format` before committing
- **Minimize diffs**: Change only what's necessary, preserve existing formatting and structure
- Match surrounding code style exactly

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Private fields | `_camelCase` | `_organizationRepository` |
| Public members | PascalCase | `GetByIdAsync` |
| Local variables | camelCase | `organizationId` |
| Constants | PascalCase | `MaxRetryCount` |
| Type parameters | `T` prefix | `TModel` |

## Formatting Rules

- **Indentation**: 4 spaces, no tabs
- **Namespaces**: File-scoped (`namespace Foo;`)
- **Usings**: Outside namespace
- **Braces**: Always use, even for single-line blocks
- **No `#region`**: Never use `#region`/`#endregion` directives — they hide code and discourage refactoring

## Async Patterns

- **Suffix**: Always use `Async` suffix for async methods
- **CancellationToken**: Pass through call chains when available
- **ValueTask**: Prefer `ValueTask<T>` for hot paths that often complete synchronously
- **ConfigureAwait**: Not required in ASP.NET Core

```csharp
// From src/Exceptionless.Core/Services/UsageService.cs
public async Task SavePendingUsageAsync()
{
    var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
    await SavePendingOrganizationUsageAsync(utcNow);
    await SavePendingProjectUsageAsync(utcNow);
}
```

## Structured Logging

Use message templates with named placeholders — values go in the args, not string interpolation:

```csharp
// ✅ Correct: Named placeholders for structured data
_logger.LogInformation("Saving org ({OrganizationId}-{OrganizationName}) event usage",
    organizationId, organization.Name);

_logger.LogError(ex, "Error retrieving event post payload: {Path}", path);

_logger.LogWarning("Unable to parse user agent {UserAgent}. Exception: {Message}",
    userAgent, ex.Message);

// ❌ Wrong: String interpolation loses structure
_logger.LogInformation($"Saving org {organizationId}");
```

### Log Scopes with ExceptionlessState

Use scopes to add context to all log entries within a block:

```csharp
// From src/Exceptionless.Core/Jobs/EventPostsJob.cs
using var _ = _logger.BeginScope(new ExceptionlessState()
    .Organization(ep.OrganizationId)
    .Project(ep.ProjectId));

// All log entries in this scope automatically include org/project context
_logger.LogInformation("Processing event post");
```

Add tags and properties for richer context:

```csharp
using (_logger.BeginScope(new ExceptionlessState()
    .Organization(organization.Id)
    .Tag("Delete")
    .Tag("Bot")))
{
    _logger.LogInformation("Removing bot events");
}
```

## Nullable Reference Types

- Honor nullable annotations throughout
- Treat nullable warnings as errors
- Use `?` suffix for nullable types

```csharp
public async Task<User?> FindUserAsync(string? email)
{
    if (string.IsNullOrWhiteSpace(email))
        return null;

    return await _repository.FindByEmailAsync(email);
}
```

## Resource Disposal

```csharp
// Prefer using declarations
using var stream = File.OpenRead(path);

// Async disposal
await using var connection = await CreateConnectionAsync();
```

## Constructor Injection

Prefer constructor injection with readonly fields:

```csharp
// From src/Exceptionless.Core/Services/UsageService.cs
public class UsageService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ICacheClient _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public UsageService(
        IOrganizationRepository organizationRepository,
        ICacheClient cache,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        _organizationRepository = organizationRepository;
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<UsageService>();
    }
}
```

## Validation Patterns

### Input Validation

Validate early, fail fast:

```csharp
public async Task<ActionResult> ProcessAsync(string id)
{
    if (string.IsNullOrEmpty(id))
        return BadRequest("Id is required");

    var entity = await _repository.GetByIdAsync(id);
    if (entity is null)
        return NotFound();

    // Continue processing
}
```

### Domain Validation

See [backend-architecture](backend-architecture/SKILL.md) for validation patterns (FluentValidation for domain models, MiniValidator for API requests).
