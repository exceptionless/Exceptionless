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
        private readonly IEventRepository _eventRepository;

        public StackRepository(ElasticClient elasticClient, IEventRepository eventRepository, IValidator<Stack> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(elasticClient, validator, cacheClient, messagePublisher) {
            _eventRepository = eventRepository;
        }

        protected override void BeforeAdd(ICollection<Stack> documents) {
            // TODO: Remove this dependency on the mongo lib.
            foreach (var ev in documents.Where(ev => ev.Id == null))
                ev.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();

            base.BeforeAdd(documents);
        }

        public void IncrementEventCounter(string stackId, string organizationId, DateTime occurrenceDate) {
            // If total occurrences are zero (stack data was reset), then set first occurrence date
            // Only update the LastOccurrence if the new date is greater then the existing date.
            var result = _elasticClient.Update<Stack>(s => s
                .Id(stackId)
                .Script(@"if (ctx._source.total_occurrences == 0) {
                            ctx._source.first_occurrence = occurrenceDate; 
                          }
                          if (ctx._source.last_occurrence < occurrenceDate) {
                            ctx._source.last_occurrence = occurrenceDate; 
                          }                          
                          ctx._source.total_occurrences++;")
                .Params(p => p.Add("occurrenceDate", occurrenceDate)));

            if (!result.IsValid) {
                Log.Error().Message("Error occurred incrementing stack count.");
                return;
            }

            InvalidateCache(stackId);

            if (EnableNotifications) {
                PublishMessageAsync(new EntityChanged {
                    ChangeType = EntityChangeType.Saved,
                    Id = stackId,
                    OrganizationId = organizationId,
                    Type = _entityType
                });
            }
        }

        public StackInfo GetStackInfoBySignatureHash(string projectId, string signatureHash) {
            return FindOne<StackInfo>(new ElasticSearchOptions<Stack>()
                .WithProjectId(projectId)
                .WithQuery(Query<Stack>.Term(s => s.SignatureHash, signatureHash))
                .WithFields("id", "date_fixed", "occurrences_are_critical", "is_hidden")
                .WithCacheKey(String.Concat(projectId, signatureHash, "v2")));
        }

        public ICollection<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var options = new ElasticSearchOptions<Stack>().WithProjectId(projectId).WithSort(s => s.OnField(e => e.LastOccurrence).Descending()).WithPaging(paging);
            options.Query = Query<Stack>.Range(r => r.OnField(s => s.LastOccurrence).GreaterOrEquals(utcStart));
            options.Query &= Query<Stack>.Range(r => r.OnField(s => s.LastOccurrence).LowerOrEquals(utcEnd));

            if (!includeFixed)
                options.Query &= Query<Stack>.Filtered(f => f.Filter(f1 => f1.Missing(s => s.DateFixed)));

            if (!includeHidden)
                options.Query &= !Query<Stack>.Term(s => s.IsHidden, true);

            if (!includeNotFound)
                options.Query &= Query<Stack>.Filtered(f => f.Filter(f1 => f1.Missing("signature_info.path")));

            return Find<Stack>(options);
        }

        public ICollection<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var options = new ElasticSearchOptions<Stack>().WithProjectId(projectId).WithSort(s => s.OnField(e => e.FirstOccurrence).Descending()).WithPaging(paging);
            options.Query = Query<Stack>.Range(r => r.OnField(s => s.FirstOccurrence).GreaterOrEquals(utcStart));
            options.Query &= Query<Stack>.Range(r => r.OnField(s => s.FirstOccurrence).LowerOrEquals(utcEnd));

            if (!includeFixed)
                options.Query &= Query<Stack>.Filtered(f => f.Filter(f1 => f1.Missing(s => s.DateFixed)));

            if (!includeHidden)
                options.Query &= !Query<Stack>.Term(s => s.IsHidden, true);

            if (!includeNotFound)
                options.Query &= Query<Stack>.Filtered(f => f.Filter(f1 => f1.Missing("signature_info.path")));

            return Find<Stack>(options);
        }

        public void MarkAsRegressed(string stackId, string organizationId) {
            var result = _elasticClient.Update<Stack>(s => s
                .Id(stackId)
                .Script("ctx._source.remove('date_fixed'); ctx._source.is_regressed = true;"));

            if (!result.IsValid) {
                Log.Error().Message("Error occurred marking the stack fixed");
                return;
            }

            InvalidateCache(stackId);

            if (EnableNotifications) {
                PublishMessageAsync(new EntityChanged {
                    ChangeType = EntityChangeType.Saved,
                    Id = stackId,
                    OrganizationId = organizationId,
                    Type = _entityType
                });
            }
        }

        public override void InvalidateCache(Stack entity) {
            if (Cache == null)
                return;

            var originalStack = GetById(entity.Id, true);
            if (originalStack != null) {
                if (originalStack.DateFixed != entity.DateFixed) {
                    _eventRepository.UpdateFixedByStackId(entity.Id, entity.DateFixed.HasValue);
                }

                if (originalStack.IsHidden != entity.IsHidden) {
                    _eventRepository.UpdateHiddenByStackId(entity.Id, entity.IsHidden);
                }

                InvalidateCache(String.Concat(entity.ProjectId, entity.SignatureHash));
            }

            base.InvalidateCache(entity);
        }

        public void InvalidateCache(string id, string signatureHash, string projectId) {
            InvalidateCache(id);
            InvalidateCache(String.Concat(projectId, signatureHash));
        }

        protected override void AfterRemove(ICollection<Stack> documents, bool sendNotification = true) {
            foreach (Stack document in documents)
                InvalidateCache(String.Concat(document.ProjectId, document.SignatureHash));

            base.AfterRemove(documents, sendNotification);
        }
    }
}