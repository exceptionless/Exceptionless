using System.Globalization;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.BackgroundTasks;
using Exceptionless.Core.Repositories;

namespace Exceptionless.Core.Services.BulkOperations;

/// <summary>Snoozes stacks until a time in the future, exactly like <c>POST /stacks/{ids}/mark-snoozed</c>.</summary>
public class SnoozeStacksBulkOperationHandler(IStackRepository stackRepository, StackActionService stackActions) : StackBulkOperationHandler(stackRepository, stackActions)
{
    public const string SnoozeUntilParameter = "snooze_until_utc";

    public override string Type => "stacks.snooze";

    public override Task<BulkOperationCheckResult> ValidateAsync(BulkOperationRequest request)
    {
        var unknown = RejectUnknownParameters(request, SnoozeUntilParameter);
        if (!unknown.IsOk)
        {
            return Task.FromResult(unknown);
        }

        if (!request.Parameters.TryGetValue(SnoozeUntilParameter, out string? value) || !TryParse(value, out var snoozeUntilUtc))
        {
            return Task.FromResult(BulkOperationCheckResult.Invalid($"parameters.{SnoozeUntilParameter}", "A snooze end time (UTC) is required."));
        }

        string? error = StackActions.ValidateSnooze(snoozeUntilUtc);
        return Task.FromResult(error is null ? Ok : BulkOperationCheckResult.Invalid($"parameters.{SnoozeUntilParameter}", error));
    }

    public override string Describe(BackgroundTask task)
    {
        task.Bulk!.Parameters.TryGetValue(SnoozeUntilParameter, out string? value);
        string suffix = TryParse(value, out var snoozeUntilUtc) ? $" until {snoozeUntilUtc:u}" : String.Empty;
        return $"Snooze {Subject(task)}{suffix}";
    }

    protected override Task ApplyAsync(BackgroundTask task, IReadOnlyCollection<Stack> stacks)
    {
        TryParse(task.Bulk!.Parameters.GetValueOrDefault(SnoozeUntilParameter), out var snoozeUntilUtc);

        return StackActions.SnoozeAsync(stacks, snoozeUntilUtc);
    }

    private static bool TryParse(string? value, out DateTime snoozeUntilUtc)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out snoozeUntilUtc);
    }
}
