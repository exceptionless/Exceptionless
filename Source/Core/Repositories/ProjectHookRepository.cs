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
using Exceptionless.Models.Admin;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core {
    public class ProjectHookRepository : MongoRepositoryWithIdentity<ProjectHook>, IProjectHookRepository {
        public ProjectHookRepository(MongoDatabase database, ICacheClient cacheClient = null) : base(database, cacheClient) {}

        public const string CollectionName = "project.hook";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        public new static class FieldNames {
            public const string Id = "_id";
            public const string ProjectId = "ProjectId";
            public const string Url = "Url";
            public const string EventTypes = "EventTypes";
        }

        public static class EventTypes {
            public const string NewError = "NewError";
            public const string ErrorRegression = "ErrorRegression";
            public const string CriticalError = "CriticalError";
            public const string StackPromoted = "StackPromoted";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.Url), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<ProjectHook> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.ProjectId).SetRepresentation(BsonType.ObjectId).SetElementName(FieldNames.ProjectId);
            cm.GetMemberMap(c => c.Url).SetElementName(FieldNames.Url);
            cm.GetMemberMap(c => c.EventTypes).SetElementName(FieldNames.EventTypes);
        }

        public IEnumerable<ProjectHook> GetByProjectId(string projectId) {
            ProjectHook[] result = Cache != null ? Cache.Get<ProjectHook[]>(GetScopedCacheKey(projectId)) : null;
            if (result == null) {
                result = Collection
                    .Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                    .ToArray();
                if (Cache != null)
                    Cache.Set(GetScopedCacheKey(projectId), result, TimeSpan.FromMinutes(5));
            }

            return result;
        }

        public override void InvalidateCache(ProjectHook entity) {
            Cache.Remove(GetScopedCacheKey(entity.ProjectId));
            base.InvalidateCache(entity);
        }
    }
}