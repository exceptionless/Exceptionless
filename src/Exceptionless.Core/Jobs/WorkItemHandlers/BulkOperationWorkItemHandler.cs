using System.Text.Json;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Services.BackgroundTasks;
using Exceptionless.Core.Services.BulkOperations;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

/// <summary>
/// Runs a stored bulk task in batches. After every batch the counts and the resume cursor are saved and a progress
/// message is published; between batches it checks for cancellation. A restarted job continues from the saved cursor.
/// </summary>
public class BulkOperationWorkItemHandler : WorkItemHandlerBase
{
    /// <summary>
    /// How many temporary errors in a row (without a batch completing in between) end the task.
    /// </summary>
    public const int MaxTransientFailures = 3;

    /// <summary>
    /// Saved as the cursor once the last batch is done, so a run that restarts before the task is marked complete finds
    /// nothing left to do instead of starting the selection over.
    /// </summary>
    internal const string ExhaustedCursor = "exhausted";

    private readonly BulkOperationService _service;
    private readonly BackgroundTaskService _taskService;
    private readonly IReadOnlyDictionary<BulkOperationEntity, IBulkSelectionResolver> _resolvers;
    private readonly BulkOperationOptions _options;
    private readonly ILockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IResiliencePolicy _checkpointPolicy;

    public BulkOperationWorkItemHandler(BulkOperationService service, BackgroundTaskService taskService, IEnumerable<IBulkSelectionResolver> resolvers, BulkOperationOptions options,
        ILockProvider lockProvider, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _service = service;
        _taskService = taskService;
        _resolvers = resolvers.ToDictionary(r => r.Entity);
        _options = options;
        _lockProvider = lockProvider;
        _timeProvider = timeProvider;
        _checkpointPolicy = new ResiliencePolicyBuilder(Log, timeProvider)
            .WithMaxAttempts(MaxTransientFailures)
            .WithDelay(options.RetryDelay)
            .WithShouldRetry((_, exception) => !IsPermanent(exception))
            .Build();

        // Progress is reported after every batch. Renewing the queue entry and the task lock then keeps a long run from
        // timing out and being delivered to a second worker while this one is still deleting.
        AutoRenewLockOnProgress = true;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{nameof(BulkOperationWorkItem)}:{((BulkOperationWorkItem)workItem).TaskId}";
        return _lockProvider.TryAcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<BulkOperationWorkItem>()!;
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.TaskId);

        BackgroundTask? task = null;
        try
        {
            task = await _taskService.GetAsync(workItem.TaskId);
            if (task is null)
            {
                // The record expired or the cache was cleared. Say so, otherwise the progress panel waits forever.
                Log.LogWarning("Bulk operation {TaskId} no longer exists", workItem.TaskId);
                await _service.PublishLostAsync(workItem);
                return;
            }

            using var _ = Log.BeginScope(new ExceptionlessState().Organization(task.OrganizationId).Tag("BulkOperation"));
            if (!task.IsActive)
            {
                Log.LogInformation("Skipping bulk operation {TaskId} because it is already {Status}", task.Id, task.Status);
                return;
            }

            var handler = _service.GetHandler(task.Type);
            if (handler is null || !_resolvers.TryGetValue(handler.Entity, out var resolver))
            {
                await FinishAsync(workItem, BackgroundTaskStatus.Failed, $"Unknown bulk operation type '{task.Type}'.");
                return;
            }

            if (await _taskService.IsCancelRequestedAsync(task.Id))
            {
                await FinishAsync(workItem, BackgroundTaskStatus.Cancelled, "Cancelled by user.");
                return;
            }

            string? pinnedSelection = null;
            if (task.Bulk!.Selection.Kind is BulkOperationSelectionKind.Filter)
            {
                pinnedSelection = await _service.GetPinnedSelectionAsync(task.Id);
                if (pinnedSelection is null)
                {
                    await FinishAsync(workItem, BackgroundTaskStatus.Failed, "The saved selection expired, so the operation could not continue.");
                    return;
                }
            }

            task = await _taskService.UpdateAsync(task.Id, current =>
            {
                if (!current.IsActive)
                    return false;

                current.Start(UtcNow);
                return true;
            });

            if (task is null)
            {
                await _service.PublishLostAsync(workItem);
                return;
            }

            if (task.Status is not BackgroundTaskStatus.Running)
                return;

            Log.LogInformation("Running bulk operation {TaskId} {Type} from cursor {Cursor}", task.Id, task.Type, task.Bulk!.Cursor);
            await context.ReportProgressAsync(GetPercent(task), task.Description);

            while (true)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    // The host is shutting down. Leave the task running so the queue retry resumes it from the cursor.
                    context.Result = JobResult.CancelledWithMessage($"Stopping bulk operation {task.Id} for shutdown");
                    return;
                }

                if (await _taskService.IsCancelRequestedAsync(task.Id))
                {
                    await FinishAsync(workItem, BackgroundTaskStatus.Cancelled, $"Cancelled by user after {task.Processed:N0} of {task.Total:N0}.");
                    return;
                }

                var page = await GetPageAsync(task, resolver, pinnedSelection, context.CancellationToken);
                if (page.Ids.Count == 0)
                    break;

                // The user who started the task is authorized once, when it is created. Each batch re-checks the scope
                // (organization and project) but not the user: someone removed from the organization mid-run still finishes
                // the deletion they legitimately started, and any member of the organization can cancel it.
                var result = await handler.ExecuteBatchAsync(task, page.Ids, context.CancellationToken);

                task = await SaveBatchAsync(task, page, result);

                if (task is null)
                {
                    await _service.PublishLostAsync(workItem);
                    return;
                }

                if (!task.IsActive)
                    return;

                await context.ReportProgressAsync(GetPercent(task), $"{task.Processed:N0} of {task.Total:N0}");

                if (page.NextCursor is null)
                    break;
            }

            task = await _taskService.UpdateAsync(task.Id, current =>
            {
                if (!current.IsActive)
                    return false;

                current.Total = current.Processed;
                current.Finish(BackgroundTaskStatus.Completed, current.Failed > 0 ? $"Completed with {current.Failed:N0} failures." : "Completed.", UtcNow);
                return true;
            });

            if (task is null)
                await _service.PublishLostAsync(workItem);
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            context.Result = JobResult.CancelledWithMessage($"Stopping bulk operation {workItem.TaskId} for shutdown");
        }
        catch (BatchCheckpointException ex)
        {
            Log.LogError(ex, "Bulk operation {TaskId} could not save completed batch progress: {Message}", workItem.TaskId, ex.Message);
            await FinishAsync(workItem, BackgroundTaskStatus.Failed, ex.Message);
        }
        catch (BulkSelectionRejectedException ex)
        {
            Log.LogWarning(ex, "Bulk operation {TaskId} selection is no longer available: {Message}", workItem.TaskId, ex.Message);
            await FinishAsync(workItem, BackgroundTaskStatus.Failed, ex.Message);
        }
        catch (Exception ex) when (!IsPermanent(ex))
        {
            // Elasticsearch, cache and network hiccups: continue in a new work item from the saved counts and cursor. Rethrowing
            // would use the queue's retries, which cover the whole work item, so a task that makes progress between errors
            // would run out of deliveries while still marked running. After MaxTransientFailures in a row, give up.
            var failed = await _taskService.UpdateAsync(workItem.TaskId, current =>
            {
                if (!current.IsActive)
                    return false;

                current.Bulk!.RetryCount++;
                current.Message = current.Bulk!.RetryCount >= MaxTransientFailures ? "The operation stopped because of repeated errors." : "Retrying after a temporary error.";
                if (current.Bulk!.RetryCount >= MaxTransientFailures)
                    current.Finish(BackgroundTaskStatus.Failed, current.Message, UtcNow);

                return true;
            });

            if (failed is { IsActive: false })
            {
                Log.LogError(ex, "Bulk operation {TaskId} failed after repeated errors: {Message}", workItem.TaskId, ex.Message);
                return;
            }

            Log.LogWarning(ex, "Bulk operation {TaskId} hit a temporary error: {Message}", workItem.TaskId, ex.Message);
            if (failed is null)
            {
                await _service.PublishLostAsync(workItem);
                return;
            }

            try
            {
                if (_options.RetryDelay > TimeSpan.Zero)
                    await Task.Delay(_options.RetryDelay * failed.Bulk!.RetryCount, _timeProvider, context.CancellationToken);

                await _service.ContinueAsync(workItem);
            }
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                context.Result = JobResult.CancelledWithMessage($"Stopping bulk operation {workItem.TaskId} for shutdown");
            }
            catch (Exception continuationException)
            {
                Log.LogError(continuationException, "Bulk operation {TaskId} could not queue another run: {Message}", workItem.TaskId, continuationException.Message);
                await FinishAsync(workItem, BackgroundTaskStatus.Failed, "The operation stopped because another run could not be queued.");
            }
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Bulk operation {TaskId} failed: {Message}", workItem.TaskId, ex.Message);
            await FinishAsync(workItem, BackgroundTaskStatus.Failed, "The operation stopped because of an unexpected error.");
        }
    }

    private async Task<BackgroundTask?> SaveBatchAsync(BackgroundTask task, BulkSelectionPage page, BulkBatchResult result)
    {
        // Keep the known result while retrying its checkpoint, including during shutdown. Replaying a successful delete
        // would classify its now-missing ids as failures or omit them from a filter selection's counts. A process stopping
        // before any checkpoint is saved can still lose that result; the deletion and task record are separate writes.
        try
        {
            return await _checkpointPolicy.ExecuteAsync(async _ => await _taskService.UpdateAsync(task.Id, current =>
            {
                // A save can succeed before releasing its update lock throws. The prior cursor makes another attempt harmless
                // if that checkpoint was committed already, or if the task has since advanced or finished.
                if (!current.IsActive || !String.Equals(current.Bulk!.Cursor, task.Bulk!.Cursor, StringComparison.Ordinal))
                    return false;

                current.Processed += page.Ids.Count;
                current.Succeeded += result.Succeeded;
                current.Failed += result.Failed;
                current.Bulk.Cursor = page.NextCursor ?? ExhaustedCursor;
                current.Bulk.RetryCount = 0;
                current.Total = Math.Max(current.Total, current.Processed);
                current.Message = result.Message;
                current.Progress = GetPercent(current);
                return true;
            }), CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Replaying a batch known to have succeeded would lose its result again. Stop if its bounded checkpoint
            // retries are exhausted, keeping the last durable counts and making the uncertain outcome explicit.
            throw new BatchCheckpointException(ex);
        }
    }

    /// <summary>
    /// Errors that retrying cannot fix. Everything else (Elasticsearch, cache and network failures surface as many different
    /// exception types) is treated as temporary and bounded by <see cref="MaxTransientFailures"/>.
    /// </summary>
    internal static bool IsPermanent(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DocumentLimitExceededException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or JsonException or InvalidCastException or NullReferenceException or KeyNotFoundException)
                return true;
        }

        return false;
    }

    private async Task<BulkSelectionPage> GetPageAsync(BackgroundTask task, IBulkSelectionResolver resolver, string? pinnedSelection, CancellationToken cancellationToken)
    {
        int size = Math.Max(1, _options.BatchSize);
        if (task.Bulk!.Cursor == ExhaustedCursor)
            return new BulkSelectionPage([], null);

        if (task.Bulk!.Selection.Kind is not BulkOperationSelectionKind.Ids)
            return await resolver.GetPageAsync(task, pinnedSelection, task.Bulk!.Cursor, size, cancellationToken);

        // The cursor of an id selection is the index of the next id to process.
        var ids = task.Bulk!.Selection.Ids ?? [];
        int start = Int32.TryParse(task.Bulk!.Cursor, out int parsed) ? parsed : 0;
        var batch = ids.Skip(start).Take(size).ToList();
        int next = start + batch.Count;
        return new BulkSelectionPage(batch, next < ids.Count ? next.ToString() : null);
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    private async Task FinishAsync(BulkOperationWorkItem workItem, BackgroundTaskStatus status, string message)
    {
        var task = await _taskService.UpdateAsync(workItem.TaskId, current =>
        {
            if (!current.IsActive)
                return false;

            current.Finish(status, message, UtcNow);
            return true;
        });

        if (task is null)
            await _service.PublishLostAsync(workItem);
    }

    private static int GetPercent(BackgroundTask task)
    {
        return task.Total <= 0 ? 0 : (int)Math.Min(99, task.Processed * 100 / task.Total);
    }

    private sealed class BatchCheckpointException(Exception innerException)
        : InvalidOperationException("The operation stopped because its progress could not be saved. Some items may have been processed.", innerException);
}
