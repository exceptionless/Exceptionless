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
            q => q
                .FieldEmpty(f => f.RedirectToStackId)
                .Include(f => f.Id, f => f.OrganizationId, f => f.ProjectId, f => f.SignatureHash),
            o => o.SoftDeleteMode(SoftDeleteQueryMode.DeletedOnly).SearchAfterPaging().PageLimit(500)
        );
    }

    public Task<FindResults<Stack>> GetRedirectedStacksNeedingReconciliationAsync()
    {
        return FindAsync(
            q => q
                .FieldHasValue(f => f.RedirectToStackId)
                .FieldEquals(f => f.NeedsRedirectReconciliation, true),
            o => o.SoftDeleteMode(SoftDeleteQueryMode.All).SearchAfterPaging().PageLimit(500));
    }

    public override Task<long> RemoveAllByOrganizationIdAsync(string organizationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        return RemoveAllAsync(
            q => q.Organization(organizationId),
            o => o.SoftDeleteMode(SoftDeleteQueryMode.All));
    }

    public override Task<long> RemoveAllByProjectIdAsync(string organizationId, string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        return RemoveAllAsync(
            q => q.Organization(organizationId).Project(projectId),
            o => o.SoftDeleteMode(SoftDeleteQueryMode.All));
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

ctx._source.total_occurrences += params.count;
if (ctx._source.redirect_to_stack_id != null) {
  ctx._source.needs_redirect_reconciliation = true;
}";

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
}

if (ctx._source.redirect_to_stack_id != null) {
    ctx._source.needs_redirect_reconciliation = true;
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

    public async Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash)
    {
        string key = GetStackSignatureCacheKey(projectId, signatureHash);
        var hit = await FindOneAsync(q => q.Project(projectId).FieldEquals(s => s.SignatureHash, signatureHash), o => o.Cache(key));
        return hit?.Document is null ? null : await ResolveCanonicalStackAsync(hit.Document);
    }

    public async Task<Stack?> GetCanonicalStackAsync(string stackId)
    {
        ArgumentException.ThrowIfNullOrEmpty(stackId);

        var stack = await GetByIdAsync(stackId, o => o.Cache().SoftDeleteMode(SoftDeleteQueryMode.All));
        return stack is null ? null : await ResolveCanonicalStackAsync(stack);
    }

    public Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null)
    {
        return FindAsync(q => query.Configure().OnlyIds(), options);
    }

    public async Task MarkAsRegressedAsync(string stackId)
    {
        try
        {
            await PatchAsync(
                stackId,
                new ActionPatch<Stack>(stack =>
                {
                    if (stack.Status == StackStatus.Regressed)
                        return false;

                    stack.Status = StackStatus.Regressed;
                    return true;
                }),
                o => o.Retry(10));
        }
        catch (DocumentNotFoundException)
        {
            _logger.LogWarning("Stack {StackId} not found when marking as regressed", stackId);
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

    public async Task<IReadOnlyCollection<string>> GetDuplicateSignaturesAsync(int maxResults = 10000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxResults);

        // ImmediateConsistency forces a segment refresh before the aggregation so that
        // any stacks soft-deleted in a previous batch are excluded here. Cost: one refresh
        // per batch (not per item), equivalent to the original explicit index refresh.
        var result = await CountAsync(
            q => q.AggregationsExpression($"terms:(duplicate_signature~{maxResults} @min:2)"),
            o => o.ImmediateConsistency());

        var buckets = result.Aggregations.Terms("terms_duplicate_signature")?.Buckets;
        if (buckets is not { Count: > 0 })
            return [];

        return buckets.Select(b => b.Key).ToArray();
    }

    public async Task<bool> AddEventTagsAsync(string stackId, IEnumerable<string?> tags)
    {
        ArgumentException.ThrowIfNullOrEmpty(stackId);
        ArgumentNullException.ThrowIfNull(tags);
        var tagsToAdd = tags.ToArray();

        string? redirectToStackId = null;
        bool modified = await PatchAsync(
            stackId,
            new ActionPatch<Stack>(stack =>
            {
                if (!String.IsNullOrEmpty(stack.RedirectToStackId))
                {
                    redirectToStackId = stack.RedirectToStackId;
                    return false;
                }

                stack.Tags ??= new TagSet();
                var originalTags = new TagSet(stack.Tags);
                stack.Tags.UnionWith(tagsToAdd);
                stack.Tags.RemoveExcessTags();
                return !stack.Tags.SetEquals(originalTags);
            }),
            o => o.Notifications(false).Retry(10));

        if (String.IsNullOrEmpty(redirectToStackId))
            return modified;

        var canonicalStack = await GetCanonicalStackAsync(redirectToStackId);
        return canonicalStack is not null && await AddEventTagsAsync(canonicalStack.Id, tagsToAdd);
    }

    public Task<long> MarkOpenAsync(IEnumerable<string> stackIds)
    {
        ArgumentNullException.ThrowIfNull(stackIds);
        var ids = new Ids(stackIds.Distinct(StringComparer.Ordinal));
        if (ids.Count == 0)
            return Task.FromResult(0L);

        return PatchAsync(
            ids,
            new ActionPatch<Stack>(stack =>
            {
                if (stack is { Status: StackStatus.Open, DateFixed: null, FixedInVersion: null, SnoozeUntilUtc: null })
                    return false;

                stack.MarkOpen();
                return true;
            }),
            o => o.Retry(10));
    }

    public async Task SetDuplicateStackRedirectAsync(Stack sourceStack, string targetStackId, bool isDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(sourceStack);
        ArgumentException.ThrowIfNullOrEmpty(sourceStack.Id);
        ArgumentException.ThrowIfNullOrEmpty(targetStackId);
        if (String.Equals(sourceStack.Id, targetStackId, StringComparison.Ordinal))
            throw new ArgumentException("Source and target stack ids must be different.", nameof(targetStackId));

        var canonicalTarget = await GetCanonicalStackAsync(targetStackId)
            ?? throw new DocumentNotFoundException(targetStackId);
        if (String.Equals(sourceStack.Id, canonicalTarget.Id, StringComparison.Ordinal))
            throw new ArgumentException("A stack redirect cannot create a cycle.", nameof(targetStackId));

        targetStackId = canonicalTarget.Id;

        if (isDeleted)
        {
            const string finalizeScript = @"
ctx._source.redirect_to_stack_id = params.targetStackId;
ctx._source.is_deleted = true;
ctx._source.needs_redirect_reconciliation = true;
ctx._source.title = '';
ctx._source.remove('description');
ctx._source.signature_info = new HashMap();
ctx._source.tags = new ArrayList();
ctx._source.references = new ArrayList();
ctx._source.remove('fixed_in_version');";

            await PatchAsync(
                sourceStack.Id,
                new ScriptPatch(finalizeScript.TrimScript())
                {
                    Params = new Dictionary<string, object> { ["targetStackId"] = targetStackId }
                },
                o => o.Notifications(false).Retry(10));
        }
        else
        {
            await PatchAsync(
                sourceStack.Id,
                new PartialPatch(new { redirect_to_stack_id = targetStackId, is_deleted = false, needs_redirect_reconciliation = true }),
                o => o.ImmediateConsistency().Notifications(false).Retry(10));
        }

        await Cache.RemoveAsync(GetStackSignatureCacheKey(sourceStack));
    }

    public Task MarkDuplicateStackReconciledAsync(Stack sourceStack)
    {
        ArgumentNullException.ThrowIfNull(sourceStack);
        ArgumentException.ThrowIfNullOrEmpty(sourceStack.Id);
        ArgumentException.ThrowIfNullOrEmpty(sourceStack.RedirectToStackId);

        const string script = @"
Instant parseDate(def dt) {
    if (dt != null) {
        try {
            return Instant.parse(dt);
        } catch(DateTimeParseException e) {}
    }
    return Instant.MIN;
}

if (ctx._source.needs_redirect_reconciliation == true
    && ctx._source.total_occurrences == params.expectedTotalOccurrences
    && parseDate(ctx._source.updated_utc).equals(parseDate(params.expectedUpdatedUtc))
    && ctx._source.redirect_to_stack_id == params.expectedTargetStackId) {
    ctx._source.needs_redirect_reconciliation = false;
} else {
    ctx.op = 'noop';
}";

        return PatchAsync(
            sourceStack.Id,
            new ScriptPatch(script.TrimScript())
            {
                Params = new Dictionary<string, object>
                {
                    ["expectedTotalOccurrences"] = sourceStack.TotalOccurrences,
                    ["expectedUpdatedUtc"] = sourceStack.UpdatedUtc,
                    ["expectedTargetStackId"] = sourceStack.RedirectToStackId
                }
            },
            o => o.Notifications(false).Retry(10));
    }

    public async Task<bool> MergeDuplicateStackAsync(string targetStackId, Stack sourceStack)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetStackId);
        ArgumentNullException.ThrowIfNull(sourceStack);
        ArgumentException.ThrowIfNullOrEmpty(sourceStack.Id);
        if (String.Equals(targetStackId, sourceStack.Id, StringComparison.Ordinal))
            throw new ArgumentException("Source and target stack ids must be different.", nameof(sourceStack));

        long nestedOccurrenceTotal = sourceStack.MergedDuplicateStackTotals.Values.Sum(total => (long)total);
        int sourceOccurrenceTotal = (int)Math.Clamp(sourceStack.TotalOccurrences - nestedOccurrenceTotal, 0, Int32.MaxValue);
        var sourceContributions = new Dictionary<string, int>(sourceStack.MergedDuplicateStackTotals, StringComparer.Ordinal)
        {
            [sourceStack.Id] = sourceOccurrenceTotal
        };

        // Track every transitive source contribution independently. This makes retries idempotent,
        // allows late occurrence deltas, and prevents redirect chains from double-counting nested
        // stacks that were already included in the source total.
        const string script = @"
Instant parseDate(def dt) {
    if (dt != null) {
        try {
            return Instant.parse(dt);
        } catch(DateTimeParseException e) {}
    }
    return Instant.MIN;
}

if (ctx._source.merged_duplicate_stack_totals == null) {
    ctx._source.merged_duplicate_stack_totals = new HashMap();
}

def occurrenceDelta = 0;
for (def contribution : params.sourceContributions.entrySet()) {
    def previousContribution = ctx._source.merged_duplicate_stack_totals.containsKey(contribution.getKey())
        ? ctx._source.merged_duplicate_stack_totals[contribution.getKey()]
        : 0;
    if (contribution.getValue() > previousContribution) {
        occurrenceDelta += contribution.getValue() - previousContribution;
    }
}

if (occurrenceDelta <= 0
    && !parseDate(ctx._source.created_utc).isAfter(parseDate(params.createdUtc))
    && !parseDate(ctx._source.last_occurrence).isBefore(parseDate(params.lastOccurrence))
    && !parseDate(ctx._source.snooze_until_utc).isBefore(parseDate(params.snoozeUntilUtc))
    && !parseDate(ctx._source.date_fixed).isBefore(parseDate(params.dateFixed))
    && !(ctx._source.status == 'open' && params.status != 'open')
    && (params.tags == null || ctx._source.tags != null && ctx._source.tags.containsAll(params.tags))
    && (params.references == null || ctx._source.references != null && ctx._source.references.containsAll(params.references))
    && (ctx._source.occurrences_are_critical == true || params.occurrencesAreCritical == false)) {
    ctx.op = 'noop';
} else {
    def safeOccurrenceDelta = occurrenceDelta > 0 ? occurrenceDelta : 0;
    for (def contribution : params.sourceContributions.entrySet()) {
        def previousContribution = ctx._source.merged_duplicate_stack_totals.containsKey(contribution.getKey())
            ? ctx._source.merged_duplicate_stack_totals[contribution.getKey()]
            : 0;
        if (contribution.getValue() > previousContribution) {
            ctx._source.merged_duplicate_stack_totals[contribution.getKey()] = contribution.getValue();
        }
    }
    def currentTotalOccurrences = ctx._source.total_occurrences == null ? 0 : ctx._source.total_occurrences;
    ctx._source.total_occurrences = currentTotalOccurrences + safeOccurrenceDelta;

    if (parseDate(ctx._source.created_utc).isAfter(parseDate(params.createdUtc))) {
        ctx._source.created_utc = params.createdUtc;
    }
    if (parseDate(ctx._source.last_occurrence).isBefore(parseDate(params.lastOccurrence))) {
        ctx._source.last_occurrence = params.lastOccurrence;
    }
    if (parseDate(ctx._source.snooze_until_utc).isBefore(parseDate(params.snoozeUntilUtc))) {
        ctx._source.snooze_until_utc = params.snoozeUntilUtc;
    }
    if (parseDate(ctx._source.date_fixed).isBefore(parseDate(params.dateFixed))) {
        ctx._source.date_fixed = params.dateFixed;
    }
    if (ctx._source.status == 'open' && params.status != 'open') {
        ctx._source.status = params.status;
    }

    if (ctx._source.tags == null) {
        ctx._source.tags = new ArrayList();
    }
    for (int i = 0; i < params.tags.size(); i++) {
        if (!ctx._source.tags.contains(params.tags[i])) {
            ctx._source.tags.add(params.tags[i]);
        }
    }

    if (ctx._source.references == null) {
        ctx._source.references = new ArrayList();
    }
    for (int i = 0; i < params.references.size(); i++) {
        if (!ctx._source.references.contains(params.references[i])) {
            ctx._source.references.add(params.references[i]);
        }
    }

    ctx._source.occurrences_are_critical = ctx._source.occurrences_are_critical == true || params.occurrencesAreCritical;
}";

        var operation = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>
            {
                ["sourceContributions"] = sourceContributions,
                ["createdUtc"] = sourceStack.CreatedUtc,
                ["lastOccurrence"] = sourceStack.LastOccurrence,
                ["snoozeUntilUtc"] = sourceStack.SnoozeUntilUtc ?? DateTime.MinValue,
                ["dateFixed"] = sourceStack.DateFixed ?? DateTime.MinValue,
                ["status"] = sourceStack.Status.ToString().ToLowerInvariant(),
                ["tags"] = sourceStack.Tags.ToArray(),
                ["references"] = sourceStack.References.ToArray(),
                ["occurrencesAreCritical"] = sourceStack.OccurrencesAreCritical
            }
        };

        bool modified = await PatchAsync(targetStackId, operation, o => o.Notifications(false).Retry(10));
        if (!modified)
            return false;

        var targetStack = await GetByIdAsync(targetStackId, o => o.SoftDeleteMode(SoftDeleteQueryMode.All));
        if (targetStack is not null)
            await Cache.RemoveAsync(GetStackSignatureCacheKey(targetStack));

        return true;
    }

    private async Task<Stack?> ResolveCanonicalStackAsync(Stack stack)
    {
        var visitedStackIds = new HashSet<string>(StringComparer.Ordinal) { stack.Id };

        while (!String.IsNullOrEmpty(stack.RedirectToStackId))
        {
            if (!visitedStackIds.Add(stack.RedirectToStackId))
                throw new DocumentException($"Circular stack redirect detected for stack {stack.Id}.");

            stack = await GetByIdAsync(
                stack.RedirectToStackId,
                o => o.Cache().SoftDeleteMode(SoftDeleteQueryMode.All))
                ?? throw new DocumentNotFoundException(stack.RedirectToStackId);
        }

        return stack.IsDeleted ? null : stack;
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

    private static string GetStackSignatureCacheKey(Stack stack) => GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
    private static string GetStackSignatureCacheKey(string projectId, string signatureHash) => String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
}
