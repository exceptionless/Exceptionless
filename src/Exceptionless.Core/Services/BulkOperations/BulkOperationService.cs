using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;
using Foundatio.Caching;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Exceptionless.Core.Services.BulkOperations;

public class BulkOperationOptions
{
    /// <summary>How many ids are resolved and executed per batch.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>How long to wait before continuing after a temporary error. Multiplied by the number of errors in a row.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// The bulk specific part of background tasks: the registry of handlers and selection resolvers, counting and pinning a
/// selection, and the one-active-destructive-task-per-project rule. Storage, indexes and notifications are
/// <see cref="BackgroundTaskService"/>.
/// </summary>
public class BulkOperationService
{
    private readonly BackgroundTaskService _taskService;
    private readonly ICacheClient _cache;
    private readonly ILockProvider _lockProvider;
    private readonly IReadOnlyDictionary<string, IBulkOperationHandler> _handlers;
    private readonly IReadOnlyDictionary<BulkOperationEntity, IBulkSelectionResolver> _resolvers;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    public BulkOperationService(BackgroundTaskService taskService, ICacheClient cache, ILockProvider lockProvider,
        IEnumerable<IBulkOperationHandler> handlers, IEnumerable<IBulkSelectionResolver> resolvers, TimeProvider timeProvider, ILogger<BulkOperationService>? logger = null)
    {
        _taskService = taskService;
        _cache = cache;
        _lockProvider = lockProvider;
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.Ordinal);
        _resolvers = resolvers.ToDictionary(r => r.Entity);
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger<BulkOperationService>.Instance;
    }

    public static string GetPinnedSelectionCacheKey(string id) => $"{BackgroundTaskService.KeyPrefix}:{id}:selection";

    public IBulkOperationHandler? GetHandler(string type) => _handlers.GetValueOrDefault(type);

    /// <summary>
    /// Queues another run that continues from the saved cursor. The queue's retries cover a whole work item, so a long task
    /// continues in a new work item after a temporary error instead of using up the retries of the current one.
    /// </summary>
    public Task ContinueAsync(BulkOperationWorkItem workItem) => _taskService.ContinueAsync(workItem);

    public async Task<string?> GetPinnedSelectionAsync(string id)
    {
        var cached = await _cache.GetAsync<string>(GetPinnedSelectionCacheKey(id));
        return cached.HasValue ? cached.Value : null;
    }

    /// <summary>Releases a rejected or terminal task's selection without changing its durable outcome.</summary>
    public async Task ReleaseSelectionAsync(BackgroundTask task, string? pinnedSelection = null, string? cursor = null)
    {
        if (task.Bulk?.Selection.Kind is not BulkOperationSelectionKind.Filter
            || GetHandler(task.Type) is not { } handler || !_resolvers.TryGetValue(handler.Entity, out var resolver))
            return;

        try
        {
            pinnedSelection ??= await GetPinnedSelectionAsync(task.Id);
            await resolver.ReleaseAsync(task, pinnedSelection, cursor ?? task.Bulk.Cursor);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not release the selection for bulk operation {TaskId}: {Message}", task.Id, ex.Message);
        }
    }

    /// <summary>
    /// Stores and enqueues the task unless it conflicts with an active destructive task on the same project.
    /// The check and the write happen under a per-organization lock so two requests cannot both pass it.
    /// </summary>
    public async Task<BulkOperationCreateResult> CreateAsync(BackgroundTask task)
    {
        var handler = GetHandler(task.Type) ?? throw new ArgumentException($"Unknown bulk operation type '{task.Type}'.", nameof(task));
        var bulk = task.Bulk ?? throw new ArgumentException("A bulk task needs its bulk details.", nameof(task));

        task.CanCancel = true;

        string? pinnedSelection = null;
        bool enqueueAttempted = false;
        bool retainSelection = false;
        try
        {
            // Count and pin before taking the lock, so a slow search never holds up other requests.
            if (bulk.Selection.Kind is BulkOperationSelectionKind.Filter)
            {
                if (!_resolvers.TryGetValue(handler.Entity, out var resolver))
                    throw new InvalidOperationException($"No selection resolver is registered for {handler.Entity}.");

                task.CreatedUtc = _timeProvider.GetUtcNow().UtcDateTime;
                var preparation = await resolver.PrepareAsync(task, CancellationToken.None);
                pinnedSelection = preparation.PinnedState;
                if (!preparation.Check.IsOk)
                    return BulkOperationCreateResult.Rejected(preparation.Check);

                task.Total = preparation.Total;
            }
            else
            {
                task.Total = bulk.Selection.Ids?.Count ?? 0;
            }

            task.Description = handler.Describe(task);

            await using var createLock = await _lockProvider.TryAcquireAsync(BackgroundTaskService.GetCreateLockResource(task.OrganizationId), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10));
            if (createLock is null)
                return BulkOperationCreateResult.Busy();

            if (handler.IsDestructive)
            {
                var conflict = await FindConflictingTaskAsync(task);
                if (conflict is not null)
                    return BulkOperationCreateResult.Conflicting(conflict);
            }

            await _taskService.AddAsync(task);
            enqueueAttempted = true;
            await _taskService.EnqueueAsync(task, new BulkOperationWorkItem
            {
                Description = task.Description,
                TaskId = task.Id,
                OrganizationId = task.OrganizationId,
                Type = task.Type
            }, async () =>
            {
                // A selection that cannot be saved must fail the task just like an enqueue failure, so an unqueued task
                // cannot remain active and block the project's next operation.
                if (pinnedSelection is not null && !await _cache.SetAsync(GetPinnedSelectionCacheKey(task.Id), pinnedSelection, BackgroundTaskService.TaskLifetime))
                    throw new InvalidOperationException("The operation's selection could not be saved.");
            });

            retainSelection = true;
            return BulkOperationCreateResult.Created(task);
        }
        finally
        {
            if (!retainSelection && pinnedSelection is not null)
            {
                var cleanupTask = task;
                if (enqueueAttempted)
                {
                    try
                    {
                        // An enqueue can succeed before its acknowledgement is lost. The worker's saved state wins;
                        // never close an accepted active task's snapshot just because the creating request failed.
                        var saved = await _taskService.GetAsync(task.Id);
                        retainSelection = saved?.IsActive == true;
                        cleanupTask = saved ?? task;
                    }
                    catch (Exception ex)
                    {
                        retainSelection = true;
                        _logger.LogWarning(ex, "Could not check bulk operation {TaskId} before releasing its selection: {Message}", task.Id, ex.Message);
                    }
                }

                if (!retainSelection)
                    await ReleaseSelectionAsync(cleanupTask, pinnedSelection);
            }
        }
    }

    /// <summary>
    /// Tells clients that a task can no longer run because its record is gone (for example the cache was cleared).
    /// The work item carries enough to publish a final state so the progress panel does not wait forever.
    /// </summary>
    public Task PublishLostAsync(BulkOperationWorkItem workItem)
    {
        return _taskService.PublishAsync(new BackgroundTaskUpdated
        {
            Description = workItem.Description ?? "Bulk operation",
            Id = workItem.TaskId,
            Message = "The operation's progress was lost and it could not continue.",
            OrganizationId = workItem.OrganizationId,
            Status = BackgroundTaskStatus.Failed,
            Type = workItem.Type ?? String.Empty,
            UpdatedUtc = _timeProvider.GetUtcNow().UtcDateTime
        });
    }

    private async Task<BackgroundTask?> FindConflictingTaskAsync(BackgroundTask candidate)
    {
        // Only active tasks can conflict, and every one of them is in the active index no matter how much history
        // the organization has.
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var finished = new List<string>();
        BackgroundTask? conflict = null;
        foreach (string id in await _taskService.GetActiveIdsAsync(candidate.OrganizationId))
        {
            var existing = await _taskService.GetAsync(id);
            if (existing is null || !existing.IsActive)
            {
                finished.Add(id);
                continue;
            }

            if (GetHandler(existing.Type) is not { IsDestructive: true })
                continue;

            // An organization wide task can touch every project, so it conflicts with any project's task.
            bool overlaps = existing.ProjectId is null || candidate.ProjectId is null || String.Equals(existing.ProjectId, candidate.ProjectId, StringComparison.Ordinal);
            if (!overlaps)
                continue;

            if (utcNow - existing.UpdatedUtc >= BackgroundTaskService.StaleAfter)
            {
                // Retire it before admitting a replacement, so a delayed delivery cannot run it alongside the new one.
                var retired = await _taskService.UpdateAsync(existing.Id, current =>
                {
                    if (!current.IsActive || utcNow - current.UpdatedUtc < BackgroundTaskService.StaleAfter)
                        return false;

                    current.Finish(BackgroundTaskStatus.Failed, "Stopped because it made no progress for two hours.", utcNow);
                    return true;
                });

                if (retired is null || !retired.IsActive)
                    continue;

                existing = retired;
            }

            conflict ??= existing;
        }

        if (finished.Count > 0)
            await _taskService.RemoveFromActiveIndexAsync(candidate.OrganizationId, finished);

        return conflict;
    }
}

public record BulkOperationCreateResult
{
    public BackgroundTask? Task { get; init; }
    public BackgroundTask? Conflict { get; init; }
    public bool IsBusy { get; init; }
    public BulkOperationCheckResult? Rejection { get; init; }

    public static BulkOperationCreateResult Created(BackgroundTask task) => new() { Task = task };
    public static BulkOperationCreateResult Conflicting(BackgroundTask conflict) => new() { Conflict = conflict };
    public static BulkOperationCreateResult Busy() => new() { IsBusy = true };
    public static BulkOperationCreateResult Rejected(BulkOperationCheckResult rejection) => new() { Rejection = rejection };
}
