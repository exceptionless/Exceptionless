using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Models.BulkOperations;
using Exceptionless.Core.Repositories;
using Foundatio.Repositories;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>
/// What every stack action has in common: the organization and project checks of the single-item stack endpoints, loading the
/// batch and keeping it inside the task's scope. Each action applies its change through <see cref="StackActionService"/>, the
/// same code the single-item endpoints use.
/// </summary>
public abstract class StackBulkOperationHandler : IBulkOperationHandler
{
    private readonly IStackRepository _stackRepository;

    protected StackBulkOperationHandler(IStackRepository stackRepository, StackActionService stackActions)
    {
        _stackRepository = stackRepository;
        StackActions = stackActions;
    }

    public abstract string Type { get; }
    public BulkOperationEntity Entity => BulkOperationEntity.Stacks;
    public virtual bool IsDestructive => false;

    protected StackActionService StackActions { get; }

    public Task<BulkOperationCheckResult> AuthorizeAsync(BulkOperationRequest request)
    {
        // Same rule as the stack endpoints: any user of the organization (or a global admin) may change its stacks.
        if (!request.Actor.CanAccessOrganization(request.Organization.Id))
            return Task.FromResult(BulkOperationCheckResult.Denied("Organization not found."));

        if (request.Project is not null && !String.Equals(request.Project.OrganizationId, request.Organization.Id, StringComparison.Ordinal))
            return Task.FromResult(BulkOperationCheckResult.Denied("Project not found."));

        return Task.FromResult(BulkOperationCheckResult.Ok);
    }

    public abstract Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request);

    public abstract string Describe(BackgroundTask task);

    public async Task<BulkBatchResult> ExecuteBatchAsync(BackgroundTask task, IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        // The batch is read from the index rather than the cache so the state that is changed starts from what is stored.
        var stacks = await _stackRepository.GetByIdsAsync(ids.ToArray(), o => o.Cache(false));

        // The task may only touch its own organization (and project), no matter what ids it was given.
        var inScope = stacks
            .Where(s => String.Equals(s.OrganizationId, task.OrganizationId, StringComparison.Ordinal)
                && (task.ProjectId is null || String.Equals(s.ProjectId, task.ProjectId, StringComparison.Ordinal)))
            .ToList();

        if (inScope.Count > 0)
        {
            await ApplyAsync(task, inScope);
        }

        int failed = ids.Count - inScope.Count;
        return new BulkBatchResult(inScope.Count, failed, failed > 0 ? $"{failed:N0} stacks were not found." : null);
    }

    /// <summary>Applies the action to stacks that are already verified to belong to the task's scope.</summary>
    protected abstract Task ApplyAsync(BackgroundTask task, IReadOnlyCollection<Stack> stacks);

    protected static BulkOperationCheckResult Ok => BulkOperationCheckResult.Ok;

    protected static string Subject(BackgroundTask task) => task.Bulk!.Selection.Kind is BulkOperationSelectionKind.Ids
        ? $"{task.Total:N0} selected stacks"
        : $"{task.Total:N0} stacks matching filter";

    /// <summary>Allows only the named parameters. Anything else is rejected so a typo never silently does nothing.</summary>
    protected BulkOperationCheckResult RejectUnknownParameters(BulkOperationRequest request, params string[] allowed)
    {
        string? unknown = request.Parameters.Keys.FirstOrDefault(k => !allowed.Contains(k, StringComparer.Ordinal));
        return unknown is null ? Ok : BulkOperationCheckResult.Invalid($"parameters.{unknown}", $"The {Type} operation does not take the parameter '{unknown}'.");
    }
}
