using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>Deletes stacks and, through the cleanup job, their events, exactly like <c>DELETE /stacks/{ids}</c>.</summary>
public class DeleteStacksBulkOperationHandler(IStackRepository stackRepository, StackActionService stackActions) : StackBulkOperationHandler(stackRepository, stackActions)
{
    public override string Type => "stacks.delete";
    public override bool IsDestructive => true;

    public override Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request)
    {
        if (request.Parameters.Count > 0)
        {
            return Task.FromResult(BulkOperationCheckResult.Invalid("parameters", $"The {Type} operation does not take parameters."));
        }

        return Task.FromResult(Ok);
    }

    public override string Describe(BackgroundTask task) => $"Delete {Subject(task)}";

    protected override Task ApplyAsync(BackgroundTask task, IReadOnlyCollection<Stack> stacks) => StackActions.DeleteAsync(stacks);
}
