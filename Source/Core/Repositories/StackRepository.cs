using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories.Configuration;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Messaging;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : ElasticSearchRepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private const string STACKING_VERSION = "v2";
        private readonly IEventRepository _eventRepository;

        public StackRepository(IElasticClient elasticClient, StackIndex index, IEventRepository eventRepository, IValidator<Stack> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, index, validator, cacheClient, messagePublisher) {
            _eventRepository = eventRepository;
            DocumentChanging += OnDocumentChanging;
            DocumentChanged += OnDocumentChanged;
        }

        private void OnDocumentChanging(object sender, DocumentChangeEventArgs<Stack> args) {
            if (args.ChangeType != ChangeType.Removed)
                return;

            foreach (Stack document in args.Documents) {
                if (_eventRepository.GetCountByStackId(document.Id) > 0)
                    throw new ApplicationException($"Stack \"{document.Id}\" can't be deleted because it has events associated to it.");
            }
        }

        private void OnDocumentChanged(object sender, DocumentChangeEventArgs<Stack> args) {
            if (args.ChangeType != ChangeType.Saved)
                return;

            foreach (var original in args.OriginalDocuments) {
                var updated = args.Documents.First(d => d.Id == original.Id);
                if (original.DateFixed != updated.DateFixed)
                    _eventRepository.UpdateFixedByStack(updated.OrganizationId, updated.Id, updated.DateFixed.HasValue);

                if (original.IsHidden != updated.IsHidden)
                    _eventRepository.UpdateHiddenByStack(updated.OrganizationId, updated.Id, updated.IsHidden);
            }
        }

        protected override void AddToCache(ICollection<Stack> documents, TimeSpan? expiresIn = null) {
            base.AddToCache(documents, expiresIn);
            foreach (var stack in documents)
                Cache.Set(GetScopedCacheKey(GetStackSignatureCacheKey(stack)), stack, expiresIn.HasValue ? expiresIn.Value : TimeSpan.FromSeconds(RepositoryConstants.DEFAULT_CACHE_EXPIRATION_SECONDS));
        }

        private string GetStackSignatureCacheKey(Stack stack) {
            return GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
        }

        private string GetStackSignatureCacheKey(string projectId, string signatureHash) {
            return String.Concat(projectId, "-", signatureHash, "-", STACKING_VERSION);
        }

        public void IncrementEventCounter(string organizationId, string projectId, string stackId, DateTime minOccurrenceDateUtc, DateTime maxOccurrenceDateUtc, int count, bool sendNotifications = true) {
            // If total occurrences are zero (stack data was reset), then set first occurrence date
            // Only update the LastOccurrence if the new date is greater then the existing date.
            var result = _elasticClient.Update<Stack>(s => s
                .Id(stackId)
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
                    .Add("count", count)));
            
            if (!result.IsValid) {
                Log.Error().Message("Error occurred incrementing total event occurrences on stack \"{0}\". Error: {1}", stackId, result.ServerError.Error).Write();
                return;
            }

            InvalidateCache(stackId);

            if (sendNotifications) {
                PublishMessage(new EntityChanged {
                    ChangeType = ChangeType.Saved,
                    Id = stackId,
                    OrganizationId = organizationId,
                    ProjectId = projectId,
                    Type = _entityType
                }, TimeSpan.FromSeconds(1.5));
            }
        }

        public Stack GetStackBySignatureHash(string projectId, string signatureHash) {
            return FindOne(new ElasticSearchOptions<Stack>()
                .WithProjectId(projectId)
                .WithFilter(Filter<Stack>.Term(s => s.SignatureHash, signatureHash))
                .WithCacheKey(GetStackSignatureCacheKey(projectId, signatureHash)));
        }

        public FindResults<Stack> GetByFilter(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            if (String.IsNullOrEmpty(sort)) {
                sort = "last";
                sortOrder = SortOrder.Descending;
            }

            var search = new ElasticSearchOptions<Stack>()
                .WithDateRange(utcStart, utcEnd, field ?? "last")
                .WithFilter(!String.IsNullOrEmpty(systemFilter) ? Filter<Stack>.Query(q => q.QueryString(qs => qs.DefaultOperator(Operator.And).Query(systemFilter))) : null)
                .WithQuery(userFilter)
                .WithPaging(paging)
                .WithSort(e => e.OnField(sort).Order(sortOrder == SortOrder.Descending ? Nest.SortOrder.Descending : Nest.SortOrder.Ascending));

            return Find(search);
        }

        public FindResults<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string query) {
            var options = new ElasticSearchOptions<Stack>().WithProjectId(projectId).WithQuery(query).WithSort(s => s.OnField(e => e.LastOccurrence).Descending()).WithPaging(paging);
            options.Filter = Filter<Stack>.Range(r => r.OnField(s => s.LastOccurrence).GreaterOrEquals(utcStart));
            options.Filter &= Filter<Stack>.Range(r => r.OnField(s => s.LastOccurrence).LowerOrEquals(utcEnd));

            return Find(options);
        }

        public FindResults<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string query) {
            var options = new ElasticSearchOptions<Stack>().WithProjectId(projectId).WithQuery(query).WithSort(s => s.OnField(e => e.FirstOccurrence).Descending()).WithPaging(paging);
            options.Filter = Filter<Stack>.Range(r => r.OnField(s => s.FirstOccurrence).GreaterOrEquals(utcStart));
            options.Filter &= Filter<Stack>.Range(r => r.OnField(s => s.FirstOccurrence).LowerOrEquals(utcEnd));

            return Find(options);
        }

        public void MarkAsRegressed(string stackId) {
            var stack = GetById(stackId);
            stack.DateFixed = null;
            stack.IsRegressed = true;
            Save(stack, true);
        }

        protected override void InvalidateCache(ICollection<Stack> stacks, ICollection<Stack> originalStacks) {
            if (!EnableCache)
                return;

            stacks.ForEach(s => InvalidateCache(GetStackSignatureCacheKey(s)));
            base.InvalidateCache(stacks, originalStacks);
        }

        public void InvalidateCache(string projectId, string stackId, string signatureHash) {
            InvalidateCache(stackId);
            InvalidateCache(GetStackSignatureCacheKey(projectId, signatureHash));
        }
    }
}