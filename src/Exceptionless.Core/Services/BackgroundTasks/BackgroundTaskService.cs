using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Messaging;
using Foundatio.Queues;
using Foundatio.Repositories.Utility;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services.BackgroundTasks;

public class BackgroundTaskOptions
{
    /// <summary>How long one attempt to lock a task waits. A busy lock is retried by the resilience policy.</summary>
    public TimeSpan LockAcquireTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Stores background tasks in the cache (with a limited lifetime), keeps a per-organization index and an index of the
/// active tasks, and publishes every change so the progress panel can follow along.
/// </summary>
public class BackgroundTaskService
{
    public static readonly TimeSpan TaskLifetime = TimeSpan.FromDays(7);

    /// <summary>An active task that has not reported progress for this long is considered abandoned.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(2);

    /// <summary>How many finished tasks the list shows, in addition to all active tasks.</summary>
    public const int ListLimit = 50;

    /// <summary>Every cache key and lock resource of background tasks starts with this prefix.</summary>
    public const string KeyPrefix = "background-tasks";

    private const int IndexPageSize = 500;
    private const int MaxIndexPages = 100;
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
    public static string GetCancelCacheKey(string id) => $"{KeyPrefix}:{id}:cancel";
    public static string GetOrganizationIndexCacheKey(string organizationId) => $"{KeyPrefix}:organization:{organizationId}";
    public static string GetActiveIndexCacheKey(string organizationId) => $"{KeyPrefix}:organization:{organizationId}:active";
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
    /// All active tasks and the newest finished tasks of the organization, newest first. Only finished history is limited.
    /// </summary>
    public async Task<IReadOnlyList<BackgroundTask>> GetByOrganizationAsync(string organizationId, int limit = ListLimit)
    {
        var activeIds = (await GetActiveIdsAsync(organizationId)).ToHashSet(StringComparer.Ordinal);
        // Ids are ObjectIds, so descending order is newest first no matter how the cache orders its list.
        var newestIds = (await GetIndexedIdsAsync(GetOrganizationIndexCacheKey(organizationId)))
            .Concat(activeIds)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(id => id, StringComparer.Ordinal);

        var tasks = new List<BackgroundTask>();
        var expired = new List<string>();
        int finishedCount = 0;
        foreach (string id in newestIds)
        {
            if (finishedCount >= limit && !activeIds.Contains(id))
                continue;

            var task = await GetAsync(id);
            if (task is null)
                expired.Add(id);
            else if (String.Equals(task.OrganizationId, organizationId, StringComparison.Ordinal) && (task.IsActive || finishedCount < limit))
            {
                tasks.Add(task);
                if (!task.IsActive)
                    finishedCount++;
            }
        }

        if (expired.Count > 0)
            await _cache.ListRemoveAsync(GetOrganizationIndexCacheKey(organizationId), expired);

        return tasks.OrderByDescending(t => t.CreatedUtc).ThenByDescending(t => t.Id, StringComparer.Ordinal).ToList();
    }

    /// <summary>The ids of the organization's tasks that have not finished.</summary>
    public async Task<IReadOnlyList<string>> GetActiveIdsAsync(string organizationId)
    {
        // Completion removes ids concurrently. Read one snapshot so a removal cannot shift an unread id past a page offset.
        var ids = await _cache.GetListAsync<string>(GetActiveIndexCacheKey(organizationId));
        return ids.HasValue ? ids.Value.Distinct(StringComparer.Ordinal).ToList() : [];
    }

    /// <summary>Puts a task that is running again (for example after a queue retry) back into the active index.</summary>
    public Task AddToActiveIndexAsync(string organizationId, string id)
    {
        return _cache.ListAddAsync(GetActiveIndexCacheKey(organizationId), [id], TaskLifetime);
    }

    /// <summary>Removes finished (or expired) ids from the active index of the organization.</summary>
    public Task RemoveFromActiveIndexAsync(string organizationId, IReadOnlyCollection<string> ids)
    {
        return _cache.ListRemoveAsync(GetActiveIndexCacheKey(organizationId), ids);
    }

    /// <summary>
    /// Gives the task an id, stores it as queued and adds it to the indexes. Nothing is published or queued yet.
    /// </summary>
    public async Task AddAsync(BackgroundTask task)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        task.Id = ObjectId.GenerateNewId(utcNow).ToString();
        task.Status = BackgroundTaskStatus.Queued;
        task.CreatedUtc = utcNow;
        task.UpdatedUtc = utcNow;

        await SaveAsync(task);
        try
        {
            if (await _cache.ListAddAsync(GetOrganizationIndexCacheKey(task.OrganizationId), [task.Id], TaskLifetime) != 1)
                throw new InvalidOperationException("The background task could not be added to the organization index.");

            if (await _cache.ListAddAsync(GetActiveIndexCacheKey(task.OrganizationId), [task.Id], TaskLifetime) != 1)
                throw new InvalidOperationException("The background task could not be added to the active index.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to index background task {TaskId}: {Message}", task.Id, ex.Message);
            await FailToStartAsync(task.Id);
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
    /// Prepares and queues the work item of a task that was added and publishes it. When preparation or queueing fails,
    /// a task that is still queued is failed and the error is rethrown. Worker progress and cancellation are preserved.
    /// </summary>
    public async Task EnqueueAsync<T>(BackgroundTask task, T workItem, Func<Task>? beforeEnqueue = null) where T : class
    {
        try
        {
            if (beforeEnqueue is not null)
                await beforeEnqueue();

            await _workItemQueue.EnqueueAsync(workItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to queue background task {TaskId}: {Message}", task.Id, ex.Message);
            await FailToStartAsync(task.Id);
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
        await using var updateLock = await _lockPolicy.ExecuteAsync(async ct =>
            await _lockProvider.AcquireAsync(GetUpdateLockResource(id), TimeSpan.FromSeconds(30), _options.LockAcquireTimeout), CancellationToken.None);

        var task = await GetAsync(id);
        if (task is null)
            return null;

        if (!update(task))
            return task;

        task.UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
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

        bool isAbandoned = utcNow - task.UpdatedUtc >= StaleAfter;
        if (!task.CanCancel && !isAbandoned)
            return BackgroundTaskCancelResult.NotCancellable(task);

        if (!await _cache.SetAsync(GetCancelCacheKey(id), true, TaskLifetime))
            throw new InvalidOperationException("The cancellation request could not be saved.");

        var updated = await UpdateAsync(id, current =>
        {
            if (!current.IsActive)
                return false;

            bool isRunningNow = current.Status is BackgroundTaskStatus.Running && utcNow - current.UpdatedUtc < StaleAfter;
            if (isRunningNow)
                return false;

            current.Finish(BackgroundTaskStatus.Cancelled, current.Status is BackgroundTaskStatus.Queued ? "Cancelled before it started." : "Cancelled after it stopped responding.", utcNow);
            return true;
        });

        return updated is null ? BackgroundTaskCancelResult.NotFound : BackgroundTaskCancelResult.Cancelled(updated);
    }

    public async Task<bool> IsCancelRequestedAsync(string id)
    {
        return (await _cache.GetAsync<bool>(GetCancelCacheKey(id))).HasValue;
    }

    public async Task SaveAsync(BackgroundTask task)
    {
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

    private Task FailToStartAsync(string id)
    {
        return UpdateAsync(id, current =>
        {
            // Queueing may have succeeded before its acknowledgement failed, allowing a worker or user to advance the task.
            if (current.Status is not BackgroundTaskStatus.Queued)
                return false;

            current.Finish(BackgroundTaskStatus.Failed, "The task could not be started. Please try again.", _timeProvider.GetUtcNow().UtcDateTime);
            return true;
        });
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
}

public record BackgroundTaskCancelResult
{
    public BackgroundTask? Task { get; init; }
    public bool IsNotCancellable { get; init; }

    public static readonly BackgroundTaskCancelResult NotFound = new();
    public static BackgroundTaskCancelResult Cancelled(BackgroundTask task) => new() { Task = task };
    public static BackgroundTaskCancelResult NotCancellable(BackgroundTask task) => new() { Task = task, IsNotCancellable = true };
}
