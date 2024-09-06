using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Options;
using Microsoft.Extensions.Logging;
using Nest;

namespace Exceptionless.Core.Repositories;

public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository
{
    private readonly TimeProvider _timeProvider;
    private const string STACKING_VERSION = "v2";

    public StackRepository(ExceptionlessElasticConfiguration configuration, IValidator<Stack> validator, AppOptions options)
        : base(configuration.Stacks, validator, options)
    {
        _timeProvider = configuration.TimeProvider;
        AddPropertyRequiredForRemove(s => s.SignatureHash);
    }

    public Task<FindResults<Stack>> GetExpiredSnoozedStatuses(DateTime utcNow, CommandOptionsDescriptor<Stack>? options = null)
    {
        return FindAsync(q => q.ElasticFilter(Query<Stack>.DateRange(d => d.Field(f => f.SnoozeUntilUtc).LessThanOrEquals(utcNow))), options);
    }

    public Task<FindResults<Stack>> GetStacksForCleanupAsync(string organizationId, DateTime cutoff)
    {
        return FindAsync(q => q
            .Organization(organizationId)
            .ElasticFilter(Query<Stack>.DateRange(d => d.Field(f => f.LastOccurrence).LessThanOrEquals(cutoff)))
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
        // Only update the LastOccurrence if the new date is greater then the existing date.
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

        var request = new UpdateRequest<Stack, Stack>(ElasticIndex.GetIndex(stackId), stackId)
        {
            Script = new InlineScript(script.TrimScript())
            {
                Params = new Dictionary<string, object>(3) {
                        { "minOccurrenceDateUtc", minOccurrenceDateUtc },
                        { "maxOccurrenceDateUtc", maxOccurrenceDateUtc },
                        { "count", count },
                        { "updatedUtc", _timeProvider.GetUtcNow().UtcDateTime }
                    }
            }
        };

        var result = await _client.UpdateAsync(request);
        if (!result.IsValid)
        {
            _logger.LogError(result.OriginalException, "Error occurred incrementing total event occurrences on stack {Stack}. Error: {Message}", stackId, result.ServerError?.Error);
            return result.ServerError?.Status == 404;
        }

        await Cache.RemoveAsync(stackId);
        if (sendNotifications)
            await PublishMessageAsync(CreateEntityChanged(ChangeType.Saved, organizationId, projectId, null, stackId), TimeSpan.FromSeconds(1.5));

        return true;
    }

    public async Task<Stack?> GetStackBySignatureHashAsync(string projectId, string signatureHash)
    {
        string key = GetStackSignatureCacheKey(projectId, signatureHash);
        var hit = await FindOneAsync(q => q.Project(projectId).ElasticFilter(Query<Stack>.Term(s => s.SignatureHash, signatureHash)), o => o.Cache(key));
        return hit?.Document;
    }

    public Task<FindResults<Stack>> GetIdsByQueryAsync(RepositoryQueryDescriptor<Stack> query, CommandOptionsDescriptor<Stack>? options = null)
    {
        return FindAsync(q => query.Configure().OnlyIds(), options);
    }

    public async Task MarkAsRegressedAsync(string stackId)
    {
        var stack = await GetByIdAsync(stackId);
        stack.Status = StackStatus.Regressed;
        await SaveAsync(stack, o => o.Cache());
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
            cacheEntries.Add(GetStackSignatureCacheKey(hit.Document), hit);

        if (cacheEntries.Count > 0)
            await AddDocumentsToCacheWithKeyAsync(cacheEntries, options.GetExpiresIn());
    }

    protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents, ChangeType? changeType = null)
    {
        var keys = documents.UnionOriginalAndModified().Select(GetStackSignatureCacheKey).Distinct();
        await Cache.RemoveAllAsync(keys);
        await base.InvalidateCacheAsync(documents, changeType);
    }

    private static string GetStackSignatureCacheKey(Stack stack) => GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
    private static string GetStackSignatureCacheKey(string projectId, string signatureHash) => String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
}
