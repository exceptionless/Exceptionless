using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Repositories;

public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository
{
    private readonly TimeProvider _timeProvider;
    private const string STACKING_VERSION = "v2";
    private static readonly TimeSpan _claimGuardExpiration = TimeSpan.FromDays(7);

    public StackRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.Stacks, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        AddRequiredField(s => s.SignatureHash);
    }

    public Task<FindResults<Stack>> GetExpiredSnoozedStatuses(DateTime utcNow, CommandOptionsDescriptor<Stack>? options = null)
    {
        return FindAsync(q => q.DateRange(null, utcNow, (Stack s) => s.SnoozeUntilUtc), options);
    }

    public Task<FindResults<Stack>> GetStacksForCleanupAsync(string organizationId, DateTime cutoff)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .DateRange(null, cutoff, (Stack s) => s.LastOccurrence)
            .FieldEquals(f => f.Status, StackStatus.Open)
            .FieldEmpty(f => f.References)
            .Include(f => f.Id, f => f.OrganizationId, f => f.ProjectId, f => f.SignatureHash)
        , o => o.SearchAfterPaging().PageLimit(500));
    }

    public Task<FindResults<Stack>> GetSoftDeleted()
    {
        return FindAsync(
            q => q.Include(f => f.Id, f => f.OrganizationId, f => f.ProjectId, f => f.SignatureHash),
            o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(500)
        );
    }

    public async Task<bool> IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true)
    {
        // If total occurrences are zero (stack data was reset), then set first occurrence date
        // Only update the LastOccurrence if the new date is greater than the existing date.
        const string script = @"
Instant parseDate(def dt) {
  if (dt != null) {
    try {
      return Instant.parse(dt);
    } catch(DateTimeParseException e) {}
  }
  return Instant.MIN;
}

if (ctx._source.total_occurrences == 0 || parseDate(ctx._source.first_occurrence).isAfter(parseDate(params.minOccurrenceDateUtc))) {
  ctx._source.first_occurrence = params.minOccurrenceDateUtc;
}

if (parseDate(ctx._source.last_occurrence).isBefore(parseDate(params.maxOccurrenceDateUtc))) {
  ctx._source.last_occurrence = params.maxOccurrenceDateUtc;
}

if (parseDate(ctx._source.updated_utc).isBefore(parseDate(params.updatedUtc))) {
  ctx._source.updated_utc = params.updatedUtc;
}

ctx._source.total_occurrences += params.count;";

        var operation = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>(4)
            {
                { "minOccurrenceDateUtc", minOccurrenceDateUtc },
                { "maxOccurrenceDateUtc", maxOccurrenceDateUtc },
                { "count", count },
                { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
            }
        };

        try
        {
            bool modified = await PatchAsync(stackId, operation, o => o.Notifications(false));
            if (!modified)
                return false;
        }
        catch (DocumentNotFoundException)
        {
            return true;
        }

        if (sendNotifications)
            await PublishMessageAsync(CreateEntityChanged(ChangeType.Saved, organizationId, projectId, null, stackId));

        return true;
    }

    public async Task<bool> SetEventCounterAsync(string stackId, DateTime firstOccurrenceUtc, DateTime lastOccurrenceUtc, long totalOccurrences, bool sendNotifications = true)
    {
        const string script = @"
Instant parseDate(def dt) {
    if (dt != null) {
        try {
            return Instant.parse(dt);
        } catch(DateTimeParseException e) {}
    }
    return Instant.MIN;
}

if (ctx._source.total_occurrences == null || ctx._source.total_occurrences < params.totalOccurrences) {
    ctx._source.total_occurrences = params.totalOccurrences;
}

if (parseDate(ctx._source.first_occurrence).isAfter(parseDate(params.firstOccurrenceUtc))) {
    ctx._source.first_occurrence = params.firstOccurrenceUtc;
}

if (parseDate(ctx._source.last_occurrence).isBefore(parseDate(params.lastOccurrenceUtc))) {
    ctx._source.last_occurrence = params.lastOccurrenceUtc;
}

if (parseDate(ctx._source.updated_utc).isBefore(parseDate(params.updatedUtc))) {
    ctx._source.updated_utc = params.updatedUtc;
}";

        var operation = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>(4)
            {
                { "firstOccurrenceUtc", firstOccurrenceUtc },
                { "lastOccurrenceUtc", lastOccurrenceUtc },
                { "totalOccurrences", totalOccurrences },
                { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
            }
        };

        try
        {
            return await PatchAsync(stackId, operation, o => o.Notifications(sendNotifications));
        }
        catch (DocumentNotFoundException)
        {
            return true;
        }
    }

    public async Task<bool> TryClaimNotificationAsync(Stack stack, PendingStackNotification kind)
    {
        // The update is applied by Elasticsearch to the current document, so only one caller can see the flag set.
        // Everyone else (and stacks that were never pending) becomes a no-op, which PatchAsync reports as not modified.
        string field = GetPendingField(kind);
        string script = $@"
if (ctx._source.{field} == true) {{
  ctx._source.{field} = false;
}} else {{
  ctx.op = 'noop';
}}";

        try
        {
            bool claimed = await PatchAsync(stack.Id, new ScriptPatch(script.TrimScript()), o => o.Notifications(false));
            if (!claimed)
                return false;

            await InvalidateStackCacheAsync(stack);

            // A whole-document save of a stale copy can put the flag back after it was claimed. Remembering the claim lets the next
            // claim clear the restored flag again without reporting the same notification twice.
            // Delivery is at most once: the flag is already cleared, so if this cache call throws the notification is lost, not repeated.
            return await Cache.AddAsync(GetClaimCacheKey(stack, kind), true, _claimGuardExpiration);
        }
        catch (DocumentNotFoundException)
        {
            return false;
        }
    }

    public Task<bool> TryReserveNotificationAsync(Stack stack, PendingStackNotification kind)
    {
        // Shares the claim key, so a pending flag restored by a concurrent batch can't send the same notification again.
        return Cache.AddAsync(GetClaimCacheKey(stack, kind), true, _claimGuardExpiration);
    }

    public async Task UpdateTagsAsync(Stack stack)
    {
        // Only the tags change, so patch them instead of saving the whole document. A stale copy must not overwrite fields that other
        // events changed while this batch was running, such as a claimed notification.
        try
        {
            await PatchAsync(stack.Id, new PartialPatch(new { tags = stack.Tags }), o => o.Notifications(false));
            await InvalidateStackCacheAsync(stack);
        }
        catch (DocumentNotFoundException)
        {
            _logger.LogWarning("Stack {StackId} not found when updating tags", stack.Id);
        }
    }

    public async Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash)
    {
        string key = GetStackSignatureCacheKey(projectId, signatureHash);
        var hit = await FindOneAsync(q => q.Project(projectId).FieldEquals(s => s.SignatureHash, signatureHash), o => o.Cache(key));
        return hit?.Document;
    }

    public Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null)
    {
        return FindAsync(q => query.Configure().OnlyIds(), options);
    }

    public async Task MarkAsRegressedAsync(Stack stack, bool notificationPending = false)
    {
        // A script update changes only these fields on the current document, so a regression cannot restore or drop a claimed flag.
        // The claim guard key belongs to the regression cycle (see GetClaimCacheKey), so an earlier cycle's key never blocks this one.
        const string script = @"
ctx._source.status = 'regressed';
ctx._source.is_new_notification_pending = false;
ctx._source.is_regression_notification_pending = params.notificationPending;";

        try
        {
            await PatchAsync(stack.Id, new ScriptPatch(script.TrimScript())
            {
                Params = new Dictionary<string, object>(1) { { "notificationPending", notificationPending } }
            });

            await InvalidateStackCacheAsync(stack);
        }
        catch (DocumentNotFoundException)
        {
            _logger.LogWarning("Stack {StackId} not found when marking as regressed", stack.Id);
        }
    }

    public Task<long> SoftDeleteByProjectIdAsync(string organizationId, string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        return PatchAllAsync(
            q => q.Organization(organizationId).Project(projectId),
            new PartialPatch(new { is_deleted = true, updated_utc = _timeProvider.GetUtcNow().UtcDateTime })
        );
    }

    protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<Stack>> findHits, ICommandOptions options, bool isDirtyRead)
    {
        await base.AddDocumentsToCacheAsync(findHits, options, isDirtyRead);

        var cacheEntries = new Dictionary<string, FindHit<Stack>>();
        foreach (var hit in findHits.Where(d => !String.IsNullOrEmpty(d.Document?.SignatureHash)))
            cacheEntries[GetStackSignatureCacheKey(hit.Document!)] = hit;

        if (cacheEntries.Count > 0)
            await AddDocumentsToCacheWithKeyAsync(cacheEntries, options.GetExpiresIn());
    }

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents, ChangeType? changeType = null)
    {
        var keysToRemove = documents.UnionOriginalAndModified().Select(GetStackSignatureCacheKey).Distinct();
        await Cache.RemoveAllAsync(keysToRemove);
        await base.InvalidateCacheAsync(documents, changeType);
    }

    private Task InvalidateStackCacheAsync(Stack stack) => InvalidateCacheAsync([new ModifiedDocument<Stack>(stack, stack)]);

    private static string GetPendingField(PendingStackNotification kind) => kind switch
    {
        PendingStackNotification.New => "is_new_notification_pending",
        PendingStackNotification.Regression => "is_regression_notification_pending",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    // A new notification is pending once per stack. A regression is pending once per fix (a stack regresses again only after it was fixed
    // again), so the fix date identifies the cycle and each cycle gets its own key.
    private static string GetClaimCacheKey(Stack stack, PendingStackNotification kind) => kind == PendingStackNotification.Regression
        ? String.Concat("notification-claim:", stack.Id, ":", GetPendingField(kind), ":", stack.DateFixed?.Ticks ?? 0)
        : String.Concat("notification-claim:", stack.Id, ":", GetPendingField(kind));

    private static string GetStackSignatureCacheKey(Stack stack) => GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
    private static string GetStackSignatureCacheKey(string projectId, string signatureHash) => String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
}
