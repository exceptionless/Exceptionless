---
name: foundatio-repositories
description: >
    Use this skill when querying, counting, patching, or paginating data through Foundatio.Repositories
    Elasticsearch abstractions. Covers filter expressions, aggregation queries, partial and script
    patches, and search-after pagination. Apply when working with any repository method — never use
    raw IElasticClient directly.
---

# Foundatio Repositories

Foundatio.Repositories provides a high-level Elasticsearch abstraction. **Never use raw `IElasticClient` directly** — always use repository methods.

> **Documentation:** <https://repositories.foundatio.dev> / <https://parsers.foundatio.dev>

## Repository Hierarchy

```text
IRepository<T>                             — CRUD, Patch, Remove
  └─ ISearchableRepository<T>              — FindAsync, CountAsync, aggregations
       └─ IRepositoryOwnedByOrganization<T>
            └─ IRepositoryOwnedByProject<T>
                 └─ IRepositoryOwnedByOrganizationAndProject<T>
```

Exceptionless repos:

| Interface                   | Entity            | Index Type                      |
| --------------------------- | ----------------- | ------------------------------- |
| `IEventRepository`          | `PersistentEvent` | `DailyIndex` (date-partitioned) |
| `IStackRepository`          | `Stack`           | `VersionedIndex` (single index) |
| `IProjectRepository`        | `Project`         | `VersionedIndex`                |
| `IOrganizationRepository`   | `Organization`    | `VersionedIndex`                |
| `IUserRepository`           | `User`            | `VersionedIndex`                |
| `ITokenRepository`          | `Token`           | `VersionedIndex`                |
| `IMigrationStateRepository` | `MigrationState`  | `VersionedIndex`                |

**Important:** `.Index(start, end)` only routes to correct daily shards for `DailyIndex` (events). It is a no-op for `VersionedIndex` (stacks, orgs, projects).

## CountAsync + AggregationsExpression

`CountAsync` returns a `CountResult` with `.Total` (long) and `.Aggregations` (AggregationsHelper).

### AggregationsExpression DSL

| Expression                     | Meaning                                  |
| ------------------------------ | ---------------------------------------- |
| `cardinality:field`            | Distinct count                           |
| `terms:field`                  | Terms aggregation                        |
| `terms:(field~SIZE)`           | Terms with bucket size limit             |
| `terms:(field~SIZE sub_agg)`   | Terms with nested aggregation            |
| `terms:(field @include:VALUE)` | Terms with include filter                |
| `date:field`                   | Date histogram (auto interval)           |
| `date:field~1d`                | Date histogram, daily interval           |
| `date:field~1M`                | Date histogram, monthly interval         |
| `date:(field sub_agg)`         | Date histogram with nested agg           |
| `sum:field~DEFAULT`            | Sum with default value                   |
| `min:field` / `max:field`      | Min/Max aggregation                      |
| `avg:field`                    | Average aggregation                      |
| `-sum:field~1`                 | Sort descending by this agg (prefix `-`) |

Multiple aggregations are space-separated: `"cardinality:stack_id terms:type sum:count~1"`

### Accessing Aggregation Results

Naming convention: `{type}_{field}` — the aggregation type prefix + underscore + field name.

```csharp
// Cardinality
result.Aggregations.Cardinality("cardinality_stack_id").Value

// Terms
result.Aggregations.Terms<string>("terms_type").Buckets  // .Key, .Total

// Date histogram
result.Aggregations.DateHistogram("date_date").Buckets   // .Date, .Total

// Sum / Min / Max / Avg
result.Aggregations.Sum("sum_count").Value
result.Aggregations.Min<DateTime>("min_date").Value
result.Aggregations.Max<DateTime>("max_date").Value
result.Aggregations.Average("avg_value").Value

// Nested aggs inside buckets
var terms = result.Aggregations.Terms<string>("terms_stack_id");
foreach (var bucket in terms.Buckets)
{
    var nested = bucket.Aggregations.Cardinality("cardinality_user").Value;
}
```

### Examples

**Simple cardinality:**

```csharp
var result = await _eventRepository.CountAsync(q => q
    .FilterExpression($"project:{projectId}")
    .AggregationsExpression("cardinality:stack_id cardinality:id"));
long uniqueStacks = result.Aggregations.Cardinality("cardinality_stack_id").Value.GetValueOrDefault();
```

**Date histogram + nested cardinality:**

```csharp
var result = await _eventRepository.CountAsync(q => q
    .FilterExpression($"project:{projectId}")
    .AggregationsExpression("date:(date cardinality:id) cardinality:id"));
var buckets = result.Aggregations.DateHistogram("date_date").Buckets;
```

**Date histogram with monthly interval:**

```csharp
var result = await _eventRepository.CountAsync(q => q
    .Organization(organizationId)
    .AggregationsExpression("date:date~1M"));
foreach (var bucket in result.Aggregations.DateHistogram("date_date").Buckets)
{
    // bucket.Date, bucket.Total
}
```

**Terms with nested min/max:**

```csharp
var result = await _eventRepository.CountAsync(q => q
    .AggregationsExpression($"terms:(stack_id~{stackSize} min:date max:date)"));
var buckets = result.Aggregations.Terms<string>("terms_stack_id").Buckets;
foreach (var b in buckets)
{
    DateTime first = b.Aggregations.Min<DateTime>("min_date").Value;
    DateTime last = b.Aggregations.Max<DateTime>("max_date").Value;
}
```

**Complex multi-aggregation (DailySummaryJob):**

```csharp
var result = await _eventRepository.CountAsync(q => q
    .SystemFilter(systemFilter)
    .FilterExpression(filter)
    .EnforceEventStackFilter()
    .AggregationsExpression("terms:(first @include:true) terms:(stack_id~3) cardinality:stack_id sum:count~1"));
double total = result.Aggregations.Sum("sum_count")?.Value ?? result.Total;
double uniqueTotal = result.Aggregations.Cardinality("cardinality_stack_id")?.Value ?? 0;
```

**Stack mode aggregations with sort prefix:**

```csharp
string aggs = mode switch
{
    "stack_recent"   => "cardinality:user sum:count~1 min:date -max:date",
    "stack_frequent" => "cardinality:user -sum:count~1 min:date max:date",
    _ => null
};
var result = await _repository.CountAsync(q => q
    .SystemFilter(systemFilter)
    .FilterExpression(filter)
    .EnforceEventStackFilter()
    .AggregationsExpression($"terms:(stack_id~{limit} {aggs})"));
```

## FilterExpression (Lucene-style)

FilterExpression accepts Lucene query syntax parsed by Foundatio Parsers:

```csharp
.FilterExpression("type:error (status:open OR status:regressed)")
.FilterExpression($"project:{projectId}")
.FilterExpression($"stack:{stackId}")
.FilterExpression("status:open OR status:regressed")
.FilterExpression($"signature_hash:{signature}")
.FilterExpression("is_deleted:false")
```

**Building OR filters from collections:**

```csharp
string filter = String.Join(" OR ", stackIds.Select(id => $"stack:{id}"));
```

## Query Extension Methods

Custom extensions on `IRepositoryQuery<T>`:

| Method                          | Purpose                             | File                     |
| ------------------------------- | ----------------------------------- | ------------------------ |
| `.Organization(id)`             | Filter by organization_id           | OrganizationQuery.cs     |
| `.Organization(ids)`            | Filter by multiple org IDs          | OrganizationQuery.cs     |
| `.Project(id)`                  | Filter by project_id                | ProjectQuery.cs          |
| `.Stack(id)` / `.Stack(ids)`    | Filter by stack_id                  | StackQuery.cs            |
| `.ExcludeStack(id)`             | Exclude stack_id                    | StackQuery.cs            |
| `.AppFilter(sf)`                | Apply app-level system filter       | AppFilterQuery.cs        |
| `.SystemFilter(query)`          | Chain a pre-built query             | Foundatio built-in       |
| `.EnforceEventStackFilter()`    | Resolve stack filters to event IDs  | EventStackFilterQuery.cs |
| `.DateRange(start, end, field)` | Date range filter                   | Foundatio built-in       |
| `.Index(start, end)`            | Route to daily shards (events only) | Foundatio built-in       |
| `.FieldEquals(expr, value)`     | Exact field match                   | Foundatio built-in       |
| `.SortExpression(sort)`         | Sort expression                     | Foundatio built-in       |

## Pagination

### Standard do/while Pattern (preferred)

```csharp
var results = await _repository.GetAllAsync(o => o.SearchAfterPaging().PageLimit(500));
do
{
    foreach (var doc in results.Documents)
    {
        // process document
    }
} while (!cancellationToken.IsCancellationRequested && await results.NextPageAsync());
```

### While-loop Pattern (when processing before checking)

```csharp
var results = await _repository.GetAllAsync(o => o.SearchAfterPaging().PageLimit(5));
while (results.Documents.Count > 0 && !cancellationToken.IsCancellationRequested)
{
    foreach (var doc in results.Documents)
    {
        // process document
    }

    if (cancellationToken.IsCancellationRequested || !await results.NextPageAsync())
        break;
}
```

**Key rules:**

- `NextPageAsync()` returns `Task<bool>` and mutates results in-place
- **Never** use `while(true) { ... break; }` — use `do/while` or `while(condition)`
- Always use `SearchAfterPaging()` for deep pagination (not offset-based)
- Always check `CancellationToken` in the loop condition

### Collecting All IDs

```csharp
var results = await _stackRepository.GetIdsByQueryAsync(
    q => systemFilterQuery.As<Stack>(),
    o => o.PageLimit(10000).SearchAfterPaging());

var stackIds = new List<string>();
if (results?.Hits is not null)
{
    do
    {
        stackIds.AddRange(results.Hits.Select(h => h.Id));
    } while (await results.NextPageAsync());
}
```

## PatchAllAsync / PatchAsync

### PartialPatch (field-level update)

```csharp
// Suspend all tokens for an org
await _tokenRepository.PatchAllAsync(
    q => q.Organization(orgId).FieldEquals(t => t.IsSuspended, false),
    new PartialPatch(new { is_suspended = true }),
    o => o.ImmediateConsistency());
```

### ScriptPatch (Painless script)

```csharp
const string script = @"
ctx._source.total_occurrences += params.count;
ctx._source.last_occurrence = params.maxOccurrenceDateUtc;";

var patch = new ScriptPatch(script.TrimScript())
{
    Params = new Dictionary<string, object>
    {
        { "count", count },
        { "maxOccurrenceDateUtc", maxDate }
    }
};

await _stackRepository.PatchAsync(stackId, patch, o => o.Notifications(false));
```

### PatchAsync with Array of IDs

```csharp
string script = $"ctx._source.next_summary_end_of_day_ticks += {TimeSpan.TicksPerDay}L;";
await PatchAsync(projects.Select(p => p.Id).ToArray(), new ScriptPatch(script), o => o.Notifications(false));
```

### Soft Delete

```csharp
await PatchAllAsync(
    q => q.Organization(orgId).Project(projectId),
    new PartialPatch(new { is_deleted = true, updated_utc = _timeProvider.GetUtcNow().UtcDateTime }));
```

## RemoveAllAsync

```csharp
// By organization + date range
await _eventRepository.RemoveAllAsync(organizationId, clientIpAddress, utcStart, utcEnd);

// By stack IDs
await _eventRepository.RemoveAllByStackIdsAsync(stackIds);

// With inline filter
await _repository.RemoveAllAsync(q => q.Organization(orgId));
```

## GetByIdsAsync (Batch Existence Checks)

```csharp
// Batch fetch — returns only found documents
var stacks = await _stackRepository.GetByIdsAsync(stackIds);

// With cache
var users = await _userRepository.GetByIdsAsync(userIds, o => o.Cache());

// Check existence by comparing returned set
var found = (await _organizationRepository.GetByIdsAsync(orgIds)).Select(o => o.Id).ToHashSet();
var missing = orgIds.Where(id => !found.Contains(id)).ToArray();
```

## CommandOptions

| Option                          | Purpose                                      |
| ------------------------------- | -------------------------------------------- |
| `o => o.Cache()`                | Enable cache read/write                      |
| `o => o.Cache("key")`           | Cache with specific key                      |
| `o => o.ReadCache()`            | Only read from cache                         |
| `o => o.ImmediateConsistency()` | ES refresh after write (tests)               |
| `o => o.SearchAfterPaging()`    | Deep pagination with search_after            |
| `o => o.PageLimit(N)`           | Page size                                    |
| `o => o.PageNumber(N)`          | Page number (use SearchAfterPaging for deep) |
| `o => o.SoftDeleteMode(mode)`   | `All`, `ActiveOnly`, `DeletedOnly`           |
| `o => o.Notifications(false)`   | Suppress change notifications                |
| `o => o.Originals()`            | Track original values for change detection   |
| `o => o.OnlyIds()`              | Return only IDs (no source)                  |

## Anti-Patterns

**NEVER do these:**

- Use `_elasticClient.SearchAsync<T>(...)` — use `CountAsync` or `FindAsync`
- Use `_elasticClient.MultiGetAsync(...)` — use `GetByIdsAsync`
- Use `_elasticClient.DeleteByQueryAsync<T>(...)` — use `RemoveAllAsync`
- Use `_elasticClient.UpdateByQueryAsync<T>(...)` — use `PatchAllAsync`
- Use `_elasticClient.Indices.RefreshAsync(...)` — use `o => o.ImmediateConsistency()`
- Use `while(true) { ... break; }` for pagination — use `do/while` or `while(condition)`
