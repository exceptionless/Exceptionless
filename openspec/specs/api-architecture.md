# Spec: API Architecture (Minimal API + Mediator)

## Overview

Defines the architecture for Exceptionless's Minimal API endpoint layer using Foundatio.Mediator for command/query dispatch.

## Requirements

### Endpoint Registration

- **ADDED**: The system SHALL register all API endpoints via a single `app.MapApiEndpoints()` extension method in `src/Exceptionless.Web/Api/ApiEndpoints.cs`.
- **ADDED**: Each feature area SHALL have its own endpoint registration method (e.g., `MapStatusEndpoints()`, `MapEventEndpoints()`).
- **ADDED**: Endpoint groups SHALL apply shared configuration (route prefix, auth policy, filters) via `ApiEndpointGroups.cs`.
- **ADDED**: All API endpoints SHALL be routed under the `api/v2/` prefix.
- **ADDED**: V1 legacy aliases SHALL be defined in the same endpoint file as their canonical v2 route.

### Mediator Dispatch

- **ADDED**: Endpoint lambdas SHALL dispatch commands/queries to Foundatio.Mediator via `IMediator.SendAsync()`.
- **ADDED**: The system SHALL NOT use `MapMediatorEndpoints()` or any auto-generated endpoint mapping for existing public API routes.
- **ADDED**: Each feature area SHALL define message records in `Messages/*.cs`.
- **ADDED**: Each feature area SHALL define handler classes in `Handlers/*.cs`.
- **ADDED**: Handlers SHALL implement `ICommandHandler<TMessage, TResponse>` from Foundatio.Mediator.

### Handler Patterns

- **ADDED**: Handlers SHALL reuse existing Core repositories and services (e.g., `IEventRepository`, `IStackRepository`, `IOrganizationRepository`).
- **ADDED**: Handlers SHALL NOT create HTTP responses (IResult, status codes). They return domain objects or DTOs.
- **ADDED**: Endpoint lambdas SHALL be responsible for mapping handler results to HTTP status codes, headers, and response bodies.

### Dependency Injection

- **ADDED**: Foundatio.Mediator SHALL be registered in DI during application startup.
- **ADDED**: All handlers SHALL be auto-discovered and registered via assembly scanning.
- **ADDED**: Handlers SHALL use constructor injection for dependencies.

### File Organization

- **ADDED**: All new API code SHALL reside under `src/Exceptionless.Web/Api/`.
- **ADDED**: The folder structure SHALL include: `Endpoints/`, `Messages/`, `Handlers/`, `Middleware/`, `Filters/`, `Results/`, `Infrastructure/`, `OpenApi/`.

## Scenarios

### Scenario: Endpoint dispatches to mediator

```
Given a registered Minimal API endpoint for GET /api/v2/about
When an HTTP GET request arrives at /api/v2/about
Then the endpoint lambda resolves IMediator from DI
And sends a GetAboutQuery message
And the StatusHandler handles the message
And returns an AboutResponse
And the endpoint returns HTTP 200 with the response serialized as JSON
```

### Scenario: Handler reuses existing repository

```
Given a GetProjectByIdQuery message with a project ID
When the ProjectHandler receives the message
Then it calls IProjectRepository.GetByIdAsync() from Exceptionless.Core
And returns the Project entity
```

### Scenario: No auto-generated mediator endpoints

```
Given the application starts
When endpoint routing is configured
Then no routes are registered via MapMediatorEndpoints()
And all public API routes are explicitly mapped in *Endpoints.cs files
```
