using Exceptionless.Core.Models.WorkItems;
using Exceptionless.Core.Repositories;
using Exceptionless.DateTimeExtensions;
using Foundatio.Jobs;
using Foundatio.Lock;
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

        Log.LogInformation("Fixing stack stats for stacks created between {UtcStart:O} and {UtcEnd:O}", wi.UtcStart, utcEnd);
        await context.ReportProgressAsync(0, $"Starting stack stats repair for window {wi.UtcStart:O} – {utcEnd:O}");

        int pagesProcessed = 0;
        int totalFixed = 0;
        int totalSkipped = 0;

        var results = await _stackRepository.GetByCreatedUtcRangeAsync(wi.UtcStart, utcEnd);
        long totalStacks = results.Total;

        while (results.Documents.Count > 0 && !context.CancellationToken.IsCancellationRequested)
        {
            await context.RenewLockAsync();

            var stackIds = results.Documents.Select(s => s.Id).ToList();
            var statsByStackId = await _eventRepository.GetEventStatsForStacksAsync(stackIds);
            foreach (var stack in results.Documents)
            {
                if (!statsByStackId.TryGetValue(stack.Id, out var stats))
                {
                    totalSkipped++;
                    continue;
                }

                bool shouldUpdateFirst = stack.FirstOccurrence.IsAfter(stats.FirstOccurrence);
                bool shouldUpdateLast = stack.LastOccurrence.IsBefore(stats.LastOccurrence);
                bool shouldUpdateTotal = stats.TotalOccurrences > stack.TotalOccurrences;
                if (!shouldUpdateFirst && !shouldUpdateLast && !shouldUpdateTotal)
                {
                    totalSkipped++;
                    continue;
                }

                DateTime firstOccurrenceToSet = shouldUpdateFirst ? stats.FirstOccurrence : stack.FirstOccurrence;
                DateTime lastOccurrenceToSet = shouldUpdateLast ? stats.LastOccurrence : stack.LastOccurrence;
                long totalOccurrencesToSet = shouldUpdateTotal ? stats.TotalOccurrences : stack.TotalOccurrences;

                Log.LogInformation(
                    "Fixing stack {StackId}: first={OldFirst:O}→{NewFirst:O} last={OldLast:O}→{NewLast:O} total={OldTotal}→{NewTotal}",
                    stack.Id,
                    stack.FirstOccurrence, firstOccurrenceToSet,
                    stack.LastOccurrence, lastOccurrenceToSet,
                    stack.TotalOccurrences, totalOccurrencesToSet);

                await _stackRepository.SetEventCounterAsync(
                    stack.OrganizationId, stack.ProjectId, stack.Id,
                    firstOccurrenceToSet, lastOccurrenceToSet, totalOccurrencesToSet,
                    sendNotifications: false);

                totalFixed++;
            }

            pagesProcessed++;
            int stacksProcessed = totalFixed + totalSkipped;
            int percentage = totalStacks > 0 ? (int)Math.Min(99, stacksProcessed * 100.0 / totalStacks) : (int)Math.Min(99, pagesProcessed * 5);
            Log.LogDebug("Processed page {Page} ({Percentage}%): fixed={Fixed} skipped={Skipped}", pagesProcessed, percentage, totalFixed, totalSkipped);
            await context.ReportProgressAsync(percentage, $"Page {pagesProcessed} ({percentage}%): fixed {totalFixed}, skipped {totalSkipped}");

            if (context.CancellationToken.IsCancellationRequested || !await results.NextPageAsync())
                break;
        }

        Log.LogInformation("Stack stats repair complete. Fixed={Fixed} Skipped={Skipped} Pages={Pages}", totalFixed, totalSkipped, pagesProcessed);
        await context.ReportProgressAsync(100, $"Done. Fixed {totalFixed} stacks, skipped {totalSkipped} stacks across {pagesProcessed} pages.");
    }
}
