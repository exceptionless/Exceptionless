# Spec: API Contract Preservation

## Overview

Defines the contract preservation requirements during the Minimal API migration. All existing public API behavior must remain unchanged.

## Requirements

### Route Preservation

- **MODIFIED**: The system SHALL preserve all existing v2 API routes with identical HTTP methods and paths.
- **MODIFIED**: The system SHALL preserve all existing v1 compatibility aliases with identical behavior.
- **MODIFIED**: The system SHALL preserve route parameter names and types (e.g., `{id}`, `{organizationId}`).
- **MODIFIED**: The system SHALL preserve query parameter names, types, and default values.

### Response Shapes

- **MODIFIED**: The system SHALL preserve JSON response body property names (camelCase).
- **MODIFIED**: The system SHALL preserve JSON response body nesting structure.
- **MODIFIED**: The system SHALL preserve JSON response body property types (string, number, boolean, array, object).
- **MODIFIED**: The system SHALL preserve null vs. absent field behavior in responses.

### Status Codes

- **MODIFIED**: The system SHALL return identical HTTP status codes for all success cases (200, 201, 202, 204).
- **MODIFIED**: The system SHALL return identical HTTP status codes for all error cases (400, 401, 403, 404, 409, 422, 429).

### Response Headers

- **MODIFIED**: The system SHALL preserve pagination headers (`X-Result-Count`, `Link`).
- **MODIFIED**: The system SHALL preserve configuration version headers.
- **MODIFIED**: The system SHALL preserve rate-limit headers.
- **MODIFIED**: The system SHALL preserve CORS headers.

### Pagination and Filtering

- **MODIFIED**: The system SHALL preserve pagination behavior (page, limit, before/after cursor).
- **MODIFIED**: The system SHALL preserve filtering behavior (query string filters, date ranges).
- **MODIFIED**: The system SHALL preserve sorting behavior (sort parameter).
- **MODIFIED**: The system SHALL preserve time range parsing (start, end, offset parameters).

### Backwards Compatibility

- **MODIFIED**: The system SHALL NOT remove, rename, or change the type of any existing route parameter.
- **MODIFIED**: The system SHALL NOT remove, rename, or change the type of any existing response field.
- **MODIFIED**: The system SHALL NOT change authentication requirements for any existing endpoint.
- **MODIFIED**: The system SHALL NOT change content-type requirements for any existing endpoint.

## Scenarios

### Scenario: v2 route preserved

```
Given an existing v2 route GET /api/v2/projects/{id}
When the migration is complete
Then GET /api/v2/projects/{id} returns the same response shape and status code
And accepts the same query parameters
And returns the same headers
```

### Scenario: v1 alias preserved

```
Given an existing v1 alias GET /api/v1/project/{id}
When the migration is complete
Then GET /api/v1/project/{id} still resolves to the same handler logic
And returns the same response as the v2 equivalent
```

### Scenario: Pagination headers preserved

```
Given a collection endpoint GET /api/v2/projects with results exceeding page size
When the client requests the first page
Then the response includes X-Result-Count header
And the response includes Link header with next page URL
And the header format is identical to pre-migration behavior
```

### Scenario: SDK compatibility

```
Given an Exceptionless SDK client configured with an API key
When the client submits events via POST /api/v2/events
Then the request succeeds with the same status code as pre-migration
And the client receives the same response headers
And the event is processed identically
```
