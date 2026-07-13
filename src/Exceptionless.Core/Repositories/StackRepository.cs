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

namespace Exceptionless.Core.Repositories;

public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository
{
    private readonly TimeProvider _timeProvider;
    private readonly AppOptions _appOptions;
    private const string STACKING_VERSION = "v2";

    public StackRepository(ExceptionlessElasticConfiguration configuration, MiniValidationValidator validator, AppOptions options)
        : base(configuration.Stacks, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        _appOptions = options;
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

    public async Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash)
    {
        string key = GetStackSignatureCacheKey(projectId, signatureHash);
        var hit = await FindOneAsync(q => q.Project(projectId).FieldEquals(s => s.SignatureHash, signatureHash), o => o.Cache(key));
        return hit?.Document;
    }

    public async Task<IReadOnlyDictionary<string, StackRoute>> GetStackRoutesBySignatureHashAsync(string projectId, IReadOnlyCollection<string> signatureHashes)
    {
        if (signatureHashes.Count == 0)
            return new Dictionary<string, StackRoute>();

        var results = await FindAsync(
            q => q.Project(projectId).SignatureHash(signatureHashes).Include(s => s.Id, s => s.SignatureHash, s => s.Status),
            o => o.PageLimit(signatureHashes.Count));

        var routes = new Dictionary<string, StackRoute>(results.Documents.Count);
        foreach (var stack in results.Documents)
        {
            if (!String.IsNullOrEmpty(stack.SignatureHash))
                routes[stack.SignatureHash] = new StackRoute(stack.Id, stack.Status);
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
            new PartialPatch(new { is_deleted = true, updated_utc = _timeProvider.GetUtcNow().UtcDateTime })
        );
        await Cache.RemoveByPrefixAsync(GetStackRouteCachePrefix(projectId));
        return deleted;
    }

    protected override async Task AddDocumentsToCacheAsync(ICollection<FindHit<Stack>> findHits, ICommandOptions options, bool isDirtyRead)
    {
        await base.AddDocumentsToCacheAsync(findHits, options, isDirtyRead);

        var cacheEntries = new Dictionary<string, FindHit<Stack>>();
        foreach (var hit in findHits.Where(d => !String.IsNullOrEmpty(d.Document?.SignatureHash)))
            cacheEntries[GetStackSignatureCacheKey(hit.Document!)] = hit;

        if (cacheEntries.Count > 0)
            await AddDocumentsToCacheWithKeyAsync(cacheEntries, options.GetExpiresIn());

        var routeEntries = new Dictionary<string, StackRouteCacheEntry>();
        foreach (var stack in findHits.Select(hit => hit.Document))
        {
            if (stack is null || stack.IsDeleted || String.IsNullOrEmpty(stack.ProjectId) || String.IsNullOrEmpty(stack.SignatureHash))
                continue;

            routeEntries[GetStackRouteCacheKey(stack.ProjectId, stack.SignatureHash)] = new StackRouteCacheEntry(true, stack.Id, stack.Status);
        }
        if (routeEntries.Count > 0)
            await Cache.SetAllAsync(routeEntries, _appOptions.EventIngestionV3.StackRouteCacheDuration);
    }

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents, ChangeType? changeType = null)
    {
        var keysToRemove = documents.UnionOriginalAndModified().Select(GetStackSignatureCacheKey).Distinct();
        await Cache.RemoveAllAsync(keysToRemove);
        var routeKeysToRemove = documents.UnionOriginalAndModified()
            .Where(s => !String.IsNullOrEmpty(s.ProjectId) && !String.IsNullOrEmpty(s.SignatureHash))
            .Select(s => GetStackRouteCacheKey(s.ProjectId, s.SignatureHash))
            .Distinct();
        await Cache.RemoveAllAsync(routeKeysToRemove);
        await base.InvalidateCacheAsync(documents, changeType);

        if (changeType is ChangeType.Added or ChangeType.Saved)
        {
            var routeEntries = new Dictionary<string, StackRouteCacheEntry>();
            foreach (var stack in documents.Select(d => d.Value))
            {
                if (stack.IsDeleted || String.IsNullOrEmpty(stack.ProjectId) || String.IsNullOrEmpty(stack.SignatureHash))
                    continue;

                routeEntries[GetStackRouteCacheKey(stack.ProjectId, stack.SignatureHash)] = new StackRouteCacheEntry(true, stack.Id, stack.Status);
            }
            if (routeEntries.Count > 0)
                await Cache.SetAllAsync(routeEntries, _appOptions.EventIngestionV3.StackRouteCacheDuration);
        }
    }

    private static string GetStackSignatureCacheKey(Stack stack) => GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
    private static string GetStackSignatureCacheKey(string projectId, string signatureHash) => String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
    internal static string GetStackRouteCacheKey(string projectId, string signatureHash) => String.Concat("stack-route:v3:v1:", projectId, ":", signatureHash);
    internal static string GetStackRouteCachePrefix(string projectId) => String.Concat("stack-route:v3:v1:", projectId, ":");
}
