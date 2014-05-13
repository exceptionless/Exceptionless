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
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class StackRepository : MongoRepositoryOwnedByOrganizationAndProject<Stack>, IStackRepository {
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IEventRepository _eventRepository;

        public StackRepository(MongoDatabase database, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IEventRepository eventRepository, ICacheClient cacheClient = null)
            : base(database, cacheClient) {
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _eventRepository = eventRepository;
        }

        protected override void AfterRemove(IList<Stack> documents, bool sendNotification = true) {
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

        public void IncrementStats(string stackId, DateTime occurrenceDate) {
            // if total occurrences are zero (stack data was reset), then set first occurrence date
            _collection.Update(Query.And(
                    Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(stackId))),
                    Query.EQ(FieldNames.TotalOccurrences, new BsonInt32(0))
                ),
                Update.Set(FieldNames.FirstOccurrence, occurrenceDate));

            // Only update the LastOccurrence if the new date is greater then the existing date.
            IMongoQuery query = Query.And(Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(stackId))), Query.LT(FieldNames.LastOccurrence, occurrenceDate));
            UpdateBuilder update = Update.Inc(FieldNames.TotalOccurrences, 1).Set(FieldNames.LastOccurrence, occurrenceDate);

            var result = _collection.Update(query, update);
            if (result.DocumentsAffected > 0)
                return;

            _collection.Update(Query.EQ(FieldNames.Id, new BsonObjectId(new ObjectId(stackId))), Update.Inc(FieldNames.TotalOccurrences, 1));
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
            var result = Cache != null ? Cache.Get<string[]>(GetScopedCacheKey(String.Concat(projectId, "@__HIDDEN"))) : null;
            if (result == null) {
                result = Collection
                    .Find(Query.And(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))), Query.EQ(FieldNames.IsHidden, BsonBoolean.True)))
                    .SetFields(FieldNames.Id)
                    .Select(err => err.Id)
                    .ToArray();
                if (Cache != null)
                    Cache.Set(GetScopedCacheKey(String.Concat(projectId, "@__HIDDEN")), result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        public void InvalidateHiddenIdsCache(string projectId) {
            Cache.Remove(GetScopedCacheKey(String.Concat(projectId, "@__HIDDEN")));
        }

        public string[] GetFixedIds(string projectId) {
            var result = Cache != null ? Cache.Get<string[]>(GetScopedCacheKey(String.Concat(projectId, "@__FIXED"))) : null;
            if (result == null) {
                result = Collection
                    .Find(Query.And(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))), Query.Exists(FieldNames.DateFixed)))
                    .SetFields(FieldNames.Id)
                    .Select(err => err.Id)
                    .ToArray();
                if (Cache != null)
                    Cache.Set(GetScopedCacheKey(String.Concat(projectId, "@__FIXED")), result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        public void InvalidateFixedIdsCache(string projectId) {
            Cache.Remove(GetScopedCacheKey(String.Concat(projectId, "@__FIXED")));
        }

        public string[] GetNotFoundIds(string projectId) {
            var result = Cache != null ? Cache.Get<string[]>(GetScopedCacheKey(String.Concat(projectId, "@__NOTFOUND"))) : null;
            if (result == null) {
                result = Collection
                    .Find(Query.And(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))), Query.Exists(FieldNames.SignatureInfo_Path)))
                    .SetFields(FieldNames.Id)
                    .Select(err => err.Id)
                    .ToArray();
                if (Cache != null)
                    Cache.Set(GetScopedCacheKey(String.Concat(projectId, "@__NOTFOUND")), result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        public void InvalidateNotFoundIdsCache(string projectId) {
            Cache.Remove(GetScopedCacheKey(String.Concat(projectId, "@__NOTFOUND")));
        }

        public IEnumerable<Stack> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, out long count, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var conditions = new List<IMongoQuery> {
                Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))),
                Query.GTE(FieldNames.LastOccurrence, utcStart),
                Query.LTE(FieldNames.LastOccurrence, utcEnd)
            };

            if (!includeFixed)
                conditions.Add(Query.NotExists(FieldNames.DateFixed));

            if (!includeHidden)
                conditions.Add(Query.NE(FieldNames.IsHidden, true));

            if (!includeNotFound)
                conditions.Add(Query.NotExists(FieldNames.SignatureInfo_Path));

            var cursor = _collection.FindAs<Stack>(Query.And(conditions));
            cursor.SetSortOrder(SortBy.Descending(FieldNames.LastOccurrence));

            if (skip.HasValue)
                cursor.SetSkip(skip.Value);

            if (take.HasValue)
                cursor.SetLimit(take.Value);

            count = cursor.Count();
            return cursor;
        }

        public IEnumerable<Stack> GetNew(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, out long count, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var conditions = new List<IMongoQuery> {
                Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))),
                Query.GTE(FieldNames.FirstOccurrence, utcStart),
                Query.LTE(FieldNames.FirstOccurrence, utcEnd)
            };

            if (!includeFixed)
                conditions.Add(Query.NotExists(FieldNames.DateFixed));

            if (!includeHidden)
                conditions.Add(Query.NE(FieldNames.IsHidden, true));

            if (!includeNotFound)
                conditions.Add(Query.NotExists(FieldNames.SignatureInfo_Path));

            var cursor = _collection.FindAs<Stack>(Query.And(conditions));
            cursor.SetSortOrder(SortBy.Descending(FieldNames.FirstOccurrence));

            if (skip.HasValue)
                cursor.SetSkip(skip.Value);

            if (take.HasValue)
                cursor.SetLimit(take.Value);

            count = cursor.Count();
            return cursor;
        }
        public void MarkAsRegressed(string id) {
            UpdateAll(new QueryOptions().WithId(id), Update.Unset(FieldNames.DateFixed).Set(FieldNames.IsRegressed, true));
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
            cm.GetMemberMap(c => c.ProjectId).SetRepresentation(BsonType.ObjectId).SetElementName(FieldNames.ProjectId);
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

        #endregion
    }
}