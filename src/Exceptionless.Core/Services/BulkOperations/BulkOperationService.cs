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

    /// <summary>How long to wait before continuing after a temporary error. Multiplied by the number of errors in a row.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
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

    /// <summary>How many operations the list shows, newest first.</summary>
    public const int ListLimit = 50;

    private const int IndexPageSize = 500;
    private const int MaxIndexPages = 100;
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;
    private readonly IMessagePublisher _publisher;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IReadOnlyDictionary<string, IBulkOperationHandler> _handlers;
    private readonly IReadOnlyDictionary<BulkOperationEntity, IBulkSelectionResolver> _resolvers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BulkOperationService> _logger;

    public BulkOperationService(ICacheClient cache, ILockProvider lockProvider, IMessagePublisher publisher, IQueue<WorkItemData> workItemQueue,
        IEnumerable<IBulkOperationHandler> handlers, IEnumerable<IBulkSelectionResolver> resolvers, TimeProvider timeProvider, ILogger<BulkOperationService> logger)
    {
        _cache = cache;
        _lockProvider = lockProvider;
        _publisher = publisher;
        _workItemQueue = workItemQueue;
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.Ordinal);
        _resolvers = resolvers.ToDictionary(r => r.Entity);
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IBulkOperationHandler? GetHandler(string type) => _handlers.GetValueOrDefault(type);

    /// <summary>
    /// Queues another run that continues from the saved cursor. The queue's retries cover a whole work item, so a long operation
    /// continues in a new work item after a temporary error instead of using up the retries of the current one.
    /// </summary>
    public Task ContinueAsync(BulkOperationWorkItem workItem)
    {
        return _workItemQueue.EnqueueAsync(workItem);
    }

    public async Task<BulkOperation?> GetAsync(string id)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var cached = await _cache.GetAsync<BulkOperation>(GetOperationKey(id));
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>
    /// The newest operations of the organization that are still stored, newest first. Every indexed id is read so a busy
    /// organization cannot push recent operations out of the list; only the history that is shown is limited.
    /// </summary>
    public async Task<IReadOnlyList<BulkOperation>> GetByOrganizationAsync(string organizationId, int limit = ListLimit)
    {
        // Ids are ObjectIds, so descending order is newest first no matter how the cache orders its list.
        var newestIds = (await GetIndexedIdsAsync(GetIndexKey(organizationId)))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(id => id, StringComparer.Ordinal)
            .Take(limit);

        var operations = new List<BulkOperation>();
        var expired = new List<string>();
        foreach (string id in newestIds)
        {
            var operation = await GetAsync(id);
            if (operation is null)
                expired.Add(id);
            else if (String.Equals(operation.OrganizationId, organizationId, StringComparison.Ordinal))
                operations.Add(operation);
        }

        if (expired.Count > 0)
            await _cache.ListRemoveAsync(GetIndexKey(organizationId), expired);

        return operations.OrderByDescending(o => o.CreatedUtc).ThenByDescending(o => o.Id, StringComparer.Ordinal).ToList();
    }

    public async Task<string?> GetPinnedSelectionAsync(string id)
    {
        var cached = await _cache.GetAsync<string>(GetPinnedKey(id));
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>
    /// Stores and enqueues the operation unless it conflicts with an active destructive operation on the same project.
    /// The check and the write happen under a per-organization lock so two requests cannot both pass it.
    /// </summary>
    public async Task<BulkOperationCreateResult> CreateAsync(BulkOperation operation)
    {
        var handler = GetHandler(operation.Type) ?? throw new ArgumentException($"Unknown bulk operation type '{operation.Type}'.", nameof(operation));

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        operation.Id = ObjectId.GenerateNewId(utcNow).ToString();
        operation.Status = BulkOperationStatus.Queued;
        operation.CreatedUtc = utcNow;
        operation.UpdatedUtc = utcNow;

        // Count and pin the selection before taking the lock, so a slow search never holds up other requests.
        string? pinnedSelection = null;
        if (operation.Selection.Kind is BulkOperationSelectionKind.Filter)
        {
            if (!_resolvers.TryGetValue(handler.Entity, out var resolver))
                throw new InvalidOperationException($"No selection resolver is registered for {handler.Entity}.");

            var preparation = await resolver.PrepareAsync(operation, CancellationToken.None);
            if (!preparation.Check.IsOk)
                return BulkOperationCreateResult.Rejected(preparation.Check);

            operation.Total = preparation.Total;
            pinnedSelection = preparation.PinnedState;
        }
        else
        {
            operation.Total = operation.Selection.Ids?.Count ?? 0;
        }

        operation.Description = handler.Describe(operation);

        await using var createLock = await _lockProvider.TryAcquireAsync($"bulk-operations:create:{operation.OrganizationId}", TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
        if (createLock is null)
            return BulkOperationCreateResult.Busy();

        if (handler.IsDestructive)
        {
            var conflict = await FindConflictingOperationAsync(operation);
            if (conflict is not null)
                return BulkOperationCreateResult.Conflicting(conflict);
        }

        await SaveAsync(operation);
        if (pinnedSelection is not null)
            await _cache.SetAsync(GetPinnedKey(operation.Id), pinnedSelection, OperationLifetime);

        await _cache.ListAddAsync(GetIndexKey(operation.OrganizationId), [operation.Id], OperationLifetime);
        await _cache.ListAddAsync(GetActiveIndexKey(operation.OrganizationId), [operation.Id], OperationLifetime);
        try
        {
            await _workItemQueue.EnqueueAsync(new BulkOperationWorkItem
            {
                Description = operation.Description,
                OperationId = operation.Id,
                OrganizationId = operation.OrganizationId,
                Type = operation.Type
            });
        }
        catch (Exception ex)
        {
            // Nothing would ever run an operation that was not queued, and it would block new operations on the project.
            _logger.LogError(ex, "Unable to queue bulk operation {OperationId}: {Message}", operation.Id, ex.Message);
            Finish(operation, BulkOperationStatus.Failed, "The operation could not be started. Please try again.");
            await SaveAsync(operation);
            await _cache.ListRemoveAsync(GetActiveIndexKey(operation.OrganizationId), [operation.Id]);
            await PublishAsync(operation);
            throw;
        }

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
            throw new TimeoutException($"Unable to lock bulk operation {id} for update.");

        var operation = await GetAsync(id);
        if (operation is null)
            return null;

        if (!update(operation))
            return operation;

        operation.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await SaveAsync(operation);
        if (!operation.IsActive)
            await _cache.ListRemoveAsync(GetActiveIndexKey(operation.OrganizationId), [operation.Id]);

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
        // Only active operations can conflict, and every one of them is in the active index no matter how much history
        // the organization has.
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var finished = new List<string>();
        BulkOperation? conflict = null;
        foreach (string id in (await GetIndexedIdsAsync(GetActiveIndexKey(candidate.OrganizationId))).Distinct(StringComparer.Ordinal))
        {
            var existing = await GetAsync(id);
            if (existing is null || !existing.IsActive)
            {
                finished.Add(id);
                continue;
            }

            if (utcNow - existing.UpdatedUtc >= StaleAfter || GetHandler(existing.Type) is not { IsDestructive: true })
                continue;

            // An organization wide operation can touch every project, so it conflicts with any project's operation.
            bool overlaps = existing.ProjectId is null || candidate.ProjectId is null || String.Equals(existing.ProjectId, candidate.ProjectId, StringComparison.Ordinal);
            if (overlaps)
                conflict ??= existing;
        }

        if (finished.Count > 0)
            await _cache.ListRemoveAsync(GetActiveIndexKey(candidate.OrganizationId), finished);

        return conflict;
    }

    private async Task<List<string>> GetIndexedIdsAsync(string key)
    {
        var ids = new List<string>();
        for (int page = 1; page <= MaxIndexPages; page++)
        {
            var batch = await _cache.GetListAsync<string>(key, page, IndexPageSize);
            if (!batch.HasValue || batch.Value.Count == 0)
                break;

            ids.AddRange(batch.Value);
            if (batch.Value.Count < IndexPageSize)
                break;
        }

        return ids;
    }

    private Task SaveAsync(BulkOperation operation) => _cache.SetAsync(GetOperationKey(operation.Id), operation, OperationLifetime);

    /// <summary>
    /// Tells clients that an operation can no longer run because its record is gone (for example the cache was cleared).
    /// The work item carries enough to publish a final state so the progress panel does not wait forever.
    /// </summary>
    public Task PublishLostAsync(BulkOperationWorkItem workItem)
    {
        return PublishAsync(new BulkOperationUpdated
        {
            Description = workItem.Description ?? "Bulk operation",
            Id = workItem.OperationId,
            Message = "The operation's progress was lost and it could not continue.",
            OrganizationId = workItem.OrganizationId,
            Status = BulkOperationStatus.Failed,
            Type = workItem.Type ?? String.Empty,
            UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private Task PublishAsync(BulkOperation operation)
    {
        return PublishAsync(new BulkOperationUpdated
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

    private async Task PublishAsync(BulkOperationUpdated message)
    {
        try
        {
            await _publisher.PublishAsync(message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish bulk operation update for {OperationId}: {Message}", message.Id, ex.Message);
        }
    }

    private static string GetOperationKey(string id) => $"bulk-operations:{id}";
    private static string GetCancelKey(string id) => $"bulk-operations:{id}:cancel";
    private static string GetIndexKey(string organizationId) => $"bulk-operations:organization:{organizationId}";
    private static string GetActiveIndexKey(string organizationId) => $"bulk-operations:organization:{organizationId}:active";
    private static string GetPinnedKey(string id) => $"bulk-operations:{id}:selection";
}

public record BulkOperationCreateResult
{
    public BulkOperation? Operation { get; init; }
    public BulkOperation? Conflict { get; init; }
    public bool IsBusy { get; init; }
    public BulkOperationCheckResult? Rejection { get; init; }

    public static BulkOperationCreateResult Created(BulkOperation operation) => new() { Operation = operation };
    public static BulkOperationCreateResult Conflicting(BulkOperation conflict) => new() { Conflict = conflict };
    public static BulkOperationCreateResult Busy() => new() { IsBusy = true };
    public static BulkOperationCreateResult Rejected(BulkOperationCheckResult rejection) => new() { Rejection = rejection };
}
