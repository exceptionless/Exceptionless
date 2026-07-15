# Serialization Architecture

This document describes the complete serialization architecture after the Newtonsoft.Json → System.Text.Json (STJ) migration. It covers every serialization path, data transformation, naming convention, and compatibility consideration.

## Table of Contents

1. [Serializer Configuration](#serializer-configuration)
2. [Serialization Paths](#serialization-paths)
3. [Data Flow: Event Lifecycle](#data-flow-event-lifecycle)
4. [GetValue\<T\> Dictionary Extraction](#getvaluet-dictionary-extraction)
5. [ObjectToInferredTypesConverter](#objecttoinferredtypesconverter)
6. [Event Upgrade Pipeline](#event-upgrade-pipeline)
7. [Model Annotations & Naming](#model-annotations--naming)
8. [Elasticsearch Divergences](#elasticsearch-divergences)
9. [Transitive Dependencies](#transitive-dependencies)
10. [Production Safety Guarantees](#production-safety-guarantees)

---

## Serializer Configuration

### Primary Configuration (`ConfigureExceptionlessDefaults`)

**File:** `src/Exceptionless.Core/Serialization/JsonSerializerOptionsExtensions.cs`

All serialization in the app starts from a single extension method that configures `JsonSerializerOptions`:

```csharp
options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
options.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
options.PropertyNameCaseInsensitive = true;
options.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All); // XSS-safe
options.Converters.Add(new ObjectToInferredTypesConverter());
options.IncludeFields = true;
options.RespectNullableAnnotations = true;
options.TypeInfoResolver = new DefaultJsonTypeInfoResolver
{
    Modifiers = { EmptyCollectionModifier.SkipEmptyCollections }
};
```

**Key behaviors:**
- **Naming:** All properties serialize as `snake_case_lower` (e.g., `LastOccurrence` → `"last_occurrence"`)
- **Nulls:** Null properties are omitted from output
- **Empty collections:** Empty `[]` and `{}` are omitted (matches Newtonsoft behavior)
- **Case-insensitive deserialization:** Reads both `"stack_trace"` and `"StackTrace"` for the same property
- **XSS-safe encoding:** `<`, `>`, `&`, `'` are escaped in all JSON output

### DI Registration

**File:** `src/Exceptionless.Core/Bootstrapper.cs`

```
JsonSerializerOptions (singleton) → ConfigureExceptionlessDefaults()
    ↓
ITextSerializer (singleton) → SystemTextJsonSerializer(options)
    ↓
ISerializer (alias) → same instance
```

Every Foundatio infrastructure component (queues, cache, message bus) resolves `ISerializer` from DI and gets the STJ-backed serializer.

---

## Serialization Paths

### Path 1: API Responses (ASP.NET Core)

**Config:** `Startup.cs` → `.AddJsonOptions(o => o.JsonSerializerOptions.ConfigureExceptionlessDefaults())`

- Separate `JsonSerializerOptions` instance from DI, but identically configured
- Additional converter: `DeltaJsonConverterFactory` for PATCH operations
- Also configured for Minimal APIs: `.ConfigureHttpJsonOptions(...)`

### Path 2: Elasticsearch Documents

**Config:** `ExceptionlessElasticConfiguration.CreateElasticClient()` → `DefaultSourceSerializer`

Uses `ConfigureExceptionlessDefaults()` + `ConfigureFoundatioRepositoryDefaults()` with these **overrides**:

| Setting | API/App | Elasticsearch | Reason |
|---------|---------|---------------|--------|
| `RespectNullableAnnotations` | `true` | `false` | Legacy ES data has unexpected nulls |
| `ObjectToInferredTypesConverter` | `preferInt64: false` | `preferInt64: true` | ES maps numbers as long |
| `JsonStringEnumConverter` | Registered (from Foundatio) | **Removed** | Most enums stored as integers in ES |

**Critical implication:** A number `42` in Event.Data:
- In API response: serialized as `42` (int)
- In Elasticsearch: stored as `42L` (long via preferInt64)
- Both round-trip correctly because deserialization handles both types

### Path 3: Queue Messages (Redis/Azure/SQS)

**Config:** Inherits DI `ISerializer` → `SystemTextJsonSerializer`

Queue payloads (e.g., `EventPost`, `WebHookNotification`, `WorkItemData`) are serialized with the standard options. Messages produced before the migration used Newtonsoft via `Foundatio.JsonNet`. **During rolling deploy, old messages in queues will be deserialized by STJ** — this works because:
- `PropertyNameCaseInsensitive = true` reads both PascalCase and snake_case
- Queue message types are simple DTOs without complex nested data

### Path 4: Message Bus (Redis Pub/Sub)

**Config:** Inherits DI `ISerializer` → `SystemTextJsonSerializer`

Messages: `EntityChanged`, `PlanChanged`, `UserMembershipChanged`, `ReleaseNotification`, `SystemNotification`. All are simple DTOs that serialize cleanly.

### Path 5: Cache (Redis/InMemory)

**Config:** Inherits DI `ISerializer` → `SystemTextJsonSerializer`

Cached values: Organizations, Projects, Stacks, Tokens, Users. Cache is ephemeral — keys expire. No migration needed; old cached values are simply evicted.

### Path 6: WebSocket Messages

**Config:** `WebSocketConnectionManager` resolves `ITextSerializer` from DI.

Messages sent to browser clients use `serializer.SerializeToString(message)` with the standard snake_case options. The JavaScript/TypeScript frontend expects snake_case.

### Path 7: Webhook Payloads

**Config:** `WebHooksJob` uses DI `JsonSerializerOptions` → `PostAsJsonAsync(url, data, options)`

Webhook payloads are serialized as snake_case JSON. This matches the previous behavior (Newtonsoft used `LowerCaseUnderscorePropertyNamesContractResolver`).

### Path 8: Email Templates

**Config:** `Mailer` uses DI `ITextSerializer` for model data extraction.

Event data is extracted via `GetValue<T>()` for building email template models. No direct JSON serialization for the template rendering.

---

## Data Flow: Event Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. INGESTION (EventPostsJob)                                    │
│    HTTP POST body → JsonSerializer.Deserialize<PersistentEvent>  │
│    Options: ConfigureExceptionlessDefaults()                     │
│    • Unknown JSON fields → ExtensionData (JsonElement dict)      │
│    • IJsonOnDeserialized.OnDeserialized() merges into Data       │
│    • ObjectToInferredTypesConverter: objects → Dict<string,obj>  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│ 2. PIPELINE PROCESSING                                          │
│    EventProcessor → Plugin chain (Error, SimpleError, Request,  │
│    Environment, Geo, Session, Angular, Privacy)                  │
│    • Reads typed data via GetValue<T>(key, serializer)           │
│    • Mutates (e.g., SetTargetInfo, strip PII)                   │
│    • Writes back via Data[key] = mutatedObject                  │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│ 3. ELASTICSEARCH STORAGE                                        │
│    Repository.SaveAsync(event)                                   │
│    Options: ConfigureExceptionlessDefaults() + ES overrides      │
│    • preferInt64: true (all ints stored as long)                 │
│    • No JsonStringEnumConverter (enums as integers)              │
│    • RespectNullableAnnotations: false                           │
│    • EmptyCollectionModifier omits empty arrays/dicts            │
└───────────────────────────┬─────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────┐
│ 4. API RESPONSE                                                 │
│    Repository.GetByIdAsync() → ES deserializes → C# model       │
│    Controller returns model → ASP.NET serializes to response     │
│    Options: ConfigureExceptionlessDefaults()                     │
│    • Client sees snake_case JSON                                 │
│    • Data dict keys preserved as-is from ES                      │
│    • Numbers: int/long depending on value                        │
└─────────────────────────────────────────────────────────────────┘
```

### Extension Data Merge (Ingestion)

When an event is posted with known data keys at the root level (legacy clients):

```json
{"type": "error", "@error": {...}, "@request": {...}, "custom_field": "value"}
```

STJ deserializes known properties (`type`), then captures unknown keys (`@error`, `@request`, `custom_field`) in `ExtensionData` as `Dictionary<string, JsonElement>`. After deserialization, `OnDeserialized()` merges them into `Data` using `ObjectToInferredTypesConverter.ConvertJsonElement()`:

- Objects → `Dictionary<string, object?>` (case-insensitive keys)
- Arrays → `List<object?>`
- Strings → `string` (with DateTimeOffset detection for ISO 8601)
- Numbers → `int`/`long`/`decimal`
- Booleans → `bool`

---

## GetValue\<T\> Dictionary Extraction

**File:** `src/Exceptionless.Core/Extensions/DataDictionaryExtensions.cs`

The `GetValue<T>()` method extracts typed values from `DataDictionary` (which stores `object?` values). After ES round-trip, values in `Data` are `Dictionary<string, object?>` (from ObjectToInferredTypesConverter).

### Extraction Strategy

```
Data["@error"] → Dictionary<string, object?> (in-memory)
    ↓ Serialize to JSON with appropriate options
    ↓ Deserialize as T via ITextSerializer
    = Error object with all properties populated
```

### Serialization Options

The method uses a single serialization options set for the **dictionary→JSON serialize step**:

```csharp
s_dictSerializeOptions = {
    PropertyNamingPolicy = SnakeCaseLower,
    DefaultIgnoreCondition = WhenWritingNull
};
```

**Key design decisions:**
- `PropertyNamingPolicy = SnakeCaseLower` converts C# property names to snake_case when serializing typed objects nested within dictionaries
- **No `DictionaryKeyPolicy`** — user-provided dictionary keys (e.g., `Error.Data`, `QueryString`) are preserved exactly as-is
- `PropertyNameCaseInsensitive = true` on the main deserializer handles matching snake_case keys back to PascalCase C# properties

### Why No DictionaryKeyPolicy

`DictionaryKeyPolicy` applies recursively to ALL dictionary keys at ALL nesting levels, which would corrupt user-provided data:

```
// User submits Error.Data with key "SomeProp"
// With DictionaryKeyPolicy: "SomeProp" → "some_prop" — DATA CORRUPTION
// Without DictionaryKeyPolicy: "SomeProp" preserved as-is ✓
```

In production, ES stores typed property names as snake_case (from `PropertyNamingPolicy`). When `GetValue<T>` deserializes from this data, `PropertyNameCaseInsensitive` handles the snake_case→PascalCase property matching. Dictionary keys don't need normalization because they're user data, not C# property names.

---

## ObjectToInferredTypesConverter

**File:** `src/Exceptionless.Core/Serialization/ObjectToInferredTypesConverter.cs`

Custom `JsonConverter<object?>` that replaces STJ's default behavior of deserializing `object`-typed properties as `JsonElement`.

### Type Inference Rules

| JSON Token | API Mode (preferInt64: false) | ES Mode (preferInt64: true) |
|------------|-------------------------------|------------------------------|
| `true`/`false` | `bool` | `bool` |
| Integer (fits int32) | `int` | `long` |
| Integer (fits int64) | `long` | `long` |
| Float/decimal | `decimal` | `double` |
| ISO 8601 string | `DateTimeOffset` | `DateTimeOffset` |
| DateTime string | `DateTime` | `DateTime` |
| Other string | `string` | `string` |
| `null` | `null` | `null` |
| Object `{}` | `Dictionary<string, object?>` (OrdinalIgnoreCase) | Same |
| Array `[]` | `List<object?>` | Same |

### Number Representation Integrity

The converter checks raw JSON bytes for decimal points (`'.'`) and exponents (`'e'`/`'E'`). A value like `0.0` stays as `double`/`decimal`, never coerced to `0L`.

### Static ConvertJsonElement Helper

Used by `Event.OnDeserialized()` to convert `JsonElement` values from `[JsonExtensionData]` into inferred .NET types. Matches the same rules as the converter but operates on a pre-read `JsonElement` rather than a `Utf8JsonReader`.

---

## Event Upgrade Pipeline

Events from older clients are upgraded to the current format via JsonNode manipulation.

### Upgrade Chain

```
GetVersion → determines event version from JSON
    ↓
V1R500_EventUpgrade (version ≤ 1.0.0-r500)
    • Renames Error.ExtraData → Error.Data
V1R844_EventUpgrade (version ≤ 1.0.0-r844)
    • Moves error info from root into structured Error object
    • Renames InnerException → Inner
    • Processes exception info
V1R850_EventUpgrade (version ≤ 1.0.0-r850)
    • Renames RequestInfo properties
V2_EventUpgrade (version ≤ 2.0)
    • Complete restructure: flat format → nested Event format
    • Moves ExceptionlessClientInfo → @submission_client
    • Moves @User → @user_description + @user (as typed objects)
    • Creates @error structure from root Code/Type/Inner/StackTrace/TargetMethod
    • Handles 404 events (type: "404" vs type: "error")
    • Renames ExtendedData → Data
    • Processes __ExceptionInfo extra properties
```

### STJ Implementation Notes

All upgraders operate on `JsonNode` (`JsonObject`/`JsonArray`/`JsonValue`) instead of Newtonsoft's `JObject`/`JToken`. Key differences:
- `JsonNode` children must be **detached** before adding to another parent (no implicit cloning)
- `V2_EventUpgrade` uses `JsonSerializer.SerializeToNode(new UserDescription(...))` for typed → node conversion (no options needed for simple DTOs — snake_case isn't required here because the root Event serializer handles the final format)
- Parse uses default `JsonNodeOptions` (max depth 64 from STJ default)

---

## Model Annotations & Naming

### SnakeCaseLower Naming Policy

STJ's `JsonNamingPolicy.SnakeCaseLower` converts property names:
- `LastOccurrence` → `last_occurrence`
- `StackTrace` → `stack_trace`
- `IPAddress` → `ip_address`

### JsonPropertyName Overrides (Legacy Compatibility)

Some properties need names that differ from what `SnakeCaseLower` would produce:

| Model | Property | Override | Why |
|-------|----------|----------|-----|
| `EnvironmentInfo` | `OSName` | `"o_s_name"` | Legacy: Newtonsoft produced `o_s_name` (letter-by-letter), not `os_name` |
| `EnvironmentInfo` | `OSVersion` | `"o_s_version"` | Same — preserves ES mapping compatibility |

### SlackToken Model (External API)

Slack API requires specific JSON property names. All properties have explicit `[JsonPropertyName]` to match Slack's API contract regardless of our naming policy.

### StackStatus Enum

```csharp
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StackStatus
{
    [JsonStringEnumMemberName("open")] Open = 0,
    [JsonStringEnumMemberName("fixed")] Fixed = 10,
    [JsonStringEnumMemberName("regressed")] Regressed = 20,
    [JsonStringEnumMemberName("snoozed")] Snoozed = 30,
    [JsonStringEnumMemberName("ignored")] Ignored = 40,
    [JsonStringEnumMemberName("discarded")] Discarded = 50
}
```

The type-level `[JsonConverter(typeof(JsonStringEnumConverter))]` ensures this enum ALWAYS serializes as a string (even in ES where the global JsonStringEnumConverter is removed). This is mapped as a `Keyword` field in the StackIndex.

### EmptyCollectionModifier

Omits empty collections and dictionaries from serialized output:
- `Tags: []` → omitted
- `References: []` → omitted
- `Data: {}` → omitted

This matches the Newtonsoft behavior and keeps ES documents compact.

---

## Elasticsearch Divergences

### Why ES Serialization Differs

Elasticsearch stores documents long-term. The ES serializer must:
1. **Store integers as long** — ES maps `number` fields to `long`. If we stored `int`, reading back would fail for values that ES promotes to long internally.
2. **Allow nulls in legacy data** — Old documents may have null in non-nullable properties. `RespectNullableAnnotations = false` prevents deserialization failures.
3. **Store enums as integers** — Most enums (EventType, etc.) are stored as integer values in ES indices. Exception: `StackStatus` uses its own type-level converter.

### Index Mappings

Dynamic templates handle user-provided indexed data (`idx.*`):
```
*-b → boolean
*-d → date
*-n → double
*-r → keyword (reference, max 256 chars)
*-s → keyword (string, max 1024 chars)
```

### Rolling Deploy Safety

During deployment where old instances use Newtonsoft and new instances use STJ:
- **ES documents:** Both serializers produce compatible snake_case JSON. STJ reads old documents fine (`PropertyNameCaseInsensitive = true`).
- **Queue messages:** STJ can read Newtonsoft-produced messages (case-insensitive + simple DTOs).
- **Cache:** Redis cache is ephemeral with TTL. Old entries expire naturally.
- **Message bus:** Pub/sub is real-time. Brief incompatibility window is self-healing.

---

## Transitive Dependencies

### Newtonsoft.Json (Transitive Only — NOT USED)

```
Foundatio.Repositories.Elasticsearch v8.0.0-beta2
  → Foundatio.Repositories v8.0.0-beta2
    → Foundatio.JsonNet v13.0.1
      → Newtonsoft.Json v13.0.4

Stripe.net v51.1.0
  → Newtonsoft.Json v13.0.4
```

**Impact:**
- `Foundatio.JsonNet` is a **transitive dependency only**. Our DI explicitly registers `SystemTextJsonSerializer` as `ITextSerializer`/`ISerializer`, overriding any default Foundatio would use.
- `Stripe.net` uses Newtonsoft internally for Stripe API communication. This is isolated to Stripe SDK internals and doesn't affect our serialization.
- No source file in `src/` references `Foundatio.JsonNet`, `JsonNetSerializer`, or uses Newtonsoft types.

### Why They're Still Present

These packages will remain until:
- Foundatio.Repositories removes its Foundatio.JsonNet dependency (tracked upstream)
- Stripe.net drops its Newtonsoft.Json dependency. As of v51.1.0 (latest stable), Stripe.net still depends on Newtonsoft.Json directly across all target frameworks (net6.0, net8.0, net9.0). While Stripe added STJ support, they have not yet removed the Newtonsoft dependency.

Neither impacts our runtime serialization.

---

## Production Safety Guarantees

### Verified Round-Trip Paths

All 1549 tests pass, covering:

1. **Event ingestion → ES → API response** (EventPipelineTests, EventControllerTests)
2. **Error/SimpleError/EnvironmentInfo/RequestInfo extraction** (GetValue with typed deserialization)
3. **WebHook payload generation** (WebHookDataTests with real event fixtures)
4. **Event upgrade V1→V2** (EventUpgraderTests with historical JSON fixtures)
5. **Stack/Organization/Project/Token CRUD** (Repository tests with ES)
6. **Aggregation queries** (AggregationTests)
7. **Session management** (SessionPlugin, CloseInactiveSessionsJob)
8. **Serializer round-trip** (SerializerTests — every model type)

### Data Mutation Audit

Every code path that calls `GetValue<T>()`, mutates the result, and needs to persist the change has been verified to write back:

| Plugin | Mutation | Write-back |
|--------|----------|------------|
| ErrorPlugin | `SetTargetInfo()` | `Data[Error] = error` ✓ |
| SimpleErrorPlugin | `SetTargetInfo()` | `Data[SimpleError] = error` ✓ |
| AngularPlugin | `SetTargetInfo()` | `Data[Error] = error` ✓ |
| RequestInfoPlugin | Strip PII, apply exclusions | `AddRequestInfo(request)` ✓ |
| EnvironmentInfoPlugin | Strip IP/machine name | `SetEnvironmentInfo(env)` ✓ |
| RemovePrivateInformationPlugin | Clear email | `SetUserDescription(desc)` ✓ |

### No Data Loss Scenarios

| Scenario | Safety |
|----------|--------|
| Existing ES documents | Read fine — `PropertyNameCaseInsensitive` handles any key format |
| In-flight queue messages | STJ reads Newtonsoft output (case-insensitive DTOs) |
| Cached values | Ephemeral with TTL, auto-expire |
| WebSocket messages | Consumers expect snake_case (unchanged) |
| Webhook consumers | snake_case output (unchanged from Newtonsoft era) |
| Old client submissions | Event upgrader handles V1/V2 format |
| Custom data keys in Event.Data | Preserved as-is (no DictionaryKeyPolicy in main serializer) |

### Known Limitations

1. **StackRepository `"open"` magic string** — Foundatio's `FieldValueHelper.ToFieldValue` doesn't yet respect `[JsonStringEnumMemberName]`. Uses `"open"` string literal instead of `StackStatus.Open`. Tracked as a TODO; functionally correct.

2. **EventRepository `ElasticFilter`** — One remaining raw ES query for `MustNot ExistsQuery` on dynamic index field (`idx.{SessionEnd}-d`). No typed Foundatio alternative exists for dynamic template fields.

3. **GetValue\<T\> preserves dictionary keys** — `GetValue<T>()` uses `PropertyNamingPolicy = SnakeCaseLower` (for typed property names) but no `DictionaryKeyPolicy`, so user-provided dictionary keys in `Error.Data`, `QueryString`, etc. are preserved exactly as submitted.
