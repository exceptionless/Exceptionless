![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio-dark-bg.svg#gh-dark-mode-only "Foundatio")![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio.svg#gh-light-mode-only "Foundatio")

[![Build status](https://github.com/FoundatioFx/Foundatio.Parsers/workflows/Build/badge.svg)](https://github.com/FoundatioFx/Foundatio.Parsers/actions)
[![NuGet Version](http://img.shields.io/nuget/v/Foundatio.Parsers.LuceneQueries.svg?style=flat)](https://www.nuget.org/packages/Foundatio.Parsers.LuceneQueries/)
[![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Ffoundatio%2Ffoundatio%2Fshield%2FFoundatio.Parsers.LuceneQueries%2Flatest)](https://f.feedz.io/foundatio/foundatio/packages/Foundatio.Parsers.LuceneQueries/latest/download)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)

An extensible Lucene-style query parser with support for Elasticsearch and SQL/Entity Framework Core. Build dynamic search APIs, custom dashboards, and powerful query interfaces.

## Documentation

**[Read the full documentation](https://parsers.foundatio.dev/)**

## Installation

```bash
# Core Lucene parser
dotnet add package Foundatio.Parsers.LuceneQueries

# Elasticsearch integration
dotnet add package Foundatio.Parsers.ElasticQueries

# SQL/EF Core integration
dotnet add package Foundatio.Parsers.SqlQueries
```

## Quick Start

### Parse and Inspect Queries

```csharp
using Foundatio.Parsers.LuceneQueries;
using Foundatio.Parsers.LuceneQueries.Visitors;

var parser = new LuceneQueryParser();
var result = parser.Parse("field:[1 TO 2]");

// Debug the AST structure
Console.WriteLine(DebugQueryVisitor.Run(result));
```

Output from `DebugQueryVisitor`:

```
Group:
  Left - Term:
      TermMax: 2
      TermMin: 1
      MinInclusive: True
      MaxInclusive: True
      Field:
          Name: field
```

Regenerate the original query:

```csharp
string query = GenerateQueryVisitor.Run(result);
// Output: "field:[1 TO 2]"
```

### Build Elasticsearch Queries

```csharp
using Foundatio.Parsers.ElasticQueries;

var parser = new ElasticQueryParser(c => c
    .UseMappings(client, "my-index")
    .UseFieldMap(new Dictionary<string, string> {
        { "user", "data.user.identity" }
    }));

// Build NEST QueryContainer
var query = await parser.BuildQueryAsync("user:john AND status:active");

// Build aggregations
var aggs = await parser.BuildAggregationsAsync("terms:(status min:created max:created)");

// Build sort
var sort = await parser.BuildSortAsync("-created +name");
```

### Build SQL/EF Core Queries

```csharp
using Foundatio.Parsers.SqlQueries;

var parser = new SqlQueryParser(c => c
    .SetDefaultFields(new[] { "Name", "Description" }));

var context = parser.GetContext(db.Products.EntityType);
string dynamicLinq = await parser.ToDynamicLinqAsync("status:active AND price:>100", context);

var results = await db.Products
    .Where(parser.ParsingConfig, dynamicLinq)
    .ToListAsync();
```

## Features

### Query Syntax
- **Term queries**: `field:value`, `field:"quoted phrase"`
- **Range queries**: `field:[1 TO 10]`, `field:>100`, `field:>=2024-01-01`
- **Boolean operators**: `AND`, `OR`, `NOT`, `+`, `-`
- **Wildcards**: `field:val*`, `field:va?ue`
- **Existence**: `_exists_:field`, `_missing_:field`
- **Date math**: `created:[now-7d TO now]`
- **Geo queries**: `location:75044~75mi`

[Full Query Syntax Reference](https://parsers.foundatio.dev/guide/query-syntax)

### Aggregations
- **Metrics**: `min`, `max`, `avg`, `sum`, `stats`, `cardinality`, `percentiles`
- **Buckets**: `terms`, `date`, `histogram`, `geogrid`, `missing`
- **Nested**: `terms:(category min:price max:price)`

[Full Aggregation Syntax Reference](https://parsers.foundatio.dev/guide/aggregation-syntax)

### Field Aliases

Map user-friendly names to actual field paths:

```csharp
var parser = new ElasticQueryParser(c => c
    .UseFieldMap(new Dictionary<string, string> {
        { "user", "data.user.identity" },
        { "created", "metadata.createdAt" }
    }));
```

[Field Aliases Guide](https://parsers.foundatio.dev/guide/field-aliases)

### Query Includes

Define reusable query macros:

```csharp
var parser = new ElasticQueryParser(c => c
    .UseIncludes(new Dictionary<string, string> {
        { "active", "status:active AND deleted:false" },
        { "recent", "created:[now-7d TO now]" }
    }));

// Expands @include:active inline
var query = await parser.BuildQueryAsync("@include:active AND category:electronics");
```

[Query Includes Guide](https://parsers.foundatio.dev/guide/query-includes)

### Validation

Validate and restrict queries:

```csharp
var parser = new ElasticQueryParser(c => c
    .SetValidationOptions(new QueryValidationOptions {
        AllowedFields = { "status", "name", "created" },
        AllowLeadingWildcards = false,
        AllowedMaxNodeDepth = 10
    }));

var result = await parser.ValidateQueryAsync(userQuery);
if (!result.IsValid)
    return BadRequest(result.Message);
```

[Validation Guide](https://parsers.foundatio.dev/guide/validation)

### Visitor Pattern

Extend with custom query transformations:

```csharp
public class CustomVisitor : ChainableQueryVisitor
{
    public override async Task VisitAsync(TermNode node, IQueryVisitorContext context)
    {
        // Custom transformation logic
        await base.VisitAsync(node, context);
    }
}

var parser = new ElasticQueryParser(c => c
    .AddVisitor(new CustomVisitor(), priority: 100));
```

[Visitors Guide](https://parsers.foundatio.dev/guide/visitors)

## Use Cases

- **Dynamic Search APIs** - Let users build complex queries
- **Custom Dashboards** - User-defined aggregations and visualizations
- **Saved Searches** - Store and reuse query fragments
- **Multi-tenant Filtering** - Apply tenant filters transparently
- **Query Translation** - Parse once, output to multiple backends

## Getting Started (Development)

1. Clone the repository
2. Open `Foundatio.Parsers.slnx` in Visual Studio or VS Code
3. Build: `dotnet build`
4. Test: `dotnet test`

## Thanks to all the people who have contributed

[![contributors](https://contributors-img.web.app/image?repo=FoundatioFx/Foundatio.Parsers)](https://github.com/FoundatioFx/Foundatio.Parsers/graphs/contributors)
