using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Utility;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.BulkOperations;

public class BulkOperationOptions
{
    /// <summary>How many ids are resolved and executed per batch.</summary>
    public int BatchSize { get; set; } = 500;
}

/// <summary>
/// Stores bulk operations in the cache (with a limited lifetime), keeps a per-organization index of operation ids,
/// enforces the one-active-destructive-operation-per-project rule, and publishes every change.
/// </summary>
public class BulkOperationService
{
    public static readonly TimeSpan OperationLifetime = TimeSpan.FromDays(7);

    /// <summary>An active operation that has not reported progress for this long is considered abandoned.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    private const int MaxIndexedOperations = 500;
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;
    private readonly IMessagePublisher _publisher;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IReadOnlyDictionary<string, IBulkOperationHandler> _handlers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BulkOperationService> _logger;

    public BulkOperationService(ICacheClient cache, ILockProvider lockProvider, IMessagePublisher publisher, IQueue<WorkItemData> workItemQueue,
        IEnumerable<IBulkOperationHandler> handlers, TimeProvider timeProvider, ILogger<BulkOperationService> logger)
    {
        _cache = cache;
        _lockProvider = lockProvider;
        _publisher = publisher;
        _workItemQueue = workItemQueue;
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.Ordinal);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IBulkOperationHandler? GetHandler(string type) => _handlers.GetValueOrDefault(type);

    public async Task<BulkOperation?> GetAsync(string id)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var cached = await _cache.GetAsync<BulkOperation>(GetOperationKey(id));
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>All operations of the organization that are still stored, newest first.</summary>
    public async Task<IReadOnlyList<BulkOperation>> GetByOrganizationAsync(string organizationId)
    {
        var ids = new List<string>();
        for (int page = 1; page <= MaxIndexedOperations / 100; page++)
        {
            var batch = await _cache.GetListAsync<string>(GetIndexKey(organizationId), page, 100);
            if (!batch.HasValue || batch.Value.Count == 0)
                break;

            ids.AddRange(batch.Value);
            if (batch.Value.Count < 100)
                break;
        }

        var operations = new List<BulkOperation>();
        var expired = new List<string>();
        foreach (string id in ids.Distinct(StringComparer.Ordinal))
        {
            var operation = await GetAsync(id);
            if (operation is null)
                expired.Add(id);
            else if (String.Equals(operation.OrganizationId, organizationId, StringComparison.Ordinal))
                operations.Add(operation);
        }

        if (expired.Count > 0)
            await _cache.ListRemoveAsync(GetIndexKey(organizationId), expired);

        return operations.OrderByDescending(o => o.CreatedUtc).ToList();
    }

    /// <summary>
    /// Stores and enqueues the operation unless it conflicts with an active destructive operation on the same project.
    /// The check and the write happen under a per-organization lock so two requests cannot both pass it.
    /// </summary>
    public async Task<BulkOperationCreateResult> CreateAsync(BulkOperation operation)
    {
        var handler = GetHandler(operation.Type) ?? throw new ArgumentException($"Unknown bulk operation type '{operation.Type}'.", nameof(operation));

        await using var createLock = await _lockProvider.TryAcquireAsync($"bulk-operations:create:{operation.OrganizationId}", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
        if (createLock is null)
            return BulkOperationCreateResult.Busy();

        if (handler.IsDestructive)
        {
            var conflict = await FindConflictingOperationAsync(operation);
            if (conflict is not null)
                return BulkOperationCreateResult.Conflicting(conflict);
        }

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        operation.Id = ObjectId.GenerateNewId(utcNow).ToString();
        operation.Status = BulkOperationStatus.Queued;
        operation.CreatedUtc = utcNow;
        operation.UpdatedUtc = utcNow;

        await SaveAsync(operation);
        await _cache.ListAddAsync(GetIndexKey(operation.OrganizationId), [operation.Id], OperationLifetime);
        await _workItemQueue.EnqueueAsync(new BulkOperationWorkItem { OperationId = operation.Id });
        await PublishAsync(operation);

        return BulkOperationCreateResult.Created(operation);
    }

    /// <summary>
    /// Loads the operation, lets <paramref name="update"/> change it, then saves and publishes it. The change is skipped
    /// when <paramref name="update"/> returns false. Returns the stored operation, or null when it no longer exists.
    /// </summary>
    public async Task<BulkOperation?> UpdateAsync(string id, Func<BulkOperation, bool> update)
    {
        await using var updateLock = await _lockProvider.TryAcquireAsync($"bulk-operations:update:{id}", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
        if (updateLock is null)
            throw new InvalidOperationException($"Unable to lock bulk operation {id} for update.");

        var operation = await GetAsync(id);
        if (operation is null)
            return null;

        if (!update(operation))
            return operation;

        operation.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await SaveAsync(operation);
        await PublishAsync(operation);
        return operation;
    }

    /// <summary>
    /// Requests cancellation. A queued (or abandoned) operation is cancelled right away; a running one stops at the
    /// next batch boundary, after the batch that is in flight finishes.
    /// </summary>
    public async Task<BulkOperation?> CancelAsync(string id)
    {
        var operation = await GetAsync(id);
        if (operation is null || !operation.IsActive)
            return operation;

        await _cache.SetAsync(GetCancelKey(id), true, OperationLifetime);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        return await UpdateAsync(id, current =>
        {
            if (!current.IsActive)
                return false;

            bool isRunningNow = current.Status is BulkOperationStatus.Running && utcNow - current.UpdatedUtc < StaleAfter;
            if (isRunningNow)
                return false;

            Finish(current, BulkOperationStatus.Cancelled, "Cancelled before it started.", utcNow);
            return true;
        });
    }

    public async Task<bool> IsCancelRequestedAsync(string id)
    {
        return (await _cache.GetAsync<bool>(GetCancelKey(id))).HasValue;
    }

    public void Finish(BulkOperation operation, BulkOperationStatus status, string? message, DateTime? utcNow = null)
    {
        operation.Status = status;
        operation.Message = message;
        operation.CompletedUtc = utcNow ?? _timeProvider.GetUtcNow().UtcDateTime;
    }

    private async Task<BulkOperation?> FindConflictingOperationAsync(BulkOperation candidate)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var existing in await GetByOrganizationAsync(candidate.OrganizationId))
        {
            if (!existing.IsActive || utcNow - existing.UpdatedUtc >= StaleAfter)
                continue;

            if (GetHandler(existing.Type) is not { IsDestructive: true })
                continue;

            // An organization wide operation can touch every project, so it conflicts with any project's operation.
            bool overlaps = existing.ProjectId is null || candidate.ProjectId is null || String.Equals(existing.ProjectId, candidate.ProjectId, StringComparison.Ordinal);
            if (overlaps)
                return existing;
        }

        return null;
    }

    private Task SaveAsync(BulkOperation operation) => _cache.SetAsync(GetOperationKey(operation.Id), operation, OperationLifetime);

    private async Task PublishAsync(BulkOperation operation)
    {
        try
        {
            await _publisher.PublishAsync(new BulkOperationUpdated
            {
                Id = operation.Id,
                OrganizationId = operation.OrganizationId,
                ProjectId = operation.ProjectId,
                Type = operation.Type,
                Description = operation.Description,
                Status = operation.Status,
                Total = operation.Total,
                Processed = operation.Processed,
                Succeeded = operation.Succeeded,
                Failed = operation.Failed,
                Message = operation.Message,
                UpdatedUtc = operation.UpdatedUtc
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish bulk operation update for {OperationId}: {Message}", operation.Id, ex.Message);
        }
    }

    private static string GetOperationKey(string id) => $"bulk-operations:{id}";
    private static string GetCancelKey(string id) => $"bulk-operations:{id}:cancel";
    private static string GetIndexKey(string organizationId) => $"bulk-operations:organization:{organizationId}";
}

public record BulkOperationCreateResult
{
    public BulkOperation? Operation { get; init; }
    public BulkOperation? Conflict { get; init; }
    public bool IsBusy { get; init; }

    public static BulkOperationCreateResult Created(BulkOperation operation) => new() { Operation = operation };
    public static BulkOperationCreateResult Conflicting(BulkOperation conflict) => new() { Conflict = conflict };
    public static BulkOperationCreateResult Busy() => new() { IsBusy = true };
}
