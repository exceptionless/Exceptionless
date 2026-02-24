using Exceptionless.Core.Models;
using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Jobs.WorkItemHandlers;

public class FixStackStatsWorkItemHandler : WorkItemHandlerBase
{
    private readonly IStackRepository _stackRepository;
    private readonly IEventRepository _eventRepository;
    private readonly ILockProvider _lockProvider;
    private readonly TimeProvider _timeProvider;

    public FixStackStatsWorkItemHandler(IStackRepository stackRepository, IEventRepository eventRepository, ILockProvider lockProvider, TimeProvider timeProvider, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    {
        _stackRepository = stackRepository;
        _eventRepository = eventRepository;
        _lockProvider = lockProvider;
        _timeProvider = timeProvider;
    }

    public override Task<ILock> GetWorkItemLockAsync(object workItem, CancellationToken cancellationToken = default)
    {
        return _lockProvider.AcquireAsync(nameof(FixStackStatsWorkItemHandler), TimeSpan.FromHours(1), cancellationToken);
    }

    public override async Task HandleItemAsync(WorkItemContext context)
    {
        var wi = context.GetData<FixStackStatsWorkItem>();
        var utcEnd = wi.UtcEnd ?? _timeProvider.GetUtcNow().UtcDateTime;

        Log.LogInformation("Starting stack stats repair for {UtcStart:O} to {UtcEnd:O}. OrganizationId={Organization}", wi.UtcStart, utcEnd, wi.Organization);
        await context.ReportProgressAsync(0, $"Starting stack stats repair for window {wi.UtcStart:O} – {utcEnd:O}");

        var organizationIds = await GetOrganizationIdsAsync(wi, utcEnd);
        Log.LogInformation("Found {OrganizationCount} organizations to process", organizationIds.Count);

        int repaired = 0;
        int skipped = 0;

        for (int index = 0; index < organizationIds.Count; index++)
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            var (organizationRepaired, organizationSkipped) = await ProcessOrganizationAsync(context, organizationIds[index], wi.UtcStart, utcEnd);
            repaired += organizationRepaired;
            skipped += organizationSkipped;

            int percentage = (int)Math.Min(99, (index + 1) * 100.0 / organizationIds.Count);
            await context.ReportProgressAsync(percentage, $"Organization {index + 1}/{organizationIds.Count} ({percentage}%): repaired {repaired}, skipped {skipped}");
        }

        Log.LogInformation("Stack stats repair complete: Repaired={Repaired} Skipped={Skipped}", repaired, skipped);
        await context.ReportProgressAsync(100, $"Done. Repaired {repaired} stacks, skipped={skipped}.");
    }

    private async Task<IReadOnlyList<string>> GetOrganizationIdsAsync(FixStackStatsWorkItem wi, DateTime utcEnd)
    {
        if (wi.Organization is not null)
            return [wi.Organization];

        var countResult = await _eventRepository.CountAsync(q => q
            .DateRange(wi.UtcStart, utcEnd, (PersistentEvent e) => e.Date)
            .Index(wi.UtcStart, utcEnd)
            .AggregationsExpression("terms:(organization_id~65536)"));

        return countResult.Aggregations.Terms<string>("terms_organization_id")?.Buckets
            .Select(b => b.Key)
            .ToList() ?? [];
    }

    private async Task<(int Repaired, int Skipped)> ProcessOrganizationAsync(WorkItemContext context, string organizationId, DateTime utcStart, DateTime utcEnd)
    {
        using var _ = Log.BeginScope(new ExceptionlessState().Organization(organizationId));
        await context.RenewLockAsync();

        var countResult = await _eventRepository.CountAsync(q => q
            .Organization(organizationId)
            .DateRange(utcStart, utcEnd, (PersistentEvent e) => e.Date)
            .Index(utcStart, utcEnd)
            .AggregationsExpression("terms:(stack_id~65536 min:date max:date)"));

        var stackBuckets = countResult.Aggregations.Terms<string>("terms_stack_id")?.Buckets ?? [];
        if (stackBuckets.Count is 0)
            return (0, 0);

        var statsByStackId = new Dictionary<string, StackEventStats>(stackBuckets.Count);
        foreach (var bucket in stackBuckets)
        {
            var firstOccurrence = bucket.Aggregations.Min<DateTime>("min_date")?.Value;
            var lastOccurrence = bucket.Aggregations.Max<DateTime>("max_date")?.Value;
            if (firstOccurrence is null || lastOccurrence is null || bucket.Total is null)
                continue;

            statsByStackId[bucket.Key] = new StackEventStats(firstOccurrence.Value, lastOccurrence.Value, bucket.Total.Value);
        }

        int repaired = 0;
        int skipped = 0;

        foreach (string[] batch in statsByStackId.Keys.Chunk(100))
        {
            if (context.CancellationToken.IsCancellationRequested)
                break;

            await context.RenewLockAsync();

            var stacks = await _stackRepository.GetByIdsAsync(batch);
            foreach (var stack in stacks)
            {
                if (!statsByStackId.TryGetValue(stack.Id, out var stats))
                {
                    skipped++;
                    continue;
                }

                bool shouldUpdateFirst = stack.FirstOccurrence.IsAfter(stats.FirstOccurrence);
                bool shouldUpdateLast = stack.LastOccurrence.IsBefore(stats.LastOccurrence);
                bool shouldUpdateTotal = stats.TotalOccurrences > stack.TotalOccurrences;
                if (!shouldUpdateFirst && !shouldUpdateLast && !shouldUpdateTotal)
                {
                    skipped++;
                    continue;
                }

                var newFirst = shouldUpdateFirst ? stats.FirstOccurrence : stack.FirstOccurrence;
                var newLast = shouldUpdateLast ? stats.LastOccurrence : stack.LastOccurrence;
                long newTotal = shouldUpdateTotal ? stats.TotalOccurrences : stack.TotalOccurrences;

                Log.LogInformation(
                    "Repairing stack {StackId}: first={OldFirst:O}->{NewFirst:O} last={OldLast:O}->{NewLast:O} total={OldTotal}->{NewTotal}",
                    stack.Id,
                    stack.FirstOccurrence, newFirst,
                    stack.LastOccurrence, newLast,
                    stack.TotalOccurrences, newTotal);

                await _stackRepository.SetEventCounterAsync(stack.Id, newFirst, newLast, newTotal, sendNotifications: false);
                repaired++;
            }
        }

        Log.LogDebug("Processed organization: Repaired={Repaired} Skipped={Skipped}", repaired, skipped);
        return (repaired, skipped);
    }
}

internal record StackEventStats(DateTime FirstOccurrence, DateTime LastOccurrence, long TotalOccurrences);
