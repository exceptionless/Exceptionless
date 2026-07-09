# Spec: API Patching

## Overview

Defines the canonical RFC 6902 patch contract and backwards-compatible partial-object behavior used by Minimal API update endpoints.

## Requirements

### JSON Patch Contract

- **MODIFIED**: The system SHALL accept RFC 6902 JSON Patch documents on migrated PATCH endpoints.
- **MODIFIED**: OpenAPI SHALL advertise `application/json-patch+json` with a typed patch-document schema.
- **MODIFIED**: First-party clients SHALL send RFC 6902 patch documents with the `application/json-patch+json` content type.
- **MODIFIED**: The system SHALL reject malformed, unsupported, or immutable-path operations before modifying the stored entity.

### Legacy Partial Object Compatibility

- **MODIFIED**: PATCH endpoints SHALL continue to accept `application/json` partial field sets used by existing clients.
- **MODIFIED**: The endpoint boundary SHALL convert each supplied legacy property to an equivalent JSON Patch replace operation.
- **MODIFIED**: Legacy partial bodies and RFC 6902 documents SHALL produce the same update semantics and validation results.

### Partial Update Semantics

- **MODIFIED**: When a field is targeted by a replace operation or present in a legacy body with a value, that value SHALL replace the existing value.
- **MODIFIED**: When a field is targeted by a replace operation or present in a legacy body with null, that field SHALL be set to null if nullable.
- **MODIFIED**: Fields not targeted by the patch SHALL remain unchanged.
- **MODIFIED**: Validation SHALL run against a copied merged model before changes are assigned to the stored entity.
- **MODIFIED**: The system SHALL return HTTP 422 when operation or merged-model validation fails.

## Scenarios

### Scenario: Partial update preserves unmodified fields

```
Given a project entity with Name="Original", DeleteBotDataEnabled=true, CustomContent="hello"
When a PATCH /api/v2/projects/{id} request replaces /name with "Updated"
Then the project Name becomes "Updated"
And DeleteBotDataEnabled remains true
And CustomContent remains "hello"
```

### Scenario: Null value clears nullable field

```
Given a project entity with Description="Some description"
When a PATCH request replaces /description with null
Then the project Description becomes null
```

### Scenario: Patch validation rejects invalid merge

```
Given a project entity with Name="Valid"
When a PATCH request replaces /name with ""
Then the operation is applied to a copy of the update model
And validation rejects the merged model because Name is required
And the response is HTTP 422 with ProblemDetails
And the original entity is NOT modified in storage
```

### Scenario: JSON Patch is accepted

```
Given any PATCH endpoint
When a request is sent with Content-Type: application/json-patch+json
Then the patch document is parsed and validated
And valid operations are applied with the endpoint's normal success response
```

### Scenario: Legacy partial object compatibility

```
Given a PATCH endpoint registered in Minimal API
When the endpoint receives an `application/json` object with partial fields
Then each supplied property is converted to an equivalent replace operation
And only those fields are applied to the entity
```
