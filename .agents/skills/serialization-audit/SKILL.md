---
name: serialization-audit
description: >
    Use this skill when verifying serialization behavior across branches, testing for backwards
    compatibility in JSON serialization changes, or comparing API request/response/storage formats
    between implementations. Apply when migrating serializers (e.g., Newtonsoft to System.Text.Json),
    adding new JSON converters, or changing naming policies.
---

# Serialization Audit Skill

## Overview

The serialization audit workflow generates branch-specific JSON snapshots of API behavior and compares them to detect behavioral differences. It exercises the full pipeline: API submission → queue → job processing → Elasticsearch storage → API response.

## Workflow

### 1. Run Audit Tests on Both Branches

The audit tests live in `tests/Exceptionless.Tests/Controllers/SerializationAuditTests.cs`. They:
- Submit events with different JSON casing conventions (snake_case, PascalCase, camelCase, mixed)
- Capture the raw request body, Elasticsearch stored document, and API response
- Save each to `audit-output/{branch-name}/{test-name}/` with files like:
  - `request.json` — what was submitted
  - `elasticsearch.json` — what was stored
  - `response.json` — what the API returned

```bash
# On main branch:
git checkout main
dotnet test tests/Exceptionless.Tests/Exceptionless.Tests.csproj -- \
  --filter-class Exceptionless.Tests.Controllers.SerializationAuditTests

# On feature branch:
git checkout feature/system-text-json-v2
dotnet test tests/Exceptionless.Tests/Exceptionless.Tests.csproj -- \
  --filter-class Exceptionless.Tests.Controllers.SerializationAuditTests
```

**Requirements:** Elasticsearch must be running (tests derive from `IntegrationTestsBase`).

### 2. Diff the Output

```bash
diff -r audit-output/main/ audit-output/feature-system-text-json-v2/ | head -100
```

Or for structured comparison:
```bash
# Compare specific test outputs
diff audit-output/main/PostEvent_SnakeCase/elasticsearch.json \
     audit-output/feature-system-text-json-v2/PostEvent_SnakeCase/elasticsearch.json
```

### 3. Categorize Differences

Common difference categories:
| Category | Example | Severity |
|----------|---------|----------|
| Casing binding failure | `ReferenceId` in ExtensionData instead of property | CRITICAL |
| Date parsing expansion | `"2026-01-15"` → `"2026-01-15T00:00:00+00:00"` | MEDIUM |
| Numeric precision | `0` vs `0.0` | LOW |
| Empty collection omission | `"tags": []` omitted | LOW/EXPECTED |
| Character encoding | `&` vs `\u0026` | LOW |

### 4. Write Targeted Tests

For each difference found, write a **unit test** that reproduces it in isolation:

```csharp
// In tests/Exceptionless.Tests/Serializer/CasingCompatibilityTests.cs
[Theory]
[InlineData("reference_id")]   // snake_case - should always work
[InlineData("ReferenceId")]    // PascalCase - must also work
[InlineData("referenceId")]    // camelCase - must also work
public void Deserialize_ReferenceId_MatchesAllCasings(string key)
{
    string json = $$"""{"type": "error", "{{key}}": "test-ref-123"}""";
    var ev = _serializer.Deserialize<PersistentEvent>(json);
    Assert.Equal("test-ref-123", ev.ReferenceId);
}
```

### 5. Implement Fixes

Common fix patterns:
- **Multi-word property casing:** Add fallback in `IJsonOnDeserialized.OnDeserialized()` to check ExtensionData for alternate casings
- **Date-only string parsing:** Check for time separator ('T') before calling `TryGetDateTimeOffset` in `ObjectToInferredTypesConverter`
- **Naming policy mismatches:** Use `[JsonPropertyName]` attributes or TypeInfo modifiers

### 6. Re-run Audit

After fixes, run audit tests into a new output directory (or the same branch directory — it overwrites):

```bash
dotnet test tests/Exceptionless.Tests/Exceptionless.Tests.csproj -- \
  --filter-class Exceptionless.Tests.Controllers.SerializationAuditTests
```

Compare again to verify differences are resolved.

## Key Files

| File | Purpose |
|------|---------|
| `tests/Exceptionless.Tests/Controllers/SerializationAuditTests.cs` | Full-pipeline audit tests |
| `tests/Exceptionless.Tests/Serializer/CasingCompatibilityTests.cs` | Unit tests for specific casing/format issues |
| `src/Exceptionless.Core/Serialization/ObjectToInferredTypesConverter.cs` | Type inference for untyped JSON values |
| `src/Exceptionless.Core/Serialization/JsonSerializerOptionsExtensions.cs` | STJ configuration (naming policy, converters) |
| `src/Exceptionless.Core/Models/Event.cs` | Event model with `OnDeserialized` fallback logic |
| `audit-output/` | Generated comparison files (gitignored) |

## Design Principles

1. **Backwards compatibility first:** Any payload that worked with Newtonsoft must still work with STJ
2. **Snake_case output, any-case input:** Serialize as snake_case, but accept PascalCase, camelCase, and snake_case on deserialization
3. **Preserve user data types:** Don't expand date-only strings to DateTimeOffset — users may store non-date strings that happen to look like dates
4. **Test the full pipeline:** Unit tests catch the bug, integration tests prove the fix works end-to-end
