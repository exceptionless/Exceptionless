# Spec: API Patching (Delta<T>)

## Overview

Defines patching behavior preservation during the Minimal API migration. Delta<T> remains the sole patching mechanism.

## Requirements

### Delta<T> Preservation

- **MODIFIED**: The system SHALL preserve `Delta<T>` as the patch mechanism for all PATCH endpoints.
- **MODIFIED**: The system SHALL apply only fields present in the request body to the target entity.
- **MODIFIED**: The system SHALL NOT modify fields absent from the request body.
- **MODIFIED**: The system SHALL validate the merged entity (after delta application) using MiniValidation.
- **MODIFIED**: The system SHALL return HTTP 422 if the merged entity fails validation.

### JSON Patch Exclusion

- **MODIFIED**: The system SHALL NOT introduce JSON Patch (RFC 6902) in this migration.
- **MODIFIED**: The system SHALL NOT accept `application/json-patch+json` content type on any endpoint.
- **MODIFIED**: PATCH endpoints SHALL continue to accept `application/json` with partial field sets.

### Partial Update Semantics

- **MODIFIED**: When a field is present in the patch body with a value, that value SHALL replace the existing value.
- **MODIFIED**: When a field is present in the patch body with null, that field SHALL be set to null (if nullable).
- **MODIFIED**: When a field is absent from the patch body, the existing value SHALL be preserved unchanged.

## Scenarios

### Scenario: Partial update preserves unmodified fields

```
Given a project entity with Name="Original", DeleteBotDataEnabled=true, CustomContent="hello"
When a PATCH /api/v2/projects/{id} request sends {"name": "Updated"}
Then the project Name becomes "Updated"
And DeleteBotDataEnabled remains true
And CustomContent remains "hello"
```

### Scenario: Null value clears nullable field

```
Given a project entity with Description="Some description"
When a PATCH request sends {"description": null}
Then the project Description becomes null
```

### Scenario: Delta validation rejects invalid merge

```
Given a project entity with Name="Valid"
When a PATCH request sends {"name": ""}
Then the delta is applied (Name becomes "")
And MiniValidation rejects the merged entity (Name is required)
And the response is HTTP 422 with ProblemDetails
And the original entity is NOT modified in storage
```

### Scenario: JSON Patch not accepted

```
Given any PATCH endpoint
When a request is sent with Content-Type: application/json-patch+json
Then the response is HTTP 415 Unsupported Media Type
```

### Scenario: Delta<T> binding in Minimal API

```
Given a PATCH endpoint registered in Minimal API
When the endpoint receives a JSON body with partial fields
Then Delta<T> correctly identifies which fields are present
And only those fields are applied to the entity
```
