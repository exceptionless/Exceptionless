# Spec: API OpenAPI Generation

## Overview

Defines OpenAPI document generation requirements for runtime and build-time, including snapshot testing and route manifests.

## Requirements

### Runtime OpenAPI Generation

- **MODIFIED**: The system SHALL serve an OpenAPI 3.x document at `/docs/v2/openapi.json` at runtime.
- **MODIFIED**: The system SHALL serve Scalar API documentation UI (at existing Scalar path).
- **ADDED**: The system SHALL use `Microsoft.AspNetCore.OpenApi` for runtime document generation.
- **ADDED**: All Minimal API endpoints SHALL include OpenAPI metadata (summary, description, response types, parameters).
- **ADDED**: Operation IDs SHALL be derived from endpoint method metadata.

### Build-Time OpenAPI Generation

- **ADDED**: The system SHALL generate an OpenAPI document as a build artifact during `dotnet build`.
- **ADDED**: The build-time artifact SHALL be generated via `Microsoft.Extensions.ApiDescription.Server`.
- **ADDED**: The build-time artifact SHALL be deterministic (same source = same output).

### Route Manifest Tests

- **ADDED**: The system SHALL include a test that enumerates all registered endpoints (HTTP method + path).
- **ADDED**: The route manifest test SHALL compare against a checked-in baseline file.
- **ADDED**: The route manifest test SHALL fail if any route is added, removed, or changed without updating the baseline.
- **ADDED**: The route manifest format SHALL be one line per route: `{METHOD} {path}` sorted alphabetically.

### OpenAPI Snapshot Tests

- **ADDED**: The system SHALL include a test that compares the generated OpenAPI document against a checked-in baseline.
- **ADDED**: The OpenAPI snapshot test SHALL fail if the document schema changes without updating the baseline.
- **ADDED**: The snapshot comparison SHALL ignore non-semantic differences (whitespace, key ordering).

## Scenarios

### Scenario: Runtime OpenAPI document served

```
Given the application is running
When a GET request is made to /docs/v2/openapi.json
Then the response is HTTP 200
And the Content-Type is application/json
And the body is a valid OpenAPI 3.x document
And all registered endpoints are present in the document
```

### Scenario: Scalar docs accessible

```
Given the application is running
When a browser navigates to the Scalar docs URL
Then the Scalar UI loads successfully
And displays documentation for all API endpoints
```

### Scenario: Build-time artifact generated

```
Given the source code has not changed
When `dotnet build src/Exceptionless.Web/Exceptionless.Web.csproj` is run
Then an openapi.json file is generated in the build output
And running the build again produces an identical file
```

### Scenario: Route manifest detects new route

```
Given a baseline route manifest with N routes
When a developer adds a new endpoint without updating the manifest
Then the route manifest test fails
And the failure message indicates which route was added
```

### Scenario: Route manifest detects removed route

```
Given a baseline route manifest with route "GET /api/v2/projects"
When that endpoint is removed without updating the manifest
Then the route manifest test fails
And the failure message indicates which route was removed
```

### Scenario: OpenAPI snapshot detects schema change

```
Given a baseline OpenAPI snapshot
When a response schema is changed (e.g., field renamed)
Then the OpenAPI snapshot test fails
And the failure shows the diff between baseline and current
```
