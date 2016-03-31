using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using Foundatio.Caching;
using Foundatio.Elasticsearch.Repositories;
using Foundatio.Elasticsearch.Repositories.Queries;
using Foundatio.Logging;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private const string STACKING_VERSION = "v2";
        private readonly IEventRepository _eventRepository;

        public StackRepository(ElasticRepositoryContext<Stack> context, StackIndex index, IEventRepository eventRepository, ILoggerFactory loggerFactory = null) : base(context, index, loggerFactory) {
            _eventRepository = eventRepository;
            DocumentsChanging.AddHandler(OnDocumentChangingAsync);
        }

        private async Task OnDocumentChangingAsync(object sender, DocumentsChangeEventArgs<Stack> args) {
            if (args.ChangeType != ChangeType.Removed)
                return;

            foreach (var document in args.Documents) {
                if (await _eventRepository.GetCountByStackIdAsync(document.Value.Id).AnyContext() > 0)
                    throw new ApplicationException($"Stack \"{document.Value.Id}\" can't be deleted because it has events associated to it.");
            }
        }

        protected override async Task AddToCacheAsync(ICollection<Stack> documents, TimeSpan? expiresIn = null) {
            if (!IsCacheEnabled)
                return;

            await base.AddToCacheAsync(documents, expiresIn).AnyContext();
            foreach (var stack in documents)
                await Cache.SetAsync(GetStackSignatureCacheKey(stack), stack, expiresIn ?? TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS)).AnyContext();
        }

        private string GetStackSignatureCacheKey(Stack stack) {
            return GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
        }

        private string GetStackSignatureCacheKey(string projectId, string signatureHash) {
            return String.Concat(projectId, ":", signatureHash, ":", STACKING_VERSION);
        }

        public async Task IncrementEventCounterAsync(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true) {
            // If total occurrences are zero (stack data was reset), then set first occurrence date
            // Only update the LastOccurrence if the new date is greater then the existing date.
            var result = await Context.ElasticClient.UpdateAsync<Stack>(stackId, s => s
                .RetryOnConflict(3)
                .Lang("groovy")
                .Script(@"if (ctx._source.total_occurrences == 0 || ctx._source.first_occurrence > minOccurrenceDateUtc) {
                            ctx._source.first_occurrence = minOccurrenceDateUtc;
                          }
                          if (ctx._source.last_occurrence < maxOccurrenceDateUtc) {
                            ctx._source.last_occurrence = maxOccurrenceDateUtc;
                          }
                          ctx._source.total_occurrences += count;")
                .Params(p => p
                    .Add("minOccurrenceDateUtc", minOccurrenceDateUtc)
                    .Add("maxOccurrenceDateUtc", maxOccurrenceDateUtc)
                    .Add("count", count))).AnyContext();

            if (!result.IsValid) {
                _logger.Error("Error occurred incrementing total event occurrences on stack \"{0}\". Error: {1}", stackId, result.ServerError.Error);
                return;
            }

            if (IsCacheEnabled)
                await Cache.RemoveAsync(stackId).AnyContext();

            if (sendNotifications) {
                await PublishMessageAsync(new ExtendedEntityChanged {
                    ChangeType = ChangeType.Saved,
                    Id = stackId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    Type = EntityType
                }, TimeSpan.FromSeconds(1.5)).AnyContext();
            }
        }

        public Task<Stack> GetStackBySignatureHashAsync(string projectId, string signatureHash) {
            return FindOneAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(Query<Stack>.Term(s => s.SignatureHash, signatureHash))
                .WithCacheKey(GetStackSignatureCacheKey(projectId, signatureHash)));
        }

        public Task<FindResults<Stack>> GetByFilterAsync(string systemFilter, string userFilter, SortingOptions sorting, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (sorting.Fields.Count == 0)
                sorting.Fields.Add(new FieldSort { Field = StackIndex.Fields.Stack.LastOccurrence, Order = SortOrder.Descending });

            var search = new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEnd, field ?? StackIndex.Fields.Stack.LastOccurrence)
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging)
                .WithSort(sorting);

            return FindAsync(search);
        }

        public Task<FindResults<Stack>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string filter) {
            var dateFilter = Query<Stack>.DateRange(r => r.Field(s => s.LastOccurrence).GreaterThanOrEquals(utcStart).LessThanOrEquals(utcEnd));
            var query = new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(dateFilter)
                .WithFilter(filter)
                .WithSort(StackIndex.Fields.Stack.LastOccurrence, SortOrder.Descending)
                .WithPaging(paging);

            return FindAsync(query);
        }

        public Task<FindResults<Stack>> GetNewAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string filter) {
            var dateFilter = Query<Stack>.DateRange(r => r.Field(s => s.FirstOccurrence).GreaterThanOrEquals(utcStart).LessThanOrEquals(utcEnd));
            var query = new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(dateFilter)
                .WithFilter(filter)
                .WithSort(StackIndex.Fields.Stack.FirstOccurrence, SortOrder.Descending)
                .WithPaging(paging);

            return FindAsync(query);
        }

        public async Task MarkAsRegressedAsync(string stackId) {
            var stack = await GetByIdAsync(stackId).AnyContext();
            stack.DateFixed = null;
            stack.IsRegressed = true;
            await SaveAsync(stack, true).AnyContext();
        }

        protected override async Task InvalidateCacheAsync(ICollection<ModifiedDocument<Stack>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original).Where(d => d != null))
                .Select(GetStackSignatureCacheKey)
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }

        public async Task InvalidateCacheAsync(string projectId, string stackId, string signatureHash) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAsync(stackId).AnyContext();
            await Cache.RemoveAsync(GetStackSignatureCacheKey(projectId, signatureHash)).AnyContext();
        }
    }
}
