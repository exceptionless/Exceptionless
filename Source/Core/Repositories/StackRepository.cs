using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using Exceptionless.Core.Repositories.Queries;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Logging;
using Foundatio.Messaging;
using Foundatio.Repositories;
using Foundatio.Repositories.Elasticsearch.Queries;
using Foundatio.Repositories.Elasticsearch.Queries.Builders;
using Foundatio.Repositories.Models;
using Foundatio.Repositories.Queries;
using Nest;
using SortOrder = Foundatio.Repositories.Models.SortOrder;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : RepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private const string STACKING_VERSION = "v2";
        private readonly IEventRepository _eventRepository;

        public StackRepository(ExceptionlessElasticConfiguration configuration, IEventRepository eventRepository, IValidator<Stack> validator, ICacheClient cache, IMessagePublisher messagePublisher, ILogger<StackRepository> logger) 
            : base(configuration.Client, validator, cache, messagePublisher, logger) {
            ElasticType = configuration.Stacks.Stack;

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
            var result = await _client.UpdateAsync<Stack>(s => s
                .Id(stackId)
                .Index(GetIndexById(stackId))
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
                    Type = ElasticType.Name
                }, TimeSpan.FromSeconds(1.5)).AnyContext();
            }
        }

        public Task<Stack> GetStackBySignatureHashAsync(string projectId, string signatureHash) {
            return FindOneAsync(new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(Filter<Stack>.Term(s => s.SignatureHash, signatureHash))
                .WithCacheKey(GetStackSignatureCacheKey(projectId, signatureHash)));
        }

        public Task<IFindResults<Stack>> GetByFilterAsync(IRepositoryQuery systemFilter, string userFilter, SortingOptions sorting, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (sorting.Fields.Count == 0)
                sorting.Fields.Add(new FieldSort { Field = StackIndexType.Fields.LastOccurrence, Order = SortOrder.Descending });

            var search = new ExceptionlessQuery()
                .WithDateRange(utcStart, utcEnd, field ?? StackIndexType.Fields.LastOccurrence)
                .WithSystemFilter(systemFilter)
                .WithFilter(userFilter)
                .WithPaging(paging)
                .WithSort(sorting);

            return FindAsync(search);
        }

        public Task<IFindResults<Stack>> GetMostRecentAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string filter) {
            var filterContainer = Filter<Stack>.Range(r => r.OnField(s => s.LastOccurrence).GreaterOrEquals(utcStart)) && Filter<Stack>.Range(r => r.OnField(s => s.LastOccurrence).LowerOrEquals(utcEnd));
            var query = new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filterContainer)
                .WithFilter(filter)
                .WithSort(StackIndexType.Fields.LastOccurrence, SortOrder.Descending)
                .WithPaging(paging);

            return FindAsync(query);
        }

        public Task<IFindResults<Stack>> GetNewAsync(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string filter) {
            var filterContainer = Filter<Stack>.Range(r => r.OnField(s => s.FirstOccurrence).GreaterOrEquals(utcStart)) && Filter<Stack>.Range(r => r.OnField(s => s.FirstOccurrence).LowerOrEquals(utcEnd));
            var query = new ExceptionlessQuery()
                .WithProjectId(projectId)
                .WithElasticFilter(filterContainer)
                .WithFilter(filter)
                .WithSort(StackIndexType.Fields.FirstOccurrence, SortOrder.Descending)
                .WithPaging(paging);

            return FindAsync(query);
        }

        public async Task MarkAsRegressedAsync(string stackId) {
            var stack = await GetByIdAsync(stackId).AnyContext();
            stack.IsRegressed = true;
            await SaveAsync(stack, true).AnyContext();
        }

        protected override async Task InvalidateCacheAsync(IReadOnlyCollection<ModifiedDocument<Stack>> documents) {
            if (!IsCacheEnabled)
                return;

            await Cache.RemoveAllAsync(documents.Select(d => d.Value)
                .Union(documents.Select(d => d.Original).Where(d => d != null))
                .Select(GetStackSignatureCacheKey)
                .Distinct()).AnyContext();

            await base.InvalidateCacheAsync(documents).AnyContext();
        }
    }
}
