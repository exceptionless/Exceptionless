# Spec: API ProblemDetails

## Overview

Defines the ProblemDetails error response format for all API error responses.

## Requirements

### ProblemDetails Shape

- **MODIFIED**: All error responses SHALL use the RFC 9457 ProblemDetails format.
- **MODIFIED**: ProblemDetails responses SHALL include the `instance` field set to the request path.
- **MODIFIED**: ProblemDetails responses SHALL include a `reference-id` extension field set to the request trace ID.
- **MODIFIED**: ProblemDetails responses SHALL include the `errors` map for validation failures.
- **MODIFIED**: The `errors` map SHALL use `lower_underscore` field name keys.
- **MODIFIED**: ProblemDetails responses SHALL include `type`, `title`, and `status` fields.

### Status Code Mapping

- **MODIFIED**: Validation failures SHALL produce HTTP 422 with ProblemDetails.
- **MODIFIED**: Authentication failures SHALL produce HTTP 401 with ProblemDetails.
- **MODIFIED**: Authorization failures SHALL produce HTTP 403 with ProblemDetails.
- **MODIFIED**: Not-found errors SHALL produce HTTP 404 with ProblemDetails.
- **MODIFIED**: Conflict errors SHALL produce HTTP 409 with ProblemDetails.
- **MODIFIED**: Rate-limit errors SHALL produce HTTP 429 with ProblemDetails.

### Centralization

- **ADDED**: ProblemDetails customization SHALL be configured once via `AddProblemDetails()` in DI.
- **ADDED**: All endpoints SHALL use the centralized ProblemDetails configuration without per-endpoint customization.
- **ADDED**: Exception handling middleware SHALL produce ProblemDetails for unhandled exceptions (500).

## Scenarios

### Scenario: Validation error ProblemDetails

```
Given a request that fails validation on fields "name" and "url"
When the validation middleware produces an error response
Then the response status is 422
And the Content-Type is application/problem+json
And the body contains:
  - type: a URI identifying the error type
  - title: "Validation Failed" or similar
  - status: 422
  - instance: the request path (e.g., "/api/v2/projects")
  - reference-id: the request trace ID
  - errors: {"name": ["Name is required"], "url": ["URL is not valid"]}
```

### Scenario: Not-found ProblemDetails

```
Given a request for GET /api/v2/projects/{id} with a non-existent ID
When the handler returns null/not-found
Then the response status is 404
And the body is ProblemDetails with instance set to the request path
And reference-id is present
```

### Scenario: Unhandled exception ProblemDetails

```
Given a request that triggers an unhandled exception in a handler
When the exception propagates to the middleware
Then the response status is 500
And the body is ProblemDetails
And sensitive exception details are NOT exposed in production
And reference-id is present for correlation
```

### Scenario: Error keys are lower_underscore

```
Given a model with properties "OrganizationId" and "ProjectName" that fail validation
When the ProblemDetails errors map is constructed
Then keys are "organization_id" and "project_name"
```
