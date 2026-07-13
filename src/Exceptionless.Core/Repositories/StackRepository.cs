using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Validation;
using Foundatio.Repositories;
using Foundatio.Repositories.Exceptions;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;
using Foundatio.Caching;
using Exceptionless.Core.Services;

namespace Exceptionless.Core.Repositories;

public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository
{
    private readonly TimeProvider _timeProvider;
    private readonly AppOptions _appOptions;
    private readonly IStackRouteCache _stackRouteCache;
    private const string STACKING_VERSION = "v2";

    public StackRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options, IStackRouteCache stackRouteCache)
        : base(configuration.Stacks, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        _appOptions = options;
        _stackRouteCache = stackRouteCache;
        // Both the legacy signature cache and the V3 route cache must invalidate the previous
        // key when a stack's signature changes or it is soft-deleted.
        OriginalsEnabled = true;
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

    public async Task ApplyIngestionStackUsageAsync(
        string organizationId,
        string projectId,
        string stackId,
        DateTime minOccurrenceDateUtc,
        DateTime maxOccurrenceDateUtc,
        int count,
        long settlementSequence,
        bool sendNotifications = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settlementSequence);

        const string script = @"
Instant parseDate(def dt) {
  if (dt != null) {
    try {
      return Instant.parse(dt);
    } catch(DateTimeParseException e) {}
  }
  return Instant.MIN;
}

long appliedSequence = ctx._source.ingestion_stack_usage_sequence == null
  ? 0
  : ctx._source.ingestion_stack_usage_sequence;
if (appliedSequence >= params.settlementSequence) {
  ctx.op = 'noop';
} else {
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
  ctx._source.ingestion_stack_usage_sequence = params.settlementSequence;
}";

        var operation = new ScriptPatch(script.TrimScript())
        {
            Params = new Dictionary<string, object>(5)
            {
                { "minOccurrenceDateUtc", minOccurrenceDateUtc },
                { "maxOccurrenceDateUtc", maxOccurrenceDateUtc },
                { "count", count },
                { "settlementSequence", settlementSequence },
                { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
            }
        };

        bool modified;
        try
        {
            modified = await PatchAsync(stackId, operation, o => o.Notifications(false));
        }
        catch (DocumentNotFoundException)
        {
            return;
        }

        if (modified && sendNotifications)
            await PublishMessageAsync(CreateEntityChanged(ChangeType.Saved, organizationId, projectId, null, stackId));
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

    public async Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash)
    {
        long projectGeneration = await _stackRouteCache.GetProjectGenerationAsync(projectId);
        string key = GetStackSignatureCacheKey(projectId, signatureHash, projectGeneration);
        var hit = await FindOneAsync(q => q.Project(projectId).FieldEquals(s => s.SignatureHash, signatureHash), o => o.Cache(key));
        return hit?.Document;
    }

    public async Task<IReadOnlyDictionary<string, StackRoute>> GetStackRoutesBySignatureHashAsync(string projectId, IReadOnlyCollection<string> signatureHashes)
    {
        if (signatureHashes.Count == 0)
            return new Dictionary<string, StackRoute>();

        var results = await FindAsync(
            q => q.Project(projectId).SignatureHash(signatureHashes).Include(
                s => s.Id,
                s => s.SignatureHash,
                s => s.Status,
                s => s.UpdatedUtc,
                s => s.FixedInVersion,
                s => s.DateFixed,
                s => s.OccurrencesAreCritical,
                s => s.RegressionEventId,
                s => s.IngestionFirstEventId),
            o => o.PageLimit(signatureHashes.Count));

        var routes = new Dictionary<string, StackRoute>(results.Documents.Count);
        foreach (var stack in results.Documents)
        {
            if (!String.IsNullOrEmpty(stack.SignatureHash))
                routes[stack.SignatureHash] = StackRouteResolver.CreateRoute(stack);
        }

        return routes;
    }

    public Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null)
    {
        return FindAsync(q => query.Configure().OnlyIds(), options);
    }

    public async Task MarkAsRegressedAsync(string stackId)
    {
        var stack = await GetByIdAsync(stackId);
        if (stack is null)
        {
            _logger.LogWarning("Stack {StackId} not found when marking as regressed", stackId);
            return;
        }

        stack.Status = StackStatus.Regressed;
        await SaveAsync(stack, o => o.Cache());
    }

    public async Task<long> SoftDeleteByProjectIdAsync(string organizationId, string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(organizationId);
        ArgumentException.ThrowIfNullOrEmpty(projectId);

        long deleted = await PatchAllAsync(
            q => q.Organization(organizationId).Project(projectId),
            new PartialPatch(new { is_deleted = true, updated_utc = _timeProvider.GetUtcNow().UtcDateTime }),
            o => o.ImmediateConsistency()
        );
        // The same generation scopes both V3 route entries and legacy signature lookups. Any
        // lookup that began before deletion can only refill the old namespace and is invisible
        // after the durable, immediately-consistent patch advances it.
        await _stackRouteCache.AdvanceProjectGenerationAsync(projectId);
        return deleted;
    }

    protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<Stack>> findHits, ICommandOptions options, bool isDirtyRead)
    {
        await base.AddDocumentsToCacheAsync(findHits, options, isDirtyRead);

        Stack[] signatureStacks = findHits
            .Select(hit => hit.Document)
            .Where(stack => stack is not null && !String.IsNullOrEmpty(stack.ProjectId) && !String.IsNullOrEmpty(stack.SignatureHash))
            .Cast<Stack>()
            .ToArray();
        var signatureGenerations = await GetProjectGenerationsAsync(signatureStacks);
        var cacheEntries = new Dictionary<string, FindHit<Stack>>();
        foreach (var hit in findHits.Where(d => !String.IsNullOrEmpty(d.Document?.SignatureHash)))
        {
            Stack stack = hit.Document!;
            cacheEntries[GetStackSignatureCacheKey(
                stack.ProjectId,
                stack.SignatureHash,
                signatureGenerations[stack.ProjectId])] = hit;
        }

        if (cacheEntries.Count > 0)
            await AddDocumentsToCacheWithKeyAsync(cacheEntries, options.GetExpiresIn());

        // Route resolver misses carry the generation observed before their repository lookup
        // and populate the route cache themselves. Generic repository read-through cannot
        // safely infer that generation, so only authoritative mutations write routes here.
    }

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents, ChangeType? changeType = null)
    {
        Stack[] cacheStacks = documents
            .UnionOriginalAndModified()
            .Where(stack => !String.IsNullOrEmpty(stack.ProjectId) && !String.IsNullOrEmpty(stack.SignatureHash))
            .ToArray();
        var projectGenerations = await GetProjectGenerationsAsync(cacheStacks);
        var keysToRemove = cacheStacks
            .Select(stack => GetStackSignatureCacheKey(
                stack.ProjectId,
                stack.SignatureHash,
                projectGenerations[stack.ProjectId]))
            .Distinct();
        await Cache.RemoveAllAsync(keysToRemove);
        await base.InvalidateCacheAsync(documents, changeType);

        Stack[] routeStacks = cacheStacks;
        if (changeType is ChangeType.Added or ChangeType.Saved)
        {
            var obsoleteRouteKeys = documents
                .SelectMany(document => new[] { document.Original, document.Value })
                .Where(stack => stack is not null
                    && !String.IsNullOrEmpty(stack.ProjectId)
                    && !String.IsNullOrEmpty(stack.SignatureHash))
                .Select(stack => new
                {
                    Stack = stack!,
                    Key = GetStackRouteCacheKey(
                        stack!.ProjectId,
                        stack.SignatureHash,
                        projectGenerations[stack.ProjectId])
                })
                .Where(item => item.Stack.IsDeleted || !documents.Any(document =>
                    !document.Value.IsDeleted
                    && !String.IsNullOrEmpty(document.Value.ProjectId)
                    && !String.IsNullOrEmpty(document.Value.SignatureHash)
                    && GetStackRouteCacheKey(
                        document.Value.ProjectId,
                        document.Value.SignatureHash,
                        projectGenerations[document.Value.ProjectId]) == item.Key))
                .Select(item => item.Key)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (obsoleteRouteKeys.Length > 0)
                await _stackRouteCache.RemoveAllAsync(obsoleteRouteKeys, _appOptions.EventIngestionV3.StackRouteCacheDuration);

            var routeEntries = new Dictionary<string, StackRouteCacheEntry>();
            foreach (var stack in documents.Select(d => d.Value))
            {
                if (stack.IsDeleted || String.IsNullOrEmpty(stack.ProjectId) || String.IsNullOrEmpty(stack.SignatureHash))
                    continue;

                routeEntries[GetStackRouteCacheKey(
                    stack.ProjectId,
                    stack.SignatureHash,
                    projectGenerations[stack.ProjectId])] = StackRouteCacheEntry.FromRoute(StackRouteResolver.CreateRoute(stack));
            }
            if (routeEntries.Count > 0)
                await _stackRouteCache.SetAllAuthoritativeAsync(routeEntries, _appOptions.EventIngestionV3.StackRouteCacheDuration);
        }
        else if (changeType is ChangeType.Removed)
        {
            string[] routeKeys = routeStacks
                .Select(stack => GetStackRouteCacheKey(
                    stack.ProjectId,
                    stack.SignatureHash,
                    projectGenerations[stack.ProjectId]))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            await _stackRouteCache.RemoveAllAsync(routeKeys, _appOptions.EventIngestionV3.StackRouteCacheDuration);
        }
    }

    private async Task<IReadOnlyDictionary<string, long>> GetProjectGenerationsAsync(IEnumerable<Stack> stacks)
    {
        string[] projectIds = stacks
            .Select(stack => stack.ProjectId)
            .Where(projectId => !String.IsNullOrEmpty(projectId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var generations = await Task.WhenAll(projectIds.Select(async projectId => new
        {
            ProjectId = projectId,
            Generation = await _stackRouteCache.GetProjectGenerationAsync(projectId)
        }));
        return generations.ToDictionary(item => item.ProjectId, item => item.Generation, StringComparer.Ordinal);
    }

    private static string GetStackSignatureCacheKey(string projectId, string signatureHash, long projectGeneration) =>
        String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION, ":", projectGeneration);
    internal static string GetStackRouteCacheKey(string projectId, string signatureHash, long projectGeneration = 0) =>
        StackRouteResolver.GetCacheKey(projectId, signatureHash, projectGeneration);
    internal static string GetStackRouteCachePrefix(string projectId) => String.Concat("stack-route:v3:v1:", projectId, ":");
}
