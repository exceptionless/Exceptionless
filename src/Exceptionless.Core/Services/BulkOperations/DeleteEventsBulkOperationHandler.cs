using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// Deletes events. Each batch goes through <see cref="EventDeletionService"/>, the same code the single-item
/// <c>DELETE /events/{ids}</c> endpoint uses, so usage accounting and notifications are identical.
/// </summary>
public class DeleteEventsBulkOperationHandler : IBulkOperationHandler
{
    public const string OperationType = "events.delete";

    private readonly IEventRepository _eventRepository;
    private readonly EventDeletionService _eventDeletionService;

    public DeleteEventsBulkOperationHandler(IEventRepository eventRepository, EventDeletionService eventDeletionService)
    {
        _eventRepository = eventRepository;
        _eventDeletionService = eventDeletionService;
    }

    public string Type => OperationType;
    public BulkOperationEntity Entity => BulkOperationEntity.Events;
    public bool IsDestructive => true;

    public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request)
    {
        // Same rule as DELETE /events/{ids}: any user of the organization (or a global admin) may delete its events.
        if (!request.Actor.CanAccessOrganization(request.Organization.Id))
            return Task.FromResult(BulkOperationCheckResult.Denied("Organization not found."));

        if (request.Project is not null && !String.Equals(request.Project.OrganizationId, request.Organization.Id, StringComparison.Ordinal))
            return Task.FromResult(BulkOperationCheckResult.Denied("Project not found."));

        return Task.FromResult(BulkOperationCheckResult.Ok);
    }

    public Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request)
    {
        if (request.Parameters.Count > 0)
            return Task.FromResult(BulkOperationCheckResult.Invalid("parameters", $"The {Type} operation does not take parameters."));

        return Task.FromResult(BulkOperationCheckResult.Ok);
    }

    public string Describe(BulkOperation operation)
    {
        return operation.Selection.Kind is BulkOperationSelectionKind.Ids
            ? $"Delete {operation.Total:N0} selected events"
            : $"Delete {operation.Total:N0} events matching filter";
    }

    public async Task<BulkBatchResult> ExecuteBatchAsync(BulkOperation operation, IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var events = await _eventRepository.GetByIdsAsync(ids.ToArray(), o => o.Cache(false));

        // The operation may only touch its own organization (and project), no matter what ids it was given.
        var inScope = events
            .Where(e => String.Equals(e.OrganizationId, operation.OrganizationId, StringComparison.Ordinal)
                && (operation.ProjectId is null || String.Equals(e.ProjectId, operation.ProjectId, StringComparison.Ordinal)))
            .ToList();

        await _eventDeletionService.DeleteAsync(inScope);

        int failed = ids.Count - inScope.Count;
        return new BulkBatchResult(inScope.Count, failed, failed > 0 ? $"{failed:N0} events were not found." : null);
    }
}
