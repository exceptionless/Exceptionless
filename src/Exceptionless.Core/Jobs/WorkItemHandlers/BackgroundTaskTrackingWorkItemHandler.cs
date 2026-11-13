using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

/// <summary>
/// Wraps the handler of a work item that is tracked by a background task. Progress, success and failure are written to the
/// task in the job process that runs the work (not from a bus subscriber, which every web instance would run), then the
/// original behavior happens: the wrapped handler runs and the job's own progress callback still renews the locks. Work items
/// without a task id pass straight through.
/// </summary>
internal sealed class BackgroundTaskTrackingWorkItemHandler : IWorkItemHandler
{
    private readonly IWorkItemHandler _inner;
    private readonly BackgroundTaskService _taskService;
    private readonly TimeProvider _timeProvider;

    public BackgroundTaskTrackingWorkItemHandler(IWorkItemHandler inner, BackgroundTaskService taskService, TimeProvider timeProvider)
    {
        _inner = inner;
        _taskService = taskService;
        _timeProvider = timeProvider;
    }

    public bool AutoRenewLockOnProgress
    {
        get => _inner.AutoRenewLockOnProgress;
        set => _inner.AutoRenewLockOnProgress = value;
    }

    public ILogger Log
    {
        get => _inner.Log;
        set => _inner.Log = value;
    }

    public Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        return _inner.GetWorkItemLockAsync(workItem, cancellationToken);
    }

    public void LogProcessingQueueEntry(IQueueEntry<WorkItemData> queueEntry, Type workItemDataType, object workItem)
    {
        _inner.LogProcessingQueueEntry(queueEntry, workItemDataType, workItem);
    }

    public void LogAutoCompletedQueueEntry(IQueueEntry<WorkItemData> queueEntry, Type workItemDataType, object workItem)
    {
        _inner.LogAutoCompletedQueueEntry(queueEntry, workItemDataType, workItem);
    }

    public async Task HandleItemAsync(WorkItemContext context)
    {
        if (context.Data is not IBackgroundTaskWorkItem { TaskId: { Length: > 0 } taskId })
        {
            await _inner.HandleItemAsync(context);
            return;
        }

        string? lastMessage = null;
        var tracked = new WorkItemContext(context.Data, context.JobId, context.WorkItemLock, context.CancellationToken, async (progress, message) =>
        {
            lastMessage = message ?? lastMessage;
            await TryUpdateAsync(taskId, task =>
            {
                if (!task.IsActive)
                    return false;

                task.Start(UtcNow);
                task.Progress = Math.Clamp(progress, 0, 100);
                task.Message = message;
                return true;
            });

            await context.ReportProgressAsync(progress, message);
        });

        if (!await StartAsync(taskId))
            return;

        try
        {
            await _inner.HandleItemAsync(tracked);
        }
        catch (Exception)
        {
            await TryUpdateAsync(taskId, task =>
            {
                if (!task.IsActive)
                    return false;

                // The exception is logged by the job; its message can contain internal details, so the user sees a generic one.
                task.Finish(BackgroundTaskStatus.Failed, "An error occurred. It will be retried automatically if possible.", UtcNow);
                return true;
            });

            throw;
        }

        context.Result = tracked.Result;

        // A result that is not a success (for example a shutdown) means the queue runs the work item again, so the task keeps running.
        if (!tracked.Result.IsSuccess)
            return;

        await TryUpdateAsync(taskId, task =>
        {
            if (!task.IsActive)
                return false;

            task.Finish(BackgroundTaskStatus.Completed, lastMessage ?? "Completed.", UtcNow);
            return true;
        });
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Marks the task running. A queue retry runs the work item again after it failed, which brings the task back to running
    /// and back into the active index. A cancelled or completed task must not run again, so a failed start decision must
    /// be retried by the queue rather than treated as a missing task record.
    /// </summary>
    private async Task<bool> StartAsync(string taskId)
    {
        bool wasFinished = false;
        var task = await _taskService.UpdateAsync(taskId, current =>
        {
            if (current.Status is BackgroundTaskStatus.Cancelled or BackgroundTaskStatus.Completed)
                return false;

            wasFinished = current.Status is BackgroundTaskStatus.Failed;
            current.Start(UtcNow);
            if (wasFinished)
            {
                current.CompletedUtc = null;
                current.Message = null;
                current.Progress = 0;
            }

            return true;
        });

        if (wasFinished && task is not null)
            await TryAsync(taskId, () => _taskService.AddToActiveIndexAsync(task.OrganizationId, task.Id));

        return task is not { Status: BackgroundTaskStatus.Cancelled or BackgroundTaskStatus.Completed };
    }

    private async Task<BackgroundTask?> TryUpdateAsync(string taskId, Func<BackgroundTask, bool> update)
    {
        BackgroundTask? result = null;
        await TryAsync(taskId, async () => result = await _taskService.UpdateAsync(taskId, update));
        return result;
    }

    // Tracking is best effort: a cache or lock problem must not fail the work the user asked for.
    private async Task TryAsync(string taskId, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "Unable to update background task {TaskId}: {Message}", taskId, ex.Message);
        }
    }
}
