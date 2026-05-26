# Spec: API Middleware and Filters

## Overview

Defines middleware and endpoint filter behavior for the Minimal API layer.

## Requirements

### Existing Middleware Preservation

- **MODIFIED**: The system SHALL preserve `ThrottlingMiddleware` behavior unchanged (rate limiting logic, response codes, headers).
- **MODIFIED**: The system SHALL preserve `OverageMiddleware` behavior unchanged (plan overage enforcement).
- **MODIFIED**: The system SHALL NOT replace, remove, or modify existing middleware implementations.
- **MODIFIED**: The system SHALL preserve middleware pipeline ordering (ThrottlingMiddleware and OverageMiddleware execute in the same relative position).

### New Middleware

- **ADDED**: `ValidationMiddleware` SHALL validate bound DTOs and short-circuit with ProblemDetails on failure.
- **ADDED**: `LoggingMiddleware` SHALL log request/response metadata for observability.

### Endpoint Filters

- **ADDED**: `ConfigurationResponseEndpointFilter` SHALL add configuration version headers to responses (replicating existing action filter behavior).
- **ADDED**: `ApiResponseHeadersEndpointFilter` SHALL add standard API response headers (replicating existing action filter behavior).
- **ADDED**: Endpoint filters SHALL be applied at the group level via `ApiEndpointGroups.cs`.

### Pipeline Ordering

- **MODIFIED**: The middleware pipeline SHALL execute in this order:
  1. Exception handling / ProblemDetails
  2. Authentication
  3. ThrottlingMiddleware
  4. OverageMiddleware
  5. Authorization
  6. Endpoint routing + endpoint filters
- **MODIFIED**: Endpoint filters SHALL execute in registration order within the endpoint pipeline.

## Scenarios

### Scenario: ThrottlingMiddleware unchanged

```
Given a client exceeding the rate limit
When a request is made to any API endpoint
Then ThrottlingMiddleware returns HTTP 429
And the response includes rate-limit headers
And this behavior is identical to pre-migration
```

### Scenario: OverageMiddleware unchanged

```
Given an organization that has exceeded its plan limits
When a request is made to submit an event
Then OverageMiddleware returns the appropriate overage response
And this behavior is identical to pre-migration
```

### Scenario: ConfigurationResponseEndpointFilter adds headers

```
Given a successful API response from any endpoint in the group
When the response is being written
Then ConfigurationResponseEndpointFilter adds the configuration version header
And the header value matches the current configuration version
```

### Scenario: Validation filter short-circuits

```
Given a request with an invalid DTO body
When the request reaches the endpoint filter pipeline
Then ValidationMiddleware short-circuits before the endpoint lambda executes
And returns HTTP 422 ProblemDetails
```

### Scenario: Middleware order preserved

```
Given an unauthenticated request to a rate-limited endpoint
When the request enters the pipeline
Then ThrottlingMiddleware evaluates the request before authentication rejects it
And the pipeline order is: exception handling → auth → throttling → overage → authorization → routing
```
