![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio-dark-bg.svg#gh-dark-mode-only "Foundatio")![Foundatio](https://raw.githubusercontent.com/FoundatioFx/Foundatio/master/media/foundatio.svg#gh-light-mode-only "Foundatio")

[![Build status](https://github.com/FoundatioFx/Foundatio.Repositories/workflows/Build/badge.svg)](https://github.com/FoundatioFx/Foundatio.Repositories/actions)
[![NuGet Version](http://img.shields.io/nuget/v/Foundatio.Repositories.svg?style=flat)](https://www.nuget.org/packages/Foundatio.Repositories/)
[![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Ffoundatio%2Ffoundatio%2Fshield%2FFoundatio.Repositories%2Flatest)](https://f.feedz.io/foundatio/foundatio/packages/Foundatio.Repositories/latest/download)
[![Discord](https://img.shields.io/discord/715744504891703319)](https://discord.gg/6HxgFCx)

# Foundatio.Repositories

A production-grade repository pattern library for .NET with Elasticsearch implementation. Built on [Foundatio](https://github.com/FoundatioFx/Foundatio) building blocks, it provides a clean abstraction over data access with powerful features like caching, messaging, soft deletes, and versioning.

📖 **[Full Documentation](https://repositories.foundatio.dev)**

## Installation

```bash
dotnet add package Foundatio.Repositories.Elasticsearch
```

## Quick Start

### 1. Define Your Entity

```csharp
using Foundatio.Repositories.Models;

public class Employee : IIdentity, IHaveDates, ISupportSoftDeletes
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public bool IsDeleted { get; set; }
}
```

### 2. Create Index Configuration

```csharp
using Foundatio.Repositories.Elasticsearch.Configuration;

public sealed class EmployeeIndex : VersionedIndex<Employee>
{
    public EmployeeIndex(IElasticConfiguration configuration) 
        : base(configuration, "employees", version: 1) { }

    public override TypeMappingDescriptor<Employee> ConfigureIndexMapping(
        TypeMappingDescriptor<Employee> map)
    {
        return map
            .Dynamic(false)
            .Properties(p => p
                .SetupDefaults()
                .Text(f => f.Name(e => e.Name).AddKeywordAndSortFields())
                .Text(f => f.Name(e => e.Email).AddKeywordAndSortFields())
                .Number(f => f.Name(e => e.Age).Type(NumberType.Integer))
            );
    }
}
```

### 3. Create Repository

```csharp
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch;

public interface IEmployeeRepository : ISearchableRepository<Employee> { }

public class EmployeeRepository : ElasticRepositoryBase<Employee>, IEmployeeRepository
{
    public EmployeeRepository(MyElasticConfiguration configuration) 
        : base(configuration.Employees) { }
}
```

### 4. Use the Repository

```csharp
// Add
var employee = await repository.AddAsync(new Employee 
{ 
    Name = "John Doe", 
    Email = "john@example.com",
    Age = 30 
});

// Query
var results = await repository.FindAsync(q => q
    .FilterExpression("age:>=25")
    .SortExpression("name"));

// Update
employee.Age = 31;
await repository.SaveAsync(employee);

// Soft delete
employee.IsDeleted = true;
await repository.SaveAsync(employee);

// Hard delete
await repository.RemoveAsync(employee);
```

## Features

### Repository Pattern
- **`IReadOnlyRepository<T>`** - Read operations (Get, Find, Count, Exists)
- **`IRepository<T>`** - Write operations (Add, Save, Remove, Patch)
- **`ISearchableRepository<T>`** - Dynamic querying with filters, sorting, and aggregations

### Patch Operations
- **JSON Patch** - RFC 6902 compliant patch operations
- **Partial Patch** - Update specific fields without loading the full document
- **Script Patch** - Elasticsearch Painless scripts for complex updates
- **Bulk Patching** - Apply patches to multiple documents via query

### Caching
- Built-in distributed caching with automatic invalidation
- Real-time cache consistency via message bus
- Configurable cache expiration and custom cache keys

### Message Bus
- Entity change notifications (`EntityChanged` messages)
- Real-time updates for event-driven architectures
- Soft delete transition detection (`ChangeType.Removed`)

### Soft Deletes
- Automatic query filtering based on `IsDeleted`
- Three query modes: `ActiveOnly`, `DeletedOnly`, `All`
- Restore capability for soft-deleted documents

### Document Versioning
- Optimistic concurrency control
- Automatic version conflict detection
- Retry patterns for conflict resolution

### Index Management
- **`Index<T>`** - Basic index configuration
- **`VersionedIndex<T>`** - Schema versioning with migrations
- **`DailyIndex<T>`** - Time-series with daily partitioning
- **`MonthlyIndex<T>`** - Time-series with monthly partitioning

### Event System
- `DocumentsAdding` / `DocumentsAdded`
- `DocumentsSaving` / `DocumentsSaved`
- `DocumentsRemoving` / `DocumentsRemoved`
- `DocumentsChanging` / `DocumentsChanged`
- `BeforeQuery` - Query interception
- `BeforePublishEntityChanged` - Notification interception

### Additional Features
- Document validation with custom validators
- Migrations for data schema evolution
- Jobs for index maintenance, snapshots, and cleanup
- Custom fields for tenant-specific data
- Parent-child document relationships
- Aggregations and analytics

## Documentation

Visit the [full documentation](https://repositories.foundatio.dev) for detailed guides:

- [Getting Started](https://repositories.foundatio.dev/guide/getting-started)
- [Repository Pattern](https://repositories.foundatio.dev/guide/repository-pattern)
- [Elasticsearch Setup](https://repositories.foundatio.dev/guide/elasticsearch-setup)
- [CRUD Operations](https://repositories.foundatio.dev/guide/crud-operations)
- [Querying](https://repositories.foundatio.dev/guide/querying)
- [Configuration](https://repositories.foundatio.dev/guide/configuration)
- [Validation](https://repositories.foundatio.dev/guide/validation)
- [Caching](https://repositories.foundatio.dev/guide/caching)
- [Message Bus](https://repositories.foundatio.dev/guide/message-bus)
- [Patch Operations](https://repositories.foundatio.dev/guide/patch-operations)
- [Soft Deletes](https://repositories.foundatio.dev/guide/soft-deletes)
- [Versioning](https://repositories.foundatio.dev/guide/versioning)
- [Index Management](https://repositories.foundatio.dev/guide/index-management)
- [Migrations](https://repositories.foundatio.dev/guide/migrations)
- [Jobs](https://repositories.foundatio.dev/guide/jobs)
- [Custom Fields](https://repositories.foundatio.dev/guide/custom-fields)

## Sample Application

See the [sample Blazor application](samples/Foundatio.SampleApp) for a complete working example.

## Related Projects

- [Foundatio](https://github.com/FoundatioFx/Foundatio) - Core building blocks (caching, messaging, queues, jobs)
- [Foundatio.Parsers](https://github.com/FoundatioFx/Foundatio.Parsers) - Query parsing for dynamic filtering

## Contributing

We welcome contributions! Please see our [contributing guidelines](CONTRIBUTING.md) for details.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE.txt](LICENSE.txt) for details.
