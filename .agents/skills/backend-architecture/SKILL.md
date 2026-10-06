---
name: backend-architecture
description: Apply Exceptionless backend boundaries when changing API endpoints, mediator handlers, services, or jobs.
---

# Backend Architecture

## Current boundaries

- `src/Exceptionless.Web/Api/Endpoints/`: Minimal API route registration, binding, authorization, and response metadata.
- `Api/Messages/` and `Api/Handlers/`: mediator requests and their application behavior. Follow the corresponding endpoint family.
- `Api/Filters/`, `Api/Infrastructure/`, and `Api/Results/`: shared validation, request helpers, and HTTP result mapping.
- `Exceptionless.Core`: domain models, services, repositories, billing, and serialization.
- `Exceptionless.Insulation`: provider implementations for storage, caching, mail, and other infrastructure.
- `Exceptionless.Job`: background processing.

For an endpoint example, read `src/Exceptionless.Web/Api/Endpoints/OrganizationEndpoints.cs` together with its messages and handler. Endpoint groups use `AuthorizationRoles` policies and `AutoValidationEndpointFilter`; mediator results map through `ToHttpResult` and the configured result mapper. Preserve the endpoint's response and authorization contract rather than recreating it from a generic example.

## Repository conventions

Keep domain behavior in Core: Web and Job consume Core, while Insulation supplies infrastructure implementations. Do not move HTTP concerns into domain services.

- Inject `AppOptions` directly. Non-secret settings use `appsettings.yml`; environment overrides use the `EX_` prefix.
- Use Foundatio abstractions for cache, queue, message bus, file storage, distributed locks, and resilience. Use [foundatio-repositories](../foundatio-repositories/SKILL.md) for Elasticsearch operations.
- Queue entries complete after durable processing succeeds. Distinguish retryable failures from invalid input.
- Propagate cancellation where supported. Use structured logging and `ExceptionlessState` scopes for organization/project context.
- WebSocket notifications use `MessageBusBroker` and `WebSocketConnectionManager`, backed by the message bus.
- Follow root guidance for OpenAPI snapshots and compatibility. See [serialization architecture](../../../docs/serialization-architecture.md) when changing JSON behavior.

## Implementation details

- Prefer constructor injection and readonly dependency fields. Follow `.editorconfig`, use the `Async` suffix for asynchronous methods, and avoid adding regions.
- Use `ICacheClient`, `IQueue<T>`, `IMessageBus`, `IFileStorage`, `ILockProvider`, and `IResiliencePolicyProvider` rather than creating provider-specific parallel abstractions.
- Queue consumers commonly derive from `QueueJobBase<T>`; scheduled jobs use bases such as `JobWithLockBase` and `[Job]` scheduling attributes. Preserve lock, completion, abandonment, and cancellation behavior when modifying a job. Invalid input should not enter an endless retry cycle.
- Use DataAnnotations and the existing MiniValidation integration for model validation. Preserve endpoint binding/validation errors and mediator-to-HTTP result mapping; inspect `Api/Filters/AutoValidationEndpointFilter.cs`, `Api/Results/ApiResultMapper.cs`, and the endpoint's tests for the expected status and ProblemDetails shape.
- Use structured message templates, not interpolated log messages, and `ExceptionlessState` for shared context. Never log passwords, API keys, full tokens, or sensitive payloads; prefer necessary identifiers and safe summaries.
