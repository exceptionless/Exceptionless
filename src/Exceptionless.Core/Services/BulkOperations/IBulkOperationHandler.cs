using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BulkOperations;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// One bulk action, e.g. <c>events.delete</c>. Adding a new bulk action means adding one implementation of this
/// interface; handlers are discovered and registered automatically.
/// </summary>
public interface IBulkOperationHandler
{
    /// <summary>The unique key that identifies the action in API requests and stored operations.</summary>
    string Type { get; }

    /// <summary>The kind of item the action targets. Selects the <see cref="IBulkSelectionResolver"/> that pages ids.</summary>
    BulkOperationEntity Entity { get; }

    /// <summary>Destructive actions are limited to one active operation per project.</summary>
    bool IsDestructive { get; }

    /// <summary>Checks that the actor may run this action against the organization and project.</summary>
    Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request);

    /// <summary>Checks the action specific parameters and the selection shape.</summary>
    Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request);

    /// <summary>A short, human readable description shown while the operation runs.</summary>
    string Describe(BulkOperation operation);

    /// <summary>
    /// Executes one batch of ids through the same code path as the single-item action, so side effects stay identical.
    /// Ids that cannot be processed (missing, or outside the operation's organization and project) must be reported as failed.
    /// </summary>
    Task<BulkBatchResult> ExecuteBatchAsync(BulkOperation operation, IReadOnlyCollection<string> ids, CancellationToken cancellationToken);
}

/// <summary>Pages the ids that match a filter selection. One implementation per <see cref="BulkOperationEntity"/>.</summary>
public interface IBulkSelectionResolver
{
    BulkOperationEntity Entity { get; }

    /// <summary>Checks that the filter of a selection can be run for the organization (syntax, plan limits).</summary>
    Task<BulkOperationCheckResult> ValidateSelectionAsync(Organization organization, BulkOperationSelection selection);

    /// <summary>
    /// Runs once when the operation is created. Counts what matches and pins every part of the selection that could
    /// otherwise change while the operation runs (for events, the stack level filter), so the selection stays exactly
    /// what the user confirmed and pages stay cheap. The pinned state is stored with the operation for its lifetime.
    /// </summary>
    Task<BulkSelectionPreparation> PrepareAsync(BulkOperation operation, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the next page of ids after <paramref name="cursor"/>. The cursor is a value based position, so
    /// paging stays correct while earlier items are being deleted or changed. Paging must not count totals.
    /// </summary>
    Task<BulkSelectionPage> GetPageAsync(BulkOperation operation, string? pinnedState, string? cursor, int size, CancellationToken cancellationToken);
}

public record BulkSelectionPreparation(BulkOperationCheckResult Check, long Total = 0, string? PinnedState = null)
{
    public static BulkSelectionPreparation Rejected(BulkOperationCheckResult check) => new(check);
}

public record BulkSelectionPage(IReadOnlyList<string> Ids, string? NextCursor);

public record BulkBatchResult(int Succeeded, int Failed, string? Message = null);

/// <summary>The authenticated caller, independent of the transport the request arrived on.</summary>
public record BulkOperationActor(string UserId, bool IsGlobalAdmin, IReadOnlyCollection<string> OrganizationIds)
{
    public bool CanAccessOrganization(string organizationId) => IsGlobalAdmin || OrganizationIds.Contains(organizationId, StringComparer.Ordinal);
}

public record BulkOperationRequest(BulkOperationActor Actor, Organization Organization, Project? Project, string Type, BulkOperationSelection Selection, IReadOnlyDictionary<string, string> Parameters);

public enum BulkOperationCheckStatus
{
    Ok,
    Denied,
    Invalid,
    PlanLimit
}

public record BulkOperationCheckResult(BulkOperationCheckStatus Status, string? Field = null, string? Message = null)
{
    public static readonly BulkOperationCheckResult Ok = new(BulkOperationCheckStatus.Ok);

    public bool IsOk => Status is BulkOperationCheckStatus.Ok;

    public static BulkOperationCheckResult Denied(string message) => new(BulkOperationCheckStatus.Denied, null, message);
    public static BulkOperationCheckResult Invalid(string field, string message) => new(BulkOperationCheckStatus.Invalid, field, message);
    public static BulkOperationCheckResult PlanLimit(string message) => new(BulkOperationCheckStatus.PlanLimit, null, message);
}
