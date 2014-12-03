#region Copyright 2014 Exceptionless

// This program is free software: you can redistribute it and/or modify it 
// under the terms of the GNU Affero General Public License as published 
// by the Free Software Foundation, either version 3 of the License, or 
// (at your option) any later version.
// 
//     http://www.gnu.org/licenses/agpl-3.0.html

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Messaging;
using Exceptionless.Core.Messaging.Models;
using Exceptionless.Models;
using FluentValidation;
using Nest;
using NLog.Fluent;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : ElasticSearchRepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private const string STACKING_VERSION = "v2";
        private readonly IEventRepository _eventRepository;

        public StackRepository(IElasticClient elasticClient, IEventRepository eventRepository, IValidator<Stack> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, validator, cacheClient, messagePublisher) {
            _eventRepository = eventRepository;
        }

        protected override void BeforeAdd(ICollection<Stack> documents) {
            // TODO: Remove this dependency on the mongo lib.
            foreach (var ev in documents.Where(ev => ev.Id == null))
                ev.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

            base.BeforeAdd(documents);
        }

        protected override void AfterAdd(ICollection<Stack> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            base.AfterAdd(documents, addToCache, expiresIn);
            if (!addToCache)
                return;

            foreach (var stack in documents)
                Cache.Set(GetScopedCacheKey(GetStackSignatureCacheKey(stack)), stack);
        }

        private string GetStackSignatureCacheKey(Stack stack) {
            return GetStackSignatureCacheKey(stack.ProjectId, stack.SignatureHash);
        }

        private string GetStackSignatureCacheKey(string projectId, string signatureHash) {
            return String.Concat(projectId, "-", signatureHash, "-", STACKING_VERSION);
        }

        public void IncrementEventCounter(string organizationId, string stackId, DateTime occurrenceDateUtc) {
            // If total occurrences are zero (stack data was reset), then set first occurrence date
            // Only update the LastOccurrence if the new date is greater then the existing date.
            var result = _elasticClient.Update<Stack>(s => s
                .Id(stackId)
                .Script(@"if (ctx._source.total_occurrences == 0 || ctx._source.first_occurrence > occurrenceDateUtc) {
                            ctx._source.first_occurrence = occurrenceDateUtc;
                          }
                          if (ctx._source.last_occurrence < occurrenceDateUtc) {
                            ctx._source.last_occurrence = occurrenceDateUtc;
                          }
                          ctx._source.total_occurrences += 1;")
                .Params(p => p.Add("occurrenceDateUtc", occurrenceDateUtc)));
            
            if (!result.IsValid) {
                Log.Error().Message("Error occurred incrementing stack count.");
                return;
            }

            //Trace.WriteLine(String.Format("Incr: {0}", stackId));
            InvalidateCache(stackId);

            if (EnableNotifications) {
                PublishMessage(new EntityChanged {
                    ChangeType = EntityChangeType.Saved,
                    Id = stackId,
                    OrganizationId = organizationId,
                    Type = _entityType
                });
            }
        }

        public Stack GetStackBySignatureHash(string projectId, string signatureHash) {
            return FindOne(new ElasticSearchOptions<Stack>()
                .WithProjectId(projectId)
                .WithFilter(Filter<Stack>.Term(s => s.SignatureHash, signatureHash))
                .WithCacheKey(GetStackSignatureCacheKey(projectId, signatureHash)));
        }

        public ICollection<Stack> GetByFilter(string systemFilter, string userFilter, string sort, SortOrder sortOrder, string field, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
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

        public ICollection<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string query) {
            var options = new ElasticSearchOptions<Stack>().WithProjectId(projectId).WithQuery(query).WithSort(s => s.OnField(e => e.LastOccurrence).Descending()).WithPaging(paging);
            options.Filter = Filter<Stack>.Range(r => r.OnField(s => s.LastOccurrence).GreaterOrEquals(utcStart));
            options.Filter &= Filter<Stack>.Range(r => r.OnField(s => s.LastOccurrence).LowerOrEquals(utcEnd));

            return Find(options);
        }

        public ICollection<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, string query) {
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

        public override void InvalidateCache(Stack entity) {
            if (Cache == null)
                return;

            var originalStack = GetById(entity.Id, true);
            if (originalStack != null) {
                if (originalStack.DateFixed != entity.DateFixed)
                    _eventRepository.UpdateFixedByStack(entity.OrganizationId, entity.Id, entity.DateFixed.HasValue);

                if (originalStack.IsHidden != entity.IsHidden)
                    _eventRepository.UpdateHiddenByStack(entity.OrganizationId, entity.Id, entity.IsHidden);

                InvalidateCache(GetStackSignatureCacheKey(entity));
            }

            base.InvalidateCache(entity);
        }

        public void InvalidateCache(string projectId, string stackId, string signatureHash) {
            InvalidateCache(stackId);
            InvalidateCache(GetStackSignatureCacheKey(projectId, signatureHash));
        }

        protected override void AfterRemove(ICollection<Stack> documents, bool sendNotification = true) {
            foreach (Stack document in documents)
                InvalidateCache(GetStackSignatureCacheKey(document));

            base.AfterRemove(documents, sendNotification);
        }
    }
}