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

| Interface                   | Entity            | Index Type                      |
| --------------------------- | ----------------- | ------------------------------- |
| `IEventRepository`          | `PersistentEvent` | `DailyIndex` (date-partitioned) |
| `IStackRepository`          | `Stack`           | `VersionedIndex` (single index) |
| `IProjectRepository`        | `Project`         | `VersionedIndex`                |
| `IOrganizationRepository`   | `Organization`    | `VersionedIndex`                |
| `IUserRepository`           | `User`            | `VersionedIndex`                |
| `ITokenRepository`          | `Token`           | `VersionedIndex`                |
| `IMigrationStateRepository` | `MigrationState`  | `VersionedIndex`                |

**Important:** `.Index(start, end)` only routes to correct daily shards for `DailyIndex` (events). It is a no-op for `VersionedIndex`.

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
result.Aggregations.Cardinality("cardinality_stack_id").Value
result.Aggregations.Terms<string>("terms_type").Buckets       // .Key, .Total
result.Aggregations.DateHistogram("date_date").Buckets        // .Date, .Total
result.Aggregations.Sum("sum_count").Value
result.Aggregations.Min<DateTime>("min_date").Value
result.Aggregations.Max<DateTime>("max_date").Value
result.Aggregations.Average("avg_value").Value

// Nested aggs inside buckets
foreach (var bucket in result.Aggregations.Terms<string>("terms_stack_id").Buckets)
    bucket.Aggregations.Cardinality("cardinality_user").Value;
```

## FilterExpression (Lucene-style)

FilterExpression accepts Lucene query syntax parsed by Foundatio Parsers:

```csharp
.FilterExpression("type:error (status:open OR status:regressed)")
.FilterExpression($"project:{projectId}")
.FilterExpression($"stack:{stackId}")
.FilterExpression($"signature_hash:{signature}")
.FilterExpression("is_deleted:false")
```

**Building OR filters from collections:**

```csharp
string filter = String.Join(" OR ", stackIds.Select(id => $"stack:{id}"));
```

## Query Extension Methods

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

Use `SearchAfterPaging()` for deep pagination (never offset-based). `NextPageAsync()` returns `Task<bool>` and mutates results in-place.

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

**Key rules:**
- **Never** use `while(true) { ... break; }` — use `do/while` or `while(condition)`
- Always check `CancellationToken` in the loop condition

## PatchAllAsync / PatchAsync

Use `PartialPatch` for field-level updates, `ScriptPatch` for Painless scripts. Pass `o => o.ImmediateConsistency()` when write-then-read consistency is needed (tests). Use `o => o.Notifications(false)` to suppress change notifications.

```csharp
await _tokenRepository.PatchAllAsync(
    q => q.Organization(orgId).FieldEquals(t => t.IsSuspended, false),
    new PartialPatch(new { is_suspended = true }),
    o => o.ImmediateConsistency());
```

## Anti-Patterns

**NEVER do these:**

- Use `_elasticClient.SearchAsync<T>(...)` — use `CountAsync` or `FindAsync`
- Use `_elasticClient.MultiGetAsync(...)` — use `GetByIdsAsync`
- Use `_elasticClient.DeleteByQueryAsync<T>(...)` — use `RemoveAllAsync`
- Use `_elasticClient.UpdateByQueryAsync<T>(...)` — use `PatchAllAsync`
- Use `_elasticClient.Indices.RefreshAsync(...)` — use `o => o.ImmediateConsistency()`
- Use `while(true) { ... break; }` for pagination — use `do/while` or `while(condition)`
