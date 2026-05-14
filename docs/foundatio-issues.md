# Foundatio Upstream Issues — STJ Migration RCA Report

**Discovered during:** Exceptionless PR #2135 (Newtonsoft.Json → System.Text.Json migration)  
**Date:** May 2026  
**Branch:** `feature/system-text-json-v2`

This document contains the root-cause analysis and proposed solutions for three issues in Foundatio/Foundatio.Repositories discovered during the STJ migration. Each section is formatted as a GitHub issue ready to file upstream.

---

## Issue 1: `Foundatio.Repositories` still depends on `Foundatio.JsonNet` even when STJ is the registered serializer

**Repo:** `FoundatioFx/Foundatio.Repositories`  
**Severity:** Medium — forces Newtonsoft.Json into every consumer's dependency graph even after fully migrating to STJ

### Issue 1 — Background

When Exceptionless registers `SystemTextJsonSerializer` as `ITextSerializer`/`ISerializer` in DI, the Foundatio runtime never calls `Foundatio.JsonNet` — the override is complete and verified. However, the NuGet package graph still carries Newtonsoft.Json as a transitive dependency:

```text
Foundatio.Repositories.Elasticsearch v8.0.0-beta1
  └─ Foundatio.Repositories v8.0.0-beta1
       └─ Foundatio.JsonNet v13.0.1
            └─ Newtonsoft.Json v13.0.4
```

No source file in `src/` references `Foundatio.JsonNet`, `JsonNetSerializer`, or any Newtonsoft type — it is entirely dead weight.

### Issue 1 — Root Cause

`Foundatio.Repositories.csproj` lists `Foundatio.JsonNet` as a hard `<PackageReference>` dependency rather than an optional/conditional one. Since Foundatio now ships `SystemTextJsonSerializer` natively, the JsonNet package should be opt-in (e.g., a separate `Foundatio.JsonNet` package that consumers reference only when needed) or removed if `SystemTextJsonSerializer` is the new default.

### Issue 1 — Impact

- Doubles effective serialization library payload on every deployment
- Prevents consumers from declaring "we have no Newtonsoft.Json dependency" in security/compliance audits
- Risk: if Newtonsoft is loaded in the same AppDomain, some internal code path could inadvertently use it (unlikely but not zero)

### Issue 1 — Proposed Solution

Option A (preferred): Make the `Foundatio.JsonNet` reference conditional — only include it when the consumer explicitly references it.  
Option B: Publish `Foundatio.Repositories` without a default serializer dependency and let consumers choose.  
Option C: Flip the default — make `SystemTextJsonSerializer` the default and move `Foundatio.JsonNet` to a separate opt-in package.

### Issue 1 — Workaround in Exceptionless

None needed at runtime. The dead dependency is accepted until Foundatio resolves this upstream.

---

## Issue 2: `FieldValueHelper.ToFieldValue` does not respect `[JsonStringEnumMemberName]`

**Repo:** `FoundatioFx/Foundatio.Repositories`  
**Severity:** High — causes silent query bugs when enum values have JSON name overrides

### Issue 2 — Background

`StackStatus` is an enum that uses `[JsonStringEnumMemberName]` to define its wire names for STJ:

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StackStatus
{
    [JsonStringEnumMemberName("open")]      Open = 0,
    [JsonStringEnumMemberName("fixed")]     Fixed = 10,
    [JsonStringEnumMemberName("regressed")] Regressed = 20,
    [JsonStringEnumMemberName("snoozed")]   Snoozed = 30,
    [JsonStringEnumMemberName("ignored")]   Ignored = 40,
    [JsonStringEnumMemberName("discarded")] Discarded = 50,
}
```

The Elasticsearch index stores `StackStatus.Open` as the JSON string `"open"`.  
`FieldValueHelper.ToFieldValue(StackStatus.Open)` returns `"Open"` (Pascal-case enum name).

This means a query like `.FieldEquals(f => f.Status, StackStatus.Open)` generates a Term query for `"Open"` instead of `"open"`, returning **zero results** even though matching documents exist.

### Issue 2 — Root Cause

`FieldValueHelper.ToFieldValue` in Foundatio.Repositories uses `value.ToString()` for enum values. It doesn't check:

- `[JsonStringEnumMemberName]` (STJ's attribute, .NET 9+)
- `[EnumMember(Value = "...")]` (System.Runtime.Serialization)
- `[JsonConverter(typeof(JsonStringEnumConverter))]` on the enum type

When a consumer configures STJ with `JsonStringEnumConverter` and decorates enum members with `[JsonStringEnumMemberName]`, the actual stored value in Elasticsearch is the attribute-defined name, but `ToFieldValue` is unaware of this.

### Issue 2 — Impact

Any repository `FieldEquals` / `FieldIn` call passing an enum value where `[JsonStringEnumMemberName]` overrides the default name will silently produce wrong ES queries — matching zero documents. This is a data correctness bug, not a crash.

### Issue 2 — Reproduction

```csharp
// StackStatus.Open is stored in ES as "open"
// This query generates Term { field: "status", value: "Open" } → 0 results
.FieldEquals(f => f.Status, StackStatus.Open)

// Workaround: use the string literal
// TODO: Use StackStatus.Open when Foundatio's FieldValueHelper.ToFieldValue
// respects [JsonStringEnumMemberName]
.FieldEquals(f => f.Status, "open")
```

See: `src/Exceptionless.Core/Repositories/StackRepository.cs` line 35-36

### Issue 2 — Proposed Solution

Update `FieldValueHelper.ToFieldValue` to check for `[JsonStringEnumMemberName]` when converting enum values. Pseudo-code:

```csharp
public static FieldValue ToFieldValue(object value)
{
    if (value is Enum enumValue)
    {
        var field = enumValue.GetType().GetField(enumValue.ToString());
        if (field is not null)
        {
            var attr = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>();
            if (attr is not null)
                return new FieldValue(attr.Name);
        }
        // fallback to existing behavior
    }
    // ... rest of method
}
```

Also consider checking `[EnumMember(Value = "...")]` for JSON.NET consumers.

### Issue 2 — Workaround in Exceptionless

Use string literal with a TODO comment until this is fixed upstream:

```csharp
// TODO: Use StackStatus.Open when Foundatio's FieldValueHelper.ToFieldValue 
// respects [JsonStringEnumMemberName]
.FieldEquals(f => f.Status, "open")
```

**Files affected:** `src/Exceptionless.Core/Repositories/StackRepository.cs`

---

## Issue 3: No typed Foundatio query API for `ExistsQuery` on dynamic template fields

**Repo:** `FoundatioFx/Foundatio.Repositories`  
**Severity:** Low — forces raw Elasticsearch query escape hatch for a common use case

### Issue 3 — Background

Elasticsearch dynamic templates allow documents to declare ad-hoc indexed fields at write time using a naming convention (`idx.{fieldName}-{type}`). Exceptionless uses:

```text
idx.*-b  → boolean
idx.*-d  → date
idx.*-n  → double
idx.*-r  → keyword (reference)
idx.*-s  → keyword (string)
```

To query open sessions, we need to find events where `idx.@session_end-d` **does not exist**:

```csharp
// Current implementation — forced to use raw ES query
.ElasticFilter(new BoolQuery
{
    MustNot = [new ExistsQuery { Field = $"idx.{Event.KnownDataKeys.SessionEnd}-d" }]
});
```

This is the only raw `ElasticFilter` remaining after the STJ migration. We can't use the typed API because `idx.@session_end-d` is not a C# model property — it's a string-keyed dynamic index field.

**File:** `src/Exceptionless.Core/Repositories/EventRepository.cs` line 38

### Issue 3 — Root Cause

Foundatio's `FieldEmpty` / `FieldExists` query extension methods accept `Expression<Func<T, TField>>` (typed model property expressions) but do not have overloads accepting a raw `string` field name. Dynamic template fields have no corresponding C# property to reference.

### Issue 3 — Proposed Solution

Add `string`-based overloads to the Foundatio query builder:

```csharp
// New overloads
public static IRepositoryQuery<T> FieldExists<T>(this IRepositoryQuery<T> query, string fieldName) { ... }
public static IRepositoryQuery<T> FieldNotExists<T>(this IRepositoryQuery<T> query, string fieldName) { ... }
```

This would let consumers write:

```csharp
// Desired — fully typed via Foundatio
.FieldNotExists($"idx.{Event.KnownDataKeys.SessionEnd}-d")
```

### Issue 3 — Workaround in Exceptionless

Raw `.ElasticFilter(...)` escape hatch. Functionally correct but bypasses Foundatio's query abstraction layer.

---

## Summary Table

| # | Issue | Foundatio Repo | Severity | Workaround |
| --- | ----- | -------------- | -------- | ---------- |
| 1 | `Foundatio.Repositories` pulls in `Foundatio.JsonNet` transitively even when STJ is used | Foundatio.Repositories | Medium | None needed at runtime |
| 2 | `FieldValueHelper.ToFieldValue` ignores `[JsonStringEnumMemberName]` → wrong ES queries for enums with JSON name overrides | Foundatio.Repositories | **High** | String literal with TODO |
| 3 | No `FieldExists(string)` / `FieldNotExists(string)` overload for dynamic template fields | Foundatio.Repositories | Low | `.ElasticFilter(new BoolQuery {...})` |

## PR Context

All workarounds are in place in `feature/system-text-json-v2` (PR #2135). The TODO comment in `StackRepository.cs` tracks Issue 2. Once Foundatio ships fixes, the workarounds can be removed.
