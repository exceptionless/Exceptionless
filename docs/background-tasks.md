# Background tasks

Background tasks let users follow work performed by `WorkItemJob`: bulk actions, project data resets, and sample-data generation. The task record describes progress; the work queue controls delivery. This guide covers the application contracts and the boundary for a future Foundatio extraction.

## Ownership and persistence

| Component | Owns | Source |
| --- | --- | --- |
| `BackgroundTaskService` | Cached records, organization indexes, serialized updates, cancellation requests, progress notifications | [Service](../src/Exceptionless.Core/Services/BackgroundTasks/BackgroundTaskService.cs) |
| `WorkItemJob` and its queue | Delivery, acknowledgment, work-item retries, execution locks | [Registration](../src/Exceptionless.Core/Bootstrapper.cs) |
| `BackgroundTaskTrackingWorkItemHandler` | Progress and lifecycle tracking around an existing ordinary work-item handler | [Tracking wrapper](../src/Exceptionless.Core/Jobs/WorkItemHandlers/BackgroundTaskTrackingWorkItemHandler.cs) |
| `BulkOperationService` and `BulkOperationWorkItemHandler` | Destructive-operation admission, batch execution, checkpoints, bounded continuation, selection cleanup | [Admission](../src/Exceptionless.Core/Services/BulkOperations/BulkOperationService.cs), [worker](../src/Exceptionless.Core/Jobs/WorkItemHandlers/BulkOperationWorkItemHandler.cs) |
| Selection resolver and action handler | Event membership, current access/retention rules, and the action's domain side effects | [Contracts](../src/Exceptionless.Core/Services/BulkOperations/IBulkOperationHandler.cs) |
| HTTP and WebSocket adapters | Authentication, organization access, public projections, client discovery and reconciliation | [API handler](../src/Exceptionless.Web/Api/Handlers/BackgroundTaskHandler.cs), [client](../src/Exceptionless.Web/ClientApp/src/lib/features/tasks/api.svelte.ts) |

The task record is the source of truth for saved progress. Counts and the bulk cursor share one record and are saved together. The queue payload carries the task identifier and the information required by its handler. Notifications are best effort; polling reads the saved state when a notification is delayed or lost.

Task records use the configured `ICacheClient` and expire seven days after their last save. They provide limited history rather than a durable audit log. Index entries have independent per-member expiration. Every active update renews active membership; a terminal update renews finished-history membership. This keeps long-running work discoverable without rewriting the larger history index on every progress report. The service also refreshes the index key's lifetime because renewing an existing member does not extend the key in every provider.

Record and index writes are separate operations. A cache failure can leave incomplete discovery even when a record was saved; a no-op mutation does not repair every prior index failure. History refresh precedes active removal so normal completion has overlapping membership and a failed refresh preserves the existing fallback. Treat this cache-backed history as best effort, and use a store with stronger indexing guarantees if a future consumer requires transactional discovery.

## Creating and updating a task

Ordinary callers use `CreateAndEnqueueAsync`. Authorization and project validation happen before this call. The factory runs after the task has its identifier, so the work item can reference it:

```csharp
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Services.BackgroundTasks;

static Task<BackgroundTask> QueueSampleDataAsync(
    BackgroundTaskService tasks,
    string organizationId,
    string projectId,
    string userId)
{
    return tasks.CreateAndEnqueueAsync(new BackgroundTask
    {
        OrganizationId = organizationId,
        ProjectId = projectId,
        Type = BackgroundTaskTypes.GenerateProjectSampleData,
        Description = "Generate sample data",
        CreatedByUserId = userId
    }, task => new GenerateSampleEventsWorkItem
    {
        OrganizationId = organizationId,
        ProjectId = projectId,
        TaskId = task.Id,
        EventCount = 250,
        DaysBack = 7
    });
}
```

This uses the same work item as [SampleDataService](../src/Exceptionless.Core/Utility/SampleDataService.cs). `CanCancel` defaults to false because this handler cannot stop midway safely. Set it to true only for an implementation that honors cancellation.

| Service method | Contract |
| --- | --- |
| `CreateAndEnqueueAsync` | Creates and indexes a queued task, then prepares and enqueues its work item. Factory or queue failures fail a still-queued task. |
| `GetAsync` | Returns the stored record, or null when absent. Internal callers must enforce their own access policy. |
| `GetByIdsAsync` | Returns existing tasks once per ID in input order, omitting missing records. Cache reads contain at most 50 records; callers enforce scope. |
| `GetByOrganizationAsync` | Returns all discoverable active tasks plus up to `limit` finished tasks, newest first; the default finished limit is 50. |
| `UpdateAsync` | Locks one task, reloads it, runs a synchronous mutation, and saves only when the callback returns true. Returns null when the record is absent. |
| `CancelAsync` | Records a cancellation request and returns the current task outcome, including a distinct non-cancellable result. |
| `IsCancelRequestedAsync` | Lets a cooperative worker check the persisted cancellation request at a safe boundary. |

`AddAsync` followed by `EnqueueAsync` is the lower-level path used by bulk admission, where selection state must be saved before enqueueing. Keep that sequence inside the owning operation service. Raw persistence is private so callers use the locking, indexing, and notification path.

Task storage and queue submission are separate operations. An enqueue exception can occur after the queue accepted the item; cleanup changes only a task that is still queued and preserves an outcome already recorded by the worker. Keep work-item factories free of domain side effects. The returned creation snapshot may already be older than worker progress by the time it reaches the client.

An `UpdateAsync` callback should only inspect and change the supplied task. Perform expensive work outside it, then checkpoint the result. The service owns `UpdatedUtc`: every committed mutation receives a timestamp strictly newer than the previous record, including when a clock repeats or moves backward. The persistence lock is released before publishing; a slow message bus does not hold up another mutation.

## Lifecycle and retry rules

`IsActive` means `Queued` or `Running`. The worker owns valid transitions for its task type; the service supplies serialized mutations.

| Event | Saved behavior |
| --- | --- |
| Accepted submission | `Queued`, with creation and update timestamps |
| Work starts or reports progress | `Running`; the first start timestamp is retained |
| Successful completion | `Completed`, completion timestamp, progress 100 |
| Ordinary handler throws | `Failed` with a generic public message; the queue may retry it |
| Ordinary queue retries a failed task | `Running` again; completion time, message, and percent are reset, and active membership is restored |
| Bulk transient error | Keep the task active and continue from its saved cursor; three consecutive transient failures end it as `Failed` |
| Bulk permanent error, lost selection, or exhausted checkpoint retries | `Failed`; the task does not reopen automatically |
| Duplicate delivery of completed or cancelled tracked work | Skip execution |

A non-success ordinary work-item result, such as shutdown, leaves tracking active so queue delivery can resume it. Bulk shutdown also retains its saved progress and selection. An ordinary work item with no `TaskId` runs without tracking. If an ordinary task record disappears, its existing queue payload can still run; a bulk task requires its saved record and selection and stops if either is lost.

`Failed` therefore describes a failed attempt for an ordinary tracked work item and a terminal outcome for a bulk operation. Consumers must allow a newer ordinary-work update to move `Failed` back to `Running`. Retry policy belongs to the queue or bulk worker, not to a status label alone.

## Cancellation and staleness

Cancellation is cooperative. A queued cancellable task can be marked `Cancelled` immediately. A running bulk task checks the request between batches, finishes the batch already in flight, then records `Cancelled`. Completed side effects are retained.

`StaleAfter` is two hours without a saved update. Staleness is computed when needed; it is not a separate status or a timer that automatically kills work. An abandoned task can be cleared through cancellation even when its type normally cannot be stopped. Bulk admission can retire an abandoned conflicting operation before accepting a replacement.

Cancelling a finished task is idempotent. A successful cancellation request can still return `Running` while the worker finishes its current batch. Continue observing the task until its state changes. The task snapshot does not expose a separate cancellation-pending field.

## HTTP and notification contracts

All routes are under `/api/v2`. Task lookup and cancellation enforce organization access; inaccessible records return the same not-found response as missing records.

| Route | Result |
| --- | --- |
| `POST /bulk-operations` | HTTP 202 with the accepted task; conflicting destructive work returns 409 |
| `GET /tasks/{id}` | One task snapshot; missing or inaccessible tasks return 404 |
| `GET /organizations/{id}/tasks` | All active tasks plus the newest 50 finished tasks |
| `POST /tasks/{id}/cancel` | HTTP 202 with the current snapshot; a live non-cancellable task returns 422 |

The HTTP [view model](../src/Exceptionless.Web/Models/BackgroundTask/ViewBackgroundTask.cs) includes status, counts, message, creator, timestamps, and a selection summary for bulk work. That summary can contain the filter, original time expression, resolved UTC bounds, and explicit ID count. It omits the ID array, operation parameters, pinned membership, snapshot identifier, and resume cursor. The smaller [BackgroundTaskUpdated message](../src/Exceptionless.Core/Models/Messaging/BackgroundTaskUpdated.cs) also omits the selection summary.

Messages can be duplicated, delayed, or delivered out of order. Compare `UpdatedUtc` at .NET tick precision before applying them; JavaScript's `Date` alone discards submillisecond precision. Unknown task identifiers trigger rediscovery. The client polls while tasks are active and refetches when returning to an organization. A message is a hint to reconcile saved state, not an instruction to execute work.

## Bulk selection and execution

Explicit ID selection accepts at most 1,000 distinct IDs. Larger requests use a filter. The resolver captures filter membership during preparation: a complete small selection keeps fixed IDs, while a larger selection keeps an Elasticsearch point-in-time snapshot and search-after cursor. Relative dates use a fixed UTC anchor and invariant formatting.

For filter selections, current organization/project scope, search-plan rules, and retention can narrow the eligible set. Later arrivals or edits cannot expand it. Missing, expired, or legacy selection state fails safely and requires starting a new operation; the worker does not silently re-run a live filter. Empty selections have an explicit persisted representation because the application serializer omits empty collections.

Preparation runs before the organization admission lock. Destructive bulk operations that overlap the same project cannot be admitted together; an organization-wide operation overlaps every project. Each batch executes at most the configured number of IDs, with a default batch size of 500. Event deletion fetches only identity, daily-index routing, organization, project, and stack fields, then uses the same deletion service as individual deletes.

The snapshot has a two-hour idle lease, renewed while paging. Terminal or rejected work releases it best effort; active retries and shutdown retain it. Expiry bounds the resource when cleanup cannot reach the backend.

Completion does not force an Elasticsearch index refresh. Deletion uses the repository's normal eventual search consistency, so search results can briefly retain deleted events until the index refreshes.

Deletion and checkpoint persistence are separate writes. The worker retries saving a known batch result before considering another execution. A process can still stop after a side effect but before its checkpoint is saved, so handlers must tolerate replay. Missing items are reported as failed; counts are not an exactly-once ledger of external side effects.

## Adding work

For an ordinary tracked job:

1. Implement the existing work-item handler and give its work-item type an optional `TaskId` through `IBackgroundTaskWorkItem`.
2. Register it through `TrackWithBackgroundTask` in `Bootstrapper`, following reset-data or sample-data generation.
3. Create it through `CreateAndEnqueueAsync` after authorization, and report progress through `WorkItemContext.ReportProgressAsync` so execution-lock renewal still happens.
4. Define which side effects may replay, whether cancellation is safe, and which queue failures are retryable.

For a bulk action, implement `IBulkOperationHandler` with a unique operation type, authorization, parameter validation, description, and bounded batch execution. Reuse the existing entity resolver when membership semantics match. A new entity family also needs an `IBulkSelectionResolver` that prepares stable membership, returns opaque resumable cursors, and releases its resources. Keep all domain side effects in the existing domain service.

## Scaling and a future Foundatio boundary

Keep immutable selection data separate from frequently updated progress where the existing format permits it. Process one batch at a time, project only required fields, and use the cache's bulk-read operation for groups of task records. Listing reads each index once, orders the IDs, and loads records in batches of at most 50. It has no silent history-page cutoff. Conflict detection uses the same bounded record reads while checking the active index.

Returning all active tasks inherently requires work proportional to that active set. The generic cache-list API does not provide a portable ordered recent-history query, so listing still reads and orders the live history IDs. `GetAllAsync` lets providers combine reads; it does not promise one network round trip on every provider or Redis Cluster topology. Active-index renewal adds writes to a progress update, while the history index is renewed only on terminal updates. Measure these operations separately when changing retention or storage.

| Reusable responsibility | Keep in the application adapter |
| --- | --- |
| Task identity, status, progress, timestamps, and serialized state updates | ObjectId HTTP routes and Exceptionless model interfaces |
| Store retention, discovery, bounded reads, and cancellation signaling | Organization/project/user authorization and admission policy |
| Worker tracking and progress reporting using existing Foundatio primitives | Event filters, plan limits, retention policy, Elasticsearch snapshots, deletion accounting |
| Optional advisory change notifications | Public HTTP/WebSocket projections, UI text, and task-panel behavior |

The current `BackgroundTask` includes organization/project/user fields and `BulkOperationDetails`; it is still an application model. Preserve that honest boundary until another consumer establishes the common contract. An extraction should define identity, owner scope, private checkpoint serialization, retention/index consistency, and retry ownership explicitly, while reusing `ICacheClient`, queues, locks, messaging, resilience, and `TimeProvider`. Introduce a storage abstraction when it is needed to offer indexed discovery or durability that the cache contract cannot provide. Avoid a generic payload bag or a parallel queue/state-machine framework solely to anticipate reuse.

## Verification and operations

Service tests use real Foundatio in-memory infrastructure and controlled time. Regression coverage belongs at the failure boundary: factory/index/queue failures, concurrent updates, delayed notifications, cancellation, retention, full selection paging, and checkpoint recovery. Integration tests exercise real serialization, Elasticsearch routing, authorization, usage accounting, and selection mutation. Browser tests cover discovery, concurrent task completion, and cancellation access.

Start with the focused classes under [BackgroundTasks](../tests/Exceptionless.Tests/Services/BackgroundTasks) and [BulkOperations](../tests/Exceptionless.Tests/Services/BulkOperations), then the [worker tests](../tests/Exceptionless.Tests/Jobs/WorkItemHandlers), endpoint tests, and the full CI run. Manual performance measurements live in [benchmarks/BackgroundTasks](../benchmarks/BackgroundTasks); they report service time, allocation, and cache calls separately from provider/network throughput.

Deploy the updated job before or with the web application. Cache loss can remove progress and prepared selections; treat that as an interrupted operation requiring user-visible recovery. This feature adds no database migration or index. Preserve public DTOs and message shapes when changing internal storage or moving reusable components to Foundatio.
