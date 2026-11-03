using System.Text.Json;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Services.BulkOperations;
using Foundatio.Jobs;
using Foundatio.Lock;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

/// <summary>
/// Runs a stored bulk operation in batches. After every batch the counts and the resume cursor are saved and a progress
/// message is published; between batches it checks for cancellation. A restarted job continues from the saved cursor.
/// </summary>
public class BulkOperationWorkItemHandler : WorkItemHandlerBase
{
    /// <summary>
    /// How many temporary errors in a row (without a batch completing in between) end the operation.
    /// </summary>
    public const int MaxTransientFailures = 3;

    /// <summary>
    /// Saved as the cursor once the last batch is done, so a run that restarts before the operation is marked complete finds
    /// nothing left to do instead of starting the selection over.
    /// </summary>
    internal const string ExhaustedCursor = "exhausted";

    private readonly BulkOperationService _service;
    private readonly IReadOnlyDictionary<BulkOperationEntity, IBulkSelectionResolver> _resolvers;
    private readonly BulkOperationOptions _options;
    private readonly ILockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;

    public BulkOperationWorkItemHandler(BulkOperationService service, IEnumerable<IBulkSelectionResolver> resolvers, BulkOperationOptions options,
        ILockProvider lockProvider, TimeProvider timeProvider, ILoggerFactory loggerFactory) : base(loggerFactory)
    {
        _service = service;
        _resolvers = resolvers.ToDictionary(r => r.Entity);
        _options = options;
        _lockProvider = lockProvider;
        _timeProvider = timeProvider;

        // Progress is reported after every batch. Renewing the queue entry and the operation lock then keeps a long run from
        // timing out and being delivered to a second worker while this one is still deleting.
        AutoRenewLockOnProgress = true;
    }

    public override Task<ILock?> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        string cacheKey = $"{nameof(BulkOperationWorkItem)}:{((BulkOperationWorkItem)workItem).OperationId}";
        return _lockProvider.TryAcquireAsync(cacheKey, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var workItem = context.GetData<BulkOperationWorkItem>()!;
        ArgumentException.ThrowIfNullOrWhiteSpace(workItem.OperationId);

        var operation = await _service.GetAsync(workItem.OperationId);
        if (operation is null)
        {
            // The record expired or the cache was cleared. Say so, otherwise the progress panel waits forever.
            Log.LogWarning("Bulk operation {OperationId} no longer exists", workItem.OperationId);
            await _service.PublishLostAsync(workItem);
            return;
        }

        using var _ = Log.BeginScope(new ExceptionlessState().Organization(operation.OrganizationId).Tag("BulkOperation"));
        if (!operation.IsActive)
        {
            Log.LogInformation("Skipping bulk operation {OperationId} because it is already {Status}", operation.Id, operation.Status);
            return;
        }

        var handler = _service.GetHandler(operation.Type);
        if (handler is null || !_resolvers.TryGetValue(handler.Entity, out var resolver))
        {
            await FinishAsync(operation.Id, BulkOperationStatus.Failed, $"Unknown bulk operation type '{operation.Type}'.");
            return;
        }

        if (await _service.IsCancelRequestedAsync(operation.Id))
        {
            await FinishAsync(operation.Id, BulkOperationStatus.Cancelled, "Cancelled by user.");
            return;
        }

        string? pinnedSelection = null;
        if (operation.Selection.Kind is BulkOperationSelectionKind.Filter)
        {
            pinnedSelection = await _service.GetPinnedSelectionAsync(operation.Id);
            if (pinnedSelection is null)
            {
                await FinishAsync(operation.Id, BulkOperationStatus.Failed, "The saved selection expired, so the operation could not continue.");
                return;
            }
        }

        operation = await _service.UpdateAsync(operation.Id, current =>
        {
            if (!current.IsActive)
                return false;

            current.Status = BulkOperationStatus.Running;
            current.StartedUtc ??= _timeProvider.GetUtcNow().UtcDateTime;
            return true;
        });

        if (operation is null || operation.Status is not BulkOperationStatus.Running)
            return;

        Log.LogInformation("Running bulk operation {OperationId} {Type} from cursor {Cursor}", operation.Id, operation.Type, operation.Cursor);
        await context.ReportProgressAsync(GetPercent(operation), operation.Description);

        try
        {
            while (true)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    // The host is shutting down. Leave the operation running so the queue retry resumes it from the cursor.
                    context.Result = JobResult.CancelledWithMessage($"Stopping bulk operation {operation.Id} for shutdown");
                    return;
                }

                if (await _service.IsCancelRequestedAsync(operation.Id))
                {
                    await FinishAsync(operation.Id, BulkOperationStatus.Cancelled, $"Cancelled by user after {operation.Processed:N0} of {operation.Total:N0}.");
                    return;
                }

                var page = await GetPageAsync(operation, resolver, pinnedSelection, context.CancellationToken);
                if (page.Ids.Count == 0)
                    break;

                // The user who started the operation is authorized once, when it is created. Each batch re-checks the scope
                // (organization and project) but not the user: someone removed from the organization mid-run still finishes
                // the deletion they legitimately started, and any member of the organization can cancel it.
                var result = await handler.ExecuteBatchAsync(operation, page.Ids, context.CancellationToken);

                operation = await _service.UpdateAsync(operation.Id, current =>
                {
                    if (!current.IsActive)
                        return false;

                    current.Processed += page.Ids.Count;
                    current.Succeeded += result.Succeeded;
                    current.Failed += result.Failed;
                    current.Cursor = page.NextCursor ?? ExhaustedCursor;
                    current.RetryCount = 0;
                    current.Total = Math.Max(current.Total, current.Processed);
                    current.Message = result.Message;
                    return true;
                });

                if (operation is null || !operation.IsActive)
                    return;

                await context.ReportProgressAsync(GetPercent(operation), $"{operation.Processed:N0} of {operation.Total:N0}");

                if (page.NextCursor is null)
                    break;
            }

            await _service.UpdateAsync(operation.Id, current =>
            {
                if (!current.IsActive)
                    return false;

                current.Total = current.Processed;
                _service.Finish(current, BulkOperationStatus.Completed, current.Failed > 0 ? $"Completed with {current.Failed:N0} failures." : "Completed.");
                return true;
            });
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            context.Result = JobResult.CancelledWithMessage($"Stopping bulk operation {operation?.Id} for shutdown");
        }
        catch (Exception ex) when (!IsPermanent(ex))
        {
            // Elasticsearch, cache and network hiccups: continue in a new work item from the saved counts and cursor. Rethrowing
            // would use the queue's retries, which cover the whole work item, so an operation that makes progress between errors
            // would run out of deliveries while still marked running. After MaxTransientFailures in a row, give up.
            var failed = await _service.UpdateAsync(workItem.OperationId, current =>
            {
                if (!current.IsActive)
                    return false;

                current.RetryCount++;
                current.Message = current.RetryCount >= MaxTransientFailures ? "The operation stopped because of repeated errors." : "Retrying after a temporary error.";
                if (current.RetryCount >= MaxTransientFailures)
                    _service.Finish(current, BulkOperationStatus.Failed, current.Message);

                return true;
            });

            if (failed is { IsActive: false })
            {
                Log.LogError(ex, "Bulk operation {OperationId} failed after repeated errors: {Message}", workItem.OperationId, ex.Message);
                return;
            }

            Log.LogWarning(ex, "Bulk operation {OperationId} hit a temporary error and will continue in a new run: {Message}", workItem.OperationId, ex.Message);
            if (failed is null)
                return;

            if (_options.RetryDelay > TimeSpan.Zero)
                await Task.Delay(_options.RetryDelay * failed.RetryCount, _timeProvider, context.CancellationToken);

            await _service.ContinueAsync(workItem);
        }
        catch (Exception ex)
        {
            Log.LogError(ex, "Bulk operation {OperationId} failed: {Message}", workItem.OperationId, ex.Message);
            await FinishAsync(workItem.OperationId, BulkOperationStatus.Failed, "The operation stopped because of an unexpected error.");
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

    private async Task<BulkSelectionPage> GetPageAsync(BulkOperation operation, IBulkSelectionResolver resolver, string? pinnedSelection, CancellationToken cancellationToken)
    {
        int size = Math.Max(1, _options.BatchSize);
        if (operation.Cursor == ExhaustedCursor)
            return new BulkSelectionPage([], null);

        if (operation.Selection.Kind is not BulkOperationSelectionKind.Ids)
            return await resolver.GetPageAsync(operation, pinnedSelection, operation.Cursor, size, cancellationToken);

        // The cursor of an id selection is the index of the next id to process.
        var ids = operation.Selection.Ids ?? [];
        int start = Int32.TryParse(operation.Cursor, out int parsed) ? parsed : 0;
        var batch = ids.Skip(start).Take(size).ToList();
        int next = start + batch.Count;
        return new BulkSelectionPage(batch, next < ids.Count ? next.ToString() : null);
    }

    private Task FinishAsync(string operationId, BulkOperationStatus status, string message)
    {
        return _service.UpdateAsync(operationId, current =>
        {
            if (!current.IsActive)
                return false;

            _service.Finish(current, status, message);
            return true;
        });
    }

    private static int GetPercent(BulkOperation operation)
    {
        return operation.Total <= 0 ? 0 : (int)Math.Min(99, operation.Processed * 100 / operation.Total);
    }
}
