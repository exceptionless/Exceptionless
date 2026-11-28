using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>Marks stacks fixed, optionally in a version, exactly like <c>POST /stacks/{ids}/mark-fixed</c>.</summary>
public class MarkStacksFixedBulkOperationHandler(IStackRepository stackRepository, StackActionService stackActions) : StackBulkOperationHandler(stackRepository, stackActions)
{
    public const string VersionParameter = "version";

    public override string Type => "stacks.mark-fixed";

    public override Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request)
    {
        var unknown = RejectUnknownParameters(request, VersionParameter);
        if (!unknown.IsOk)
        {
            return Task.FromResult(unknown);
        }

        request.Parameters.TryGetValue(VersionParameter, out string? version);
        if (!StackActions.TryParseVersion(version, out _))
        {
            return Task.FromResult(BulkOperationCheckResult.Invalid($"parameters.{VersionParameter}", "Invalid semantic version"));
        }

        return Task.FromResult(Ok);
    }

    public override string Describe(BackgroundTask task)
    {
        task.Bulk!.Parameters.TryGetValue(VersionParameter, out string? version);
        string suffix = String.IsNullOrEmpty(version) ? String.Empty : $" in {version}";
        return $"Mark {Subject(task)} as fixed{suffix}";
    }

    protected override Task ApplyAsync(BackgroundTask task, IReadOnlyCollection<Stack> stacks)
    {
        task.Bulk!.Parameters.TryGetValue(VersionParameter, out string? version);
        StackActions.TryParseVersion(version, out var semanticVersion);
        return StackActions.MarkFixedAsync(stacks, semanticVersion);
    }
}
