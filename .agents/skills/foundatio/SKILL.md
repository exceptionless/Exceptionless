---
name: Foundatio
description: |
  Foundatio infrastructure abstractions for caching, queuing, messaging, file storage,
  locking, jobs, and resilience. Use context7 for complete API documentation.
  Keywords: Foundatio, ICacheClient, IQueue, IMessageBus, IFileStorage, ILockProvider,
  IJob, QueueJobBase, resilience, retry, Redis, Elasticsearch
---

# Foundatio

Foundatio provides pluggable infrastructure abstractions. Use context7 MCP for complete documentation.

> **Documentation:** Use `context7` to fetch current Foundatio API docs and examples.

## Core Abstractions

| Interface | Purpose | In-Memory | Production |
| --------- | ------- | --------- | ---------- |
| `ICacheClient` | Distributed caching | `InMemoryCacheClient` | Redis |
| `IQueue<T>` | Message queuing | `InMemoryQueue<T>` | Redis/SQS |
| `IMessageBus` | Pub/sub messaging | `InMemoryMessageBus` | Redis |
| `IFileStorage` | File storage | `InMemoryFileStorage` | S3/Azure |
| `ILockProvider` | Distributed locking | `InMemoryLockProvider` | Redis |
| `IResiliencePolicyProvider` | Retry/circuit breaker | N/A | Polly-based |

## ICacheClient

```csharp
// From src/Exceptionless.Core/Services/UsageService.cs
public class UsageService
{
    private readonly ICacheClient _cache;

    public async Task<int> GetUsageAsync(string organizationId, DateTime bucketUtc)
    {
        var key = GetBucketTotalCacheKey(bucketUtc, organizationId);
        var result = await _cache.GetAsync<int>(key);
        return result.HasValue ? result.Value : 0;
    }

    public async Task IncrementUsageAsync(string organizationId, DateTime bucketUtc, int count)
    {
        var key = GetBucketTotalCacheKey(bucketUtc, organizationId);
        await _cache.IncrementAsync(key, count);

        // Track org in set for later processing
        await _cache.ListAddAsync(GetOrganizationSetKey(bucketUtc), organizationId);
    }
}
```

## IQueue<T>

Queue items for background processing:

```csharp
// Enqueue
await _queue.EnqueueAsync(new EventPost
{
    OrganizationId = orgId,
    ProjectId = projectId,
    FilePath = path
});
```

## IMessageBus

Pub/sub for real-time notifications:

```csharp
// From src/Exceptionless.Web/Hubs/MessageBusBroker.cs
await _subscriber.SubscribeAsync<EntityChanged>(OnEntityChangedAsync, shutdownToken);
await _subscriber.SubscribeAsync<PlanChanged>(OnPlanChangedAsync, shutdownToken);

// Publishing
await _messagePublisher.PublishAsync(new EntityChanged
{
    ChangeType = ChangeType.Saved,
    Type = nameof(Organization),
    Id = organization.Id
});
```

## Jobs

### QueueJobBase - Queue Processing

```csharp
// From src/Exceptionless.Core/Jobs/EventPostsJob.cs
[Job(Description = "Processes queued events.", InitialDelay = "2s")]
public class EventPostsJob : QueueJobBase<EventPost>
{
    public EventPostsJob(
        IQueue<EventPost> queue,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory)
        : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        AutoComplete = false;  // Manual completion after processing
    }

    protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context)
    {
        var entry = context.QueueEntry;
        using var _ = _logger.BeginScope(new ExceptionlessState()
            .Organization(entry.Value.OrganizationId)
            .Project(entry.Value.ProjectId));

        // Process the event...
        await entry.CompleteAsync();
        return JobResult.Success;
    }
}
```

### IJob - Scheduled Jobs

```csharp
// From src/Exceptionless.Core/Jobs/CleanupDataJob.cs
[Job(Description = "Deletes old data.", InitialDelay = "1m", Interval = "1h")]
public class CleanupDataJob : IJob
{
    public async Task<JobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        await CleanupOrganizationsAsync(cancellationToken);
        await CleanupProjectsAsync(cancellationToken);
        return JobResult.Success;
    }
}
```

### Job Attributes

```csharp
[Job(
    Description = "Job description",
    InitialDelay = "2s",           // Delay before first run
    Interval = "5m",               // Run every 5 minutes
    IterationLimit = 1,            // Run once then stop
    IsContinuous = true            // Keep running
)]
```

## Resilience with IResiliencePolicyProvider

`IResiliencePolicyProvider` provides retry policies for Foundatio components:

```csharp
// Registration in Bootstrapper
services.AddSingleton<IResiliencePolicyProvider, ResiliencePolicyProvider>();
```

All queue jobs inherit resilience via base class:

```csharp
// From src/Exceptionless.Core/Jobs/EventPostsJob.cs
public class EventPostsJob : QueueJobBase<EventPost>
{
    public EventPostsJob(
        IQueue<EventPost> queue,
        TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider,
        ILoggerFactory loggerFactory)
        : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        AutoComplete = false;  // Manual completion for control
    }

    protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context)
    {
        var entry = context.QueueEntry;
        try
        {
            // Process...
            await entry.CompleteAsync();
            return JobResult.Success;
        }
        catch (Exception ex) when (ex is ValidationException or MiniValidatorException)
        {
            // Don't retry validation errors
            await entry.CompleteAsync();
            return JobResult.Success;
        }
    }
}
```

Components configured with resilience:

```csharp
// From src/Exceptionless.Core/Bootstrapper.cs
services.AddSingleton<CacheLockProvider>(s => new CacheLockProvider(
    s.GetRequiredService<ICacheClient>(),
    s.GetRequiredService<IMessageBus>(),
    s.GetRequiredService<TimeProvider>(),
    s.GetRequiredService<IResiliencePolicyProvider>(),
    s.GetRequiredService<ILoggerFactory>()
));
```

Queue entries can be retried via `AbandonAsync()` or completed via `CompleteAsync()`.

## Repositories

Foundatio.Repositories provides Elasticsearch integration:

```csharp
// From src/Exceptionless.Core/Repositories/Base/RepositoryBase.cs
public abstract class RepositoryBase<T> : ElasticRepositoryBase<T>
{
    // Automatic change notifications via IMessageBus
    protected override Task PublishChangeTypeMessageAsync(
        ChangeType changeType,
        T? document,
        IDictionary<string, object>? data = null)
    {
        return PublishMessageAsync(CreateEntityChanged(changeType, document));
    }
}
```

Repository options:

```csharp
// Cache results
await _repository.GetByIdAsync(id, o => o.Cache());

// Immediate consistency (for tests)
await _repository.AddAsync(entity, o => o.ImmediateConsistency());
```

## Testing

Use in-memory implementations for tests:

```csharp
services.AddSingleton<ICacheClient, InMemoryCacheClient>();
services.AddSingleton<IMessageBus, InMemoryMessageBus>();
services.AddSingleton(typeof(IQueue<>), typeof(InMemoryQueue<>));
```

See [backend-testing](backend-testing/SKILL.md) for `ProxyTimeProvider` patterns.

## Resilience & Reliability

Build resilient systems that handle failures gracefully:

- **Expect failures**: Network calls fail, resources exhaust, concurrent access races
- **Timeouts everywhere**: Never wait indefinitely; use cancellation tokens
- **Retry with backoff**: Use exponential backoff with jitter for transient failures
- **Graceful degradation**: Return cached data, default values, or partial results when appropriate
- **Idempotency**: Design operations to be safely retryable
- **Resource limits**: Bound queues, caches, and buffers to prevent memory exhaustion

### Retry Pattern

```csharp
// Queue entries support automatic retry
await entry.AbandonAsync();  // Return to queue for retry
await entry.CompleteAsync(); // Mark as successfully processed

// Don't retry validation errors - they'll never succeed
catch (Exception ex) when (ex is ValidationException or MiniValidatorException)
{
    await entry.CompleteAsync();  // Don't retry
    return JobResult.Success;
}
```
