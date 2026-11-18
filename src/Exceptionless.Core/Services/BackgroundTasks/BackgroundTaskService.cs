using Exceptionless.Core.Configuration;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.DateTimeExtensions;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Utility;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.BackgroundTasks;

/// <summary>
/// Stores background tasks in the cache (with a limited lifetime), keeps a per-organization index and an index of the
/// active tasks, and publishes every change so the progress panel can follow along.
/// </summary>
public class BackgroundTaskService
{
    public static readonly TimeSpan TaskLifetime = TimeSpan.FromDays(7);

    /// <summary>An active task that has not reported progress for this long is considered abandoned.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    /// <summary>How many tasks the list shows, newest first.</summary>
    public const int ListLimit = 50;

    /// <summary>Every cache key and lock resource of background tasks starts with this prefix.</summary>
    public const string KeyPrefix = "background-tasks";

    private const int IndexPageSize = 500;
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;
    private readonly IMessagePublisher _publisher;
    private readonly IQueue<WorkItemData> _workItemQueue;
    private readonly IResiliencePolicy _lockPolicy;
    private readonly BackgroundTaskOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<BackgroundTaskService> _logger;

    public BackgroundTaskService(ICacheClient cache, ILockProvider lockProvider, IMessagePublisher publisher, IQueue<WorkItemData> workItemQueue,
        IResiliencePolicyProvider resiliencePolicyProvider, BackgroundTaskOptions options, TimeProvider timeProvider, ILogger<BackgroundTaskService> logger)
    {
        _cache = cache;
        _lockProvider = lockProvider;
        _publisher = publisher;
        _workItemQueue = workItemQueue;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;

        // Several workers and web instances update the same record, so a busy lock is normal: wait a little, then try again.
        _lockPolicy = resiliencePolicyProvider.GetPolicy<BackgroundTaskService>(b => b.WithMaxAttempts(5).WithLinearDelay(TimeSpan.FromMilliseconds(250)), logger, timeProvider);
    }

    public static string GetTaskCacheKey(string id) => $"{KeyPrefix}:{id}";
    public static string GetPinnedSelectionCacheKey(string id) => $"{GetTaskCacheKey(id)}:selection";
    public static string GetCancelCacheKey(string id) => $"{KeyPrefix}:{id}:cancel";
    public static string GetOrganizationIndexCacheKey(string organizationId) => $"{KeyPrefix}:organization:{organizationId}";
    public static string GetActiveIndexCacheKey(string organizationId) => $"{GetOrganizationIndexCacheKey(organizationId)}:active";
    public static string GetUpdateLockResource(string id) => $"{KeyPrefix}:update:{id}";
    public static string GetCreateLockResource(string organizationId) => $"{KeyPrefix}:create:{organizationId}";

    public async Task<BackgroundTask?> GetAsync(string id)
    {
        if (String.IsNullOrEmpty(id))
            return null;

        var cached = await _cache.GetAsync<BackgroundTask>(GetTaskCacheKey(id));
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>
    /// The newest tasks of the organization that are still stored, newest first, plus every task that is still active.
    /// An older active task stays listed no matter how many newer tasks finished; only the finished history is limited.
    /// </summary>
    public async Task<IReadOnlyList<BackgroundTask>> GetByOrganizationAsync(string organizationId, int limit = ListLimit)
    {
        // Ids are ObjectIds, so descending order is newest first no matter how the cache orders its list.
        var newestIds = (await GetIndexedIdsAsync(GetOrganizationIndexCacheKey(organizationId)))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(id => id, StringComparer.Ordinal)
            .Take(limit)
            .Concat(await GetActiveIdsAsync(organizationId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var tasks = new List<BackgroundTask>();
        var expired = new List<string>();
        var records = await _cache.GetAllAsync<BackgroundTask>(newestIds.Select(GetTaskCacheKey));
        foreach (string id in newestIds)
        {
            if (!records.TryGetValue(GetTaskCacheKey(id), out var record) || !record.HasValue)
                expired.Add(id);
            else if (String.Equals(record.Value.OrganizationId, organizationId, StringComparison.Ordinal))
                tasks.Add(record.Value);
        }

        if (expired.Count > 0)
            await _cache.ListRemoveAsync(GetOrganizationIndexCacheKey(organizationId), expired);

        return tasks.OrderByDescending(t => t.CreatedUtc).ThenByDescending(t => t.Id, StringComparer.Ordinal).ToList();
    }

    public async Task RemoveByOrganizationAsync(string organizationId, CancellationToken cancellationToken)
    {
        string historyKey = GetOrganizationIndexCacheKey(organizationId);
        string activeKey = GetActiveIndexCacheKey(organizationId);
        var ids = await GetIndexedIdsAsync(historyKey);
        ids.AddRange(await GetIndexedIdsAsync(activeKey));
        foreach (string id in ids.Distinct(StringComparer.Ordinal))
        {
            await using var updateLock = await AcquireUpdateLockAsync(id, cancellationToken);
            var task = await GetAsync(id);
            if (task is not null && !String.Equals(task.OrganizationId, organizationId, StringComparison.Ordinal))
                continue;

            await RemoveTaskDataAsync(id);
        }

        await _cache.RemoveAllAsync([historyKey, activeKey]);
    }

    /// <summary>The ids of the organization's tasks that have not finished.</summary>
    public async Task<IReadOnlyList<string>> GetActiveIdsAsync(string organizationId)
    {
        return (await GetIndexedIdsAsync(GetActiveIndexCacheKey(organizationId))).Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>Puts a task that is running again (for example after a queue retry) back into the active index.</summary>
    public Task AddToActiveIndexAsync(string organizationId, string id)
    {
        return _cache.ListAddAsync(GetActiveIndexCacheKey(organizationId), [id], GetIndexLifetime(_timeProvider.GetUtcNow().UtcDateTime));
    }

    /// <summary>Removes finished (or expired) ids from the active index of the organization.</summary>
    public Task RemoveFromActiveIndexAsync(string organizationId, IReadOnlyCollection<string> ids)
    {
        return _cache.ListRemoveAsync(GetActiveIndexCacheKey(organizationId), ids);
    }

    /// <summary>
    /// Gives the task an id, stores it as queued and adds it to the indexes. Nothing is published or queued yet.
    /// </summary>
    public async Task AddAsync(BackgroundTask task, string? pinnedSelection = null)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        task.Id = ObjectId.GenerateNewId(utcNow).ToString();
        task.Status = BackgroundTaskStatus.Queued;
        task.CreatedUtc = utcNow;

        string historyKey = GetOrganizationIndexCacheKey(task.OrganizationId);
        string activeKey = GetActiveIndexCacheKey(task.OrganizationId);
        try
        {
            if (pinnedSelection is not null && !await _cache.SetAsync(GetPinnedSelectionCacheKey(task.Id), pinnedSelection, TaskLifetime))
                throw new InvalidOperationException("The bulk selection could not be saved.");

            await SaveAsync(task);
            var indexLifetime = GetIndexLifetime(task.UpdatedUtc);
            if (await _cache.ListAddAsync(historyKey, [task.Id], indexLifetime) is not 1)
                throw new InvalidOperationException("The background task could not be added to the organization index.");

            if (await _cache.ListAddAsync(activeKey, [task.Id], indexLifetime) is not 1)
                throw new InvalidOperationException("The background task could not be added to the active index.");
        }
        catch
        {
            // No work was enqueued. Remove only this attempt's generated id, even if an index write lost its acknowledgement.
            try
            {
                await Task.WhenAll(
                    RemoveTaskDataAsync(task.Id),
                    _cache.ListRemoveAsync(historyKey, [task.Id]),
                    _cache.ListRemoveAsync(activeKey, [task.Id]));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to clean up unqueued background task {TaskId}: {Message}", task.Id, ex.Message);
            }

            throw;
        }
    }

    /// <summary>
    /// Adds a task and queues the work item that runs it. The work item is created after the task has its id.
    /// </summary>
    public async Task<BackgroundTask> CreateAndEnqueueAsync<T>(BackgroundTask task, Func<BackgroundTask, T> createWorkItem) where T : class
    {
        await AddAsync(task);
        await EnqueueAsync(task, createWorkItem(task));
        return task;
    }

    /// <summary>
    /// Queues the work item of a task that was added and publishes it. When queueing fails the task is failed, because
    /// nothing would ever run it, and the error is rethrown.
    /// </summary>
    public async Task EnqueueAsync<T>(BackgroundTask task, T workItem) where T : class
    {
        try
        {
            await _workItemQueue.EnqueueAsync(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to queue background task {TaskId}: {Message}", task.Id, ex.Message);
            var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
            task.Finish(BackgroundTaskStatus.Failed, "The task could not be started. Please try again.", utcNow);
            await SaveAsync(task);
            await RemoveFromActiveIndexAsync(task.OrganizationId, [task.Id]);
            await PublishAsync(task);
            throw;
        }

        await PublishAsync(task);
    }

    /// <summary>
    /// Queues another run of a work item, e.g. to continue a long task after a temporary error. The queue's retries cover a
    /// whole work item, so continuing in a new work item does not use up the retries of the current one.
    /// </summary>
    public Task ContinueAsync<T>(T workItem) where T : class
    {
        return _workItemQueue.EnqueueAsync(workItem);
    }

    /// <summary>
    /// Loads the task, lets <paramref name="update"/> change it, then saves and publishes it. The change is skipped when
    /// <paramref name="update"/> returns false. Returns the stored task, or null when it no longer exists.
    /// </summary>
    /// <remarks>
    /// Tasks live in the cache rather than in Elasticsearch, so the repository's <c>PatchAsync</c> does not apply. The lock makes the
    /// load, change and save one step; a busy lock is retried with the resilience policy before giving up.
    /// </remarks>
    public async Task<BackgroundTask?> UpdateAsync(string id, Func<BackgroundTask, bool> update)
    {
        await using var updateLock = await AcquireUpdateLockAsync(id, CancellationToken.None);

        var task = await GetAsync(id);
        if (task is null)
            return null;

        if (!update(task))
            return task;

        await SaveAsync(task);
        if (!task.IsActive)
            await RemoveFromActiveIndexAsync(task.OrganizationId, [task.Id]);

        await PublishAsync(task);
        return task;
    }

    /// <summary>
    /// Requests cancellation. A queued (or abandoned) task is cancelled right away; a running bulk task stops at the next
    /// batch boundary, after the batch that is in flight finishes. Tasks that cannot be stopped are rejected while they are
    /// alive, but an abandoned one can always be cleared.
    /// </summary>
    public async Task<BackgroundTaskCancelResult> CancelAsync(string id)
    {
        var task = await GetAsync(id);
        if (task is null)
            return BackgroundTaskCancelResult.NotFound;

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!task.IsActive)
            return BackgroundTaskCancelResult.Cancelled(task);

        bool isAbandoned = IsStale(task, utcNow);
        if (!task.CanCancel && !isAbandoned)
            return BackgroundTaskCancelResult.NotCancellable(task);

        bool requestCancellation = false;
        var updated = await UpdateAsync(id, current =>
        {
            if (!current.IsActive)
                return false;

            // Progress may have resumed after the initial read. Decide eligibility against the locked state.
            var currentUtcNow = _timeProvider.GetUtcNow().UtcDateTime;
            bool isStale = IsStale(current, currentUtcNow);
            if (!current.CanCancel && !isStale)
                return false;

            requestCancellation = true;
            if (current.Status is BackgroundTaskStatus.Running && !isStale)
                return false;

            current.Finish(BackgroundTaskStatus.Cancelled, current.Status is BackgroundTaskStatus.Queued ? "Cancelled before it started." : "Cancelled after it stopped responding.", currentUtcNow);
            return true;
        });

        if (updated is null)
            return BackgroundTaskCancelResult.NotFound;

        if (!requestCancellation)
            return updated.IsActive ? BackgroundTaskCancelResult.NotCancellable(updated) : BackgroundTaskCancelResult.Cancelled(updated);

        // A queued or abandoned task is already stopped. Never undo that state if signaling fails.
        if (!await _cache.SetAsync(GetCancelCacheKey(id), true, TaskLifetime))
            throw new InvalidOperationException("The cancellation request could not be saved.");

        return BackgroundTaskCancelResult.Cancelled(updated);
    }

    public async Task<bool> IsCancelRequestedAsync(string id)
    {
        return (await _cache.GetAsync<bool>(GetCancelCacheKey(id))).HasValue;
    }

    public async Task SaveAsync(BackgroundTask task)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (task.UpdatedUtc != default && task.UpdatedUtc.Date != utcNow.Date)
        {
            await RefreshIndexAsync(GetOrganizationIndexCacheKey(task.OrganizationId), task.Id, utcNow);
            if (task.IsActive)
                await RefreshIndexAsync(GetActiveIndexCacheKey(task.OrganizationId), task.Id, utcNow);
        }

        task.UpdatedUtc = utcNow;
        if (!await _cache.SetAsync(GetTaskCacheKey(task.Id), task, TaskLifetime))
            throw new InvalidOperationException("The background task could not be saved.");
    }

    public Task PublishAsync(BackgroundTask task)
    {
        return PublishAsync(new BackgroundTaskUpdated
        {
            Id = task.Id,
            OrganizationId = task.OrganizationId,
            ProjectId = task.ProjectId,
            Type = task.Type,
            Description = task.Description,
            Status = task.Status,
            Progress = task.Progress,
            Total = task.Total,
            Processed = task.Processed,
            Succeeded = task.Succeeded,
            Failed = task.Failed,
            Message = task.Message,
            CanCancel = task.CanCancel,
            UpdatedUtc = task.UpdatedUtc
        });
    }

    public async Task PublishAsync(BackgroundTaskUpdated message)
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
            _logger.LogWarning(ex, "Failed to publish background task update for {TaskId}: {Message}", message.Id, ex.Message);
        }
    }

    internal static bool IsStale(BackgroundTask task, DateTime utcNow) => task.UpdatedUtc.IsBeforeOrEqual(utcNow - StaleAfter);

    private Task RemoveTaskDataAsync(string id) => _cache.RemoveAllAsync([GetTaskCacheKey(id), GetPinnedSelectionCacheKey(id), GetCancelCacheKey(id)]);

    // Index entries cover every record saved on this UTC day, so progress within the day only rewrites the record.
    private static TimeSpan GetIndexLifetime(DateTime utcNow) => TaskLifetime + (utcNow.Date.AddDays(1) - utcNow);

    private async Task RefreshIndexAsync(string key, string id, DateTime utcNow)
    {
        var lifetime = GetIndexLifetime(utcNow);
        await _cache.ListAddAsync(key, [id], lifetime);
        // Updating an existing Redis sorted-set member refreshes its score, but can leave the set's TTL at the old deadline.
        await _cache.SetExpirationAsync(key, lifetime);
    }

    private async Task<ILock> AcquireUpdateLockAsync(string id, CancellationToken cancellationToken)
    {
        return await _lockPolicy.ExecuteAsync(async ct =>
            await _lockProvider.AcquireAsync(GetUpdateLockResource(id), TimeSpan.FromSeconds(30), _options.LockAcquireTimeout), cancellationToken);
    }

    private async Task<List<string>> GetIndexedIdsAsync(string key)
    {
        var ids = new List<string>();
        for (int page = 1; ; page++)
        {
            var batch = await _cache.GetListAsync<string>(key, page, IndexPageSize);
            if (!batch.HasValue || batch.Value.Count is 0)
                break;

            ids.AddRange(batch.Value);
            if (batch.Value.Count < IndexPageSize)
                break;
        }

        return ids;
    }
}

public record BackgroundTaskCancelResult
{
    public BackgroundTask? Task { get; init; }
    public bool IsNotCancellable { get; init; }

    public static readonly BackgroundTaskCancelResult NotFound = new();
    public static BackgroundTaskCancelResult Cancelled(BackgroundTask task) => new() { Task = task };
    public static BackgroundTaskCancelResult NotCancellable(BackgroundTask task) => new() { Task = task, IsNotCancellable = true };
}
