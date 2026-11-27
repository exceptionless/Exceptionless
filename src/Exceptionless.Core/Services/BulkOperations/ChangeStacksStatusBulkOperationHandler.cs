using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>Changes the status of stacks to open, ignored or discarded, exactly like <c>POST /stacks/{ids}/change-status</c>.</summary>
public class ChangeStacksStatusBulkOperationHandler(IStackRepository stackRepository, StackActionService stackActions) : StackBulkOperationHandler(stackRepository, stackActions)
{
    public const string StatusParameter = "status";

    private static readonly Dictionary<string, StackStatus> _statuses = new(StringComparer.Ordinal)
    {
        [Stack.KnownStatuses.Open] = StackStatus.Open,
        [Stack.KnownStatuses.Ignored] = StackStatus.Ignored,
        [Stack.KnownStatuses.Discarded] = StackStatus.Discarded
    };

    public override string Type => "stacks.change-status";

    public override Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request)
    {
        var unknown = RejectUnknownParameters(request, StatusParameter);
        if (!unknown.IsOk)
            return Task.FromResult(unknown);

        if (!request.Parameters.TryGetValue(StatusParameter, out string? value) || !_statuses.TryGetValue(value, out var status) || !StackActionService.CanChangeStatusTo(status))
            return Task.FromResult(BulkOperationCheckResult.Invalid($"parameters.{StatusParameter}", "The status must be open, ignored or discarded."));

        return Task.FromResult(Ok);
    }

    public override string Describe(BackgroundTask task)
    {
        task.Bulk!.Parameters.TryGetValue(StatusParameter, out string? value);
        return $"Mark {Subject(task)} as {value}";
    }

    protected override Task ApplyAsync(BackgroundTask task, IReadOnlyCollection<Stack> stacks)
    {
        return StackActions.ChangeStatusAsync(stacks, _statuses[task.Bulk!.Parameters[StatusParameter]]);
    }
}
