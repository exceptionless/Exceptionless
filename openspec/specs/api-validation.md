# Spec: API Validation

## Overview

Defines validation behavior for the Minimal API endpoint layer, covering required request fields, automatic DataAnnotation validation, MiniValidation for complex cases, and patch validation.

## Requirements

### Automatic DataAnnotation Validation

- **ADDED**: The system SHALL automatically validate `[FromBody]` DTOs using DataAnnotation attributes before the endpoint lambda executes.
- **ADDED**: The system SHALL return HTTP 422 with a ProblemDetails body when automatic validation fails.
- **ADDED**: The system SHALL include all validation errors in the `errors` map of the ProblemDetails response.

### MiniValidation for Complex Cases

- **ADDED**: The system SHALL use MiniValidation for validation that cannot be expressed with DataAnnotations (cross-field, conditional).
- **ADDED**: The system SHALL use MiniValidation to validate the merged entity after applying patch operations.
- **ADDED**: MiniValidation failures SHALL produce HTTP 422 with ProblemDetails body.

### Validation Error Shape

- **MODIFIED**: Validation error responses SHALL use `lower_underscore` keys in the errors map (e.g., `organization_id`, not `OrganizationId`).
- **MODIFIED**: Validation error responses SHALL be ProblemDetails with `type`, `title`, `status`, `instance`, and `errors` fields.
- **MODIFIED**: The `errors` map SHALL be a dictionary of field name → array of error messages.
- **MODIFIED**: Endpoints that previously returned HTTP 400 for missing implicitly required request fields SHALL preserve that status and validation error shape.

### Patch Validation

- **MODIFIED**: The system SHALL preserve partial update semantics.
- **MODIFIED**: When a PATCH request is received, only fields present in the request body SHALL be applied to the entity.
- **MODIFIED**: After applying the patch, the merged entity SHALL be validated using MiniValidation.
- **MODIFIED**: The system SHALL advertise RFC 6902 JSON Patch while continuing to accept legacy `application/json` partial object bodies.

## Scenarios

### Scenario: Required-field validation rejects incomplete DTO

```
Given a POST /api/v2/tokens endpoint expecting a body with [Required] Name field
When a request is sent with an empty Name
Then the response is HTTP 422
And the body is ProblemDetails with errors map containing "name" key
And the error message indicates the field is required
```

### Scenario: Legacy implicit required-field response is preserved

```
Given POST /api/v2/webhooks previously returned HTTP 400 for missing non-nullable request fields
When a request omits organization_id, project_id, or event_types
Then the response is HTTP 400
And the body preserves the ProblemDetails errors map for the missing fields
```

### Scenario: MiniValidation validates merged patch

```
Given a PATCH /api/v2/projects/{id} endpoint
When a request patches the Name field to an empty string
Then the system applies the delta to the existing project
And validates the merged project with MiniValidation
And returns HTTP 422 because Name is required
```

### Scenario: Patch preserves unmodified fields

```
Given a project with Name="MyProject" and DeleteBotDataEnabled=true
When a PATCH request sends only {"name": "NewName"}
Then only the Name field is updated to "NewName"
And DeleteBotDataEnabled remains true
```

### Scenario: Validation errors use lower_underscore keys

```
Given a POST endpoint with validation errors on OrganizationId and ProjectName
When validation fails
Then the errors map contains keys "organization_id" and "project_name"
And NOT "OrganizationId" or "ProjectName"
```
