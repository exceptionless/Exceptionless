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
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Api.Tests.Repositories.InMemory {
    public class StackRepository : MongoRepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;

        public StackRepository(MongoDatabase database, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IEventRepository eventRepository, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, cacheClient, messagePublisher) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
        }

        public void IncrementStats(string stackId, DateTime occurrenceDate) {
            // If total occurrences are zero (stack data was reset), then set first occurrence date
            UpdateAll(new QueryOptions().WithId(stackId).WithQuery(Query.EQ(FieldNames.TotalOccurrences, new BsonInt32(0))), Update.Set(FieldNames.FirstOccurrence, occurrenceDate));

            // Only update the LastOccurrence if the new date is greater then the existing date.
            UpdateBuilder update = Update.Inc(FieldNames.TotalOccurrences, 1).Set(FieldNames.LastOccurrence, occurrenceDate);
            var documentsAffected = UpdateAll(new QueryOptions().WithId(stackId).WithQuery(Query.LT(FieldNames.LastOccurrence, occurrenceDate)), update);
            if (documentsAffected > 0)
                return;

            UpdateAll(new QueryOptions().WithId(stackId), Update.Inc(FieldNames.TotalOccurrences, 1));
            InvalidateCache(stackId);
        }

        public StackInfo GetStackInfoBySignatureHash(string projectId, string signatureHash) {
            return FindOne<StackInfo>(new OneOptions()
                .WithProjectId(projectId)
                .WithQuery(Query.EQ(FieldNames.SignatureHash, signatureHash))
                .WithFields(FieldNames.Id, FieldNames.DateFixed, FieldNames.OccurrencesAreCritical, FieldNames.IsHidden)
                .WithReadPreference(ReadPreference.Primary)
                .WithCacheKey(String.Concat(projectId, signatureHash, "v2")));
        }

        public string[] GetHiddenIds(string projectId) {
            return Find<Stack>(new MultiOptions()
                .WithProjectId(projectId)
                .WithQuery(Query.EQ(FieldNames.IsHidden, BsonBoolean.True))
                .WithFields(FieldNames.Id)
                .WithCacheKey(String.Concat(projectId, "@__HIDDEN")))
                .Select(s => s.Id).ToArray();
        }

        public void InvalidateHiddenIdsCache(string projectId) {
            Cache.Remove(GetScopedCacheKey(String.Concat(projectId, "@__HIDDEN")));
        }

        public string[] GetFixedIds(string projectId) {
            return Find<Stack>(new MultiOptions()
                .WithProjectId(projectId)
                .WithQuery(Query.Exists(FieldNames.DateFixed))
                .WithFields(FieldNames.Id)
                .WithCacheKey(String.Concat(projectId, "@__FIXED")))
                .Select(s => s.Id).ToArray();
        }

        public void InvalidateFixedIdsCache(string projectId) {
            Cache.Remove(GetScopedCacheKey(String.Concat(projectId, "@__FIXED")));
        }

        public string[] GetNotFoundIds(string projectId) {
            return Find<Stack>(new MultiOptions()
                .WithProjectId(projectId)
                .WithQuery(Query.Exists(FieldNames.SignatureInfo_Path))
                .WithFields(FieldNames.Id)
                .WithCacheKey(String.Concat(projectId, "@__NOTFOUND")))
                .Select(s => s.Id).ToArray();
        }

        public void InvalidateNotFoundIdsCache(string projectId) {
            Cache.Remove(GetScopedCacheKey(String.Concat(projectId, "@__NOTFOUND")));
        }

        public ICollection<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var options = new MultiOptions().WithProjectId(projectId).WithSort(SortBy.Descending(FieldNames.LastOccurrence)).WithPaging(paging);
            options.Query = options.Query.And(Query.GTE(FieldNames.LastOccurrence, utcStart), Query.LTE(FieldNames.LastOccurrence, utcEnd));

            if (!includeFixed)
                options.Query = options.Query.And(Query.NotExists(FieldNames.DateFixed));

            if (!includeHidden)
                options.Query = options.Query.And(Query.NE(FieldNames.IsHidden, true));

            if (!includeNotFound)
                options.Query = options.Query.And(Query.NotExists(FieldNames.SignatureInfo_Path));

            return Find<Stack>(options);
        }

        public ICollection<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var options = new MultiOptions().WithProjectId(projectId).WithSort(SortBy.Descending(FieldNames.FirstOccurrence)).WithPaging(paging);
            options.Query = options.Query.And(Query.GTE(FieldNames.FirstOccurrence, utcStart), Query.LTE(FieldNames.FirstOccurrence, utcEnd));

            if (!includeFixed)
                options.Query = options.Query.And(Query.NotExists(FieldNames.DateFixed));

            if (!includeHidden)
                options.Query = options.Query.And(Query.NE(FieldNames.IsHidden, true));

            if (!includeNotFound)
                options.Query = options.Query.And(Query.NotExists(FieldNames.SignatureInfo_Path));

            return Find<Stack>(options);
        }

        public void MarkAsRegressed(string id) {
            UpdateAll(new QueryOptions().WithId(id), Update.Unset(FieldNames.DateFixed).Set(FieldNames.IsRegressed, true));
        }

        public override void InvalidateCache(Stack entity) {
            var originalStack = GetById(entity.Id, true);
            if (originalStack != null) {
                if (originalStack.DateFixed != entity.DateFixed) {
                    _eventRepository.UpdateFixedByStackId(entity.Id, entity.DateFixed.HasValue);
                    InvalidateFixedIdsCache(entity.ProjectId);
                }

                if (originalStack.IsHidden != entity.IsHidden) {
                    _eventRepository.UpdateHiddenByStackId(entity.Id, entity.IsHidden);
                    InvalidateHiddenIdsCache(entity.ProjectId);
                }

                InvalidateCache(String.Concat(entity.ProjectId, entity.SignatureHash));
            }

            base.InvalidateCache(entity);
        }

        public void InvalidateCache(string id, string signatureHash, string projectId) {
            InvalidateCache(id);
            InvalidateCache(String.Concat(projectId, signatureHash));
            InvalidateFixedIdsCache(projectId);
        }

        protected override void AfterRemove(ICollection<Stack> documents, bool sendNotification = true) {
            var organizations = documents.GroupBy(s => new {
                s.OrganizationId,
                s.ProjectId
            });

            foreach (var grouping in organizations) {
                _organizationRepository.IncrementStats(grouping.Key.OrganizationId, stackCount: grouping.Count() * -1);
                _projectRepository.IncrementStats(grouping.Key.ProjectId, stackCount: grouping.Count() * -1);
            }

            foreach (Stack document in documents)
                InvalidateCache(String.Concat(document.ProjectId, document.SignatureHash));

            base.AfterRemove(documents, sendNotification);
        }

        #region Collection Setup

        public const string CollectionName = "stack";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string SignatureHash = "hash";
            public const string FirstOccurrence = "fst";
            public const string LastOccurrence = "lst";
            public const string TotalOccurrences = "tot";
            public const string SignatureInfo = "sig";
            public const string SignatureInfo_Path = "sig.Path";
            public const string FixedInVersion = "fix";
            public const string DateFixed = "fdt";
            public const string Title = "tit";
            public const string Description = "dsc";
            public const string IsHidden = "hid";
            public const string IsRegressed = "regr";
            public const string DisableNotifications = "dnot";
            public const string OccurrencesAreCritical = "crit";
            public const string References = "refs";
            public const string Tags = "tag";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId, FieldNames.SignatureHash), IndexOptions.SetUnique(true).SetBackground(true));
            _collection.CreateIndex(IndexKeys.Descending(FieldNames.ProjectId, FieldNames.LastOccurrence), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Descending(FieldNames.ProjectId, FieldNames.IsHidden, FieldNames.DateFixed, FieldNames.SignatureInfo_Path), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<Stack> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.SignatureHash).SetElementName(FieldNames.SignatureHash);
            cm.GetMemberMap(c => c.SignatureInfo).SetElementName(FieldNames.SignatureInfo);
            cm.GetMemberMap(c => c.FixedInVersion).SetElementName(FieldNames.FixedInVersion).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.DateFixed).SetElementName(FieldNames.DateFixed).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Title).SetElementName(FieldNames.Title);
            cm.GetMemberMap(c => c.TotalOccurrences).SetElementName(FieldNames.TotalOccurrences);
            cm.GetMemberMap(c => c.FirstOccurrence).SetElementName(FieldNames.FirstOccurrence);
            cm.GetMemberMap(c => c.LastOccurrence).SetElementName(FieldNames.LastOccurrence);
            cm.GetMemberMap(c => c.Description).SetElementName(FieldNames.Description).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.IsHidden).SetElementName(FieldNames.IsHidden).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsRegressed).SetElementName(FieldNames.IsRegressed).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.DisableNotifications).SetElementName(FieldNames.DisableNotifications).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.OccurrencesAreCritical).SetElementName(FieldNames.OccurrencesAreCritical).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.References).SetElementName(FieldNames.References).SetShouldSerializeMethod(obj => ((Stack)obj).References.Any());
            cm.GetMemberMap(c => c.Tags).SetElementName(FieldNames.Tags).SetShouldSerializeMethod(obj => ((Stack)obj).Tags.Any());
        }

        #endregion
    }
}