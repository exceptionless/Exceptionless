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
using System.Threading.Tasks;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using NLog.Time;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core {
    public class EventRepository : MongoRepositoryOwnedByOrganization<PersistentEvent>, IEventRepository {
        private readonly ProjectRepository _projectRepository;
        private readonly OrganizationRepository _organizationRepository;
        //private readonly ErrorStatsHelper _statsHelper;

        public EventRepository(MongoDatabase database, ProjectRepository projectRepository, OrganizationRepository organizationRepository, ICacheClient cacheClient = null)
            : base(database, cacheClient) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            //_statsHelper = statsHelper;
        }

        public const string CollectionName = "event";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        #region Class Mapping

        public new static class FieldNames {
            public const string Id = "_id";
            public const string OrganizationId = "oid";
            public const string ProjectId = "pid";
            public const string StackId = "sid";
            public const string Type = "typ";
            public const string Source = "src";
            public const string Date = "dt";
            public const string Date_UTC = "dt.0";
            public const string Tags = "tag";
            public const string Message = "msg";
            public const string Data = "ext";
            public const string ReferenceId = "ref";
            public const string SessionId = "xid";
            public const string SummaryHtml = "html";
            public const string IsFixed = "fix";
            public const string IsHidden = "hid";
            public const string RequestInfo = "req";
            public const string RequestInfo_ClientIpAddress = RequestInfo + ".ip";
        }

        protected override void InitializeCollection(MongoDatabase database) {
            base.InitializeCollection(database);

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.StackId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.OrganizationId, FieldNames.Date_UTC), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Descending(FieldNames.ProjectId, FieldNames.Date_UTC, FieldNames.IsFixed, FieldNames.IsHidden, FieldNames.Type), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Descending(FieldNames.StackId, FieldNames.Date_UTC), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<PersistentEvent> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.StackId).SetElementName(FieldNames.StackId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.IsFixed).SetElementName(FieldNames.IsFixed).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsHidden).SetElementName(FieldNames.IsHidden).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.SummaryHtml).SetElementName(FieldNames.SummaryHtml).SetIgnoreIfDefault(true);

            if (!BsonClassMap.IsClassMapRegistered(typeof(Event))) {
                BsonClassMap.RegisterClassMap<Event>(evcm => {
                    evcm.AutoMap();
                    evcm.SetIgnoreExtraElements(false);
                    evcm.SetIgnoreExtraElementsIsInherited(true);
                    evcm.MapExtraElementsProperty(c => c.Data);
                    evcm.GetMemberMap(c => c.Data).SetElementName(FieldNames.Data).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Event)obj).Data.Any()); ;
                    evcm.GetMemberMap(c => c.Source).SetElementName(FieldNames.Source).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.Message).SetElementName(FieldNames.Message).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.ReferenceId).SetElementName(FieldNames.ReferenceId).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.SessionId).SetElementName(FieldNames.SessionId).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.Date).SetElementName(FieldNames.Date).SetSerializer(new UtcDateTimeOffsetSerializer());
                    evcm.GetMemberMap(c => c.Tags).SetElementName(FieldNames.Tags).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Event)obj).Tags.Any());
                });
            }
        }

        #endregion

        public override PersistentEvent Add(PersistentEvent data, bool addToCache = false) {
            if (data == null)
                throw new ArgumentNullException("data");
            if (String.IsNullOrEmpty(data.OrganizationId))
                throw new ArgumentException("OrganizationId must be set.", "data");
            if (String.IsNullOrEmpty(data.ProjectId))
                throw new ArgumentException("ProjectId must be set.", "data");

            return base.Add(data, addToCache);
        }

        public override void Add(IEnumerable<PersistentEvent> events, bool addToCache = false) {
            foreach (PersistentEvent eventData in events)
                Add(eventData, addToCache);
        }

        public void UpdateFixedByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            IMongoQuery query = Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(stackId)));

            var update = new UpdateBuilder();
            if (value)
                update.Set(FieldNames.IsFixed, true);
            else
                update.Unset(FieldNames.IsFixed);

            Collection.Update(query, update, UpdateFlags.Multi);
        }

        public void UpdateHiddenByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            IMongoQuery query = Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(stackId)));

            var update = new UpdateBuilder();
            if (value)
                update.Set(FieldNames.IsHidden, true);
            else
                update.Unset(FieldNames.IsHidden);

            Collection.Update(query, update, UpdateFlags.Multi);
        }

        public void RemoveAllByProjectId(string projectId) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId)
                .Select(es => new PersistentEvent {
                    Id = es.Id,
                    OrganizationId = es.OrganizationId,
                    ProjectId = projectId
                })
                .ToArray();

            while (errors.Length > 0) {
                Delete(errors);

                errors = Collection.Find(Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId))))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId)
                    .Select(es => new PersistentEvent {
                        Id = es.Id,
                        OrganizationId = es.OrganizationId,
                        ProjectId = projectId
                    })
                    .ToArray();
            }
        }

        public async Task RemoveAllByProjectIdAsync(string projectId) {
            await Task.Run(() => RemoveAllByProjectId(projectId));
        }

        public void RemoveAllByStackId(string stackId) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(stackId))))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                .Select(e => new PersistentEvent {
                    Id = e.Id,
                    OrganizationId = e.OrganizationId,
                    ProjectId = e.ProjectId,
                    StackId = stackId
                })
                .ToArray();

            while (errors.Length > 0) {
                Delete(errors);

                errors = Collection.Find(Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(stackId))))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                    .Select(e => new PersistentEvent {
                        Id = e.Id,
                        OrganizationId = e.OrganizationId,
                        ProjectId = e.ProjectId,
                        StackId = stackId
                    })
                    .ToArray();
            }
        }

        public async Task RemoveAllByStackIdAsync(string stackId) {
            await Task.Run(() => RemoveAllByStackId(stackId));
        }

        public void RemoveAllByDate(string organizationId, DateTime utcCutoffDate) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.And(
                Query.EQ(FieldNames.OrganizationId, new BsonObjectId(new ObjectId(organizationId))),
                Query.LT(FieldNames.Date_UTC, utcCutoffDate.Ticks)))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                .Select(e => new PersistentEvent {
                    Id = e.Id,
                    OrganizationId = e.OrganizationId,
                    ProjectId = e.ProjectId,
                    StackId = e.StackId
                })
                .ToArray();

            while (errors.Length > 0) {
                Delete(errors);

                errors = Collection.Find(Query.And(
                    Query.EQ(FieldNames.OrganizationId, new BsonObjectId(new ObjectId(organizationId))),
                    Query.LT(FieldNames.Date_UTC, utcCutoffDate.Ticks)))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                    .Select(e => new PersistentEvent {
                        Id = e.Id,
                        OrganizationId = e.OrganizationId,
                        ProjectId = e.ProjectId,
                        StackId = e.StackId
                    }).ToArray();
            }
        }

        public void RemoveAllByClientIpAndDate(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.And(
                Query.EQ(FieldNames.RequestInfo_ClientIpAddress, new BsonString(clientIp)),
                Query.GTE(FieldNames.Date_UTC, utcStartDate.Ticks),
                Query.LTE(FieldNames.Date_UTC, utcEndDate.Ticks)))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                .Select(e => new PersistentEvent {
                    Id = e.Id,
                    OrganizationId = e.OrganizationId,
                    ProjectId = e.ProjectId,
                    StackId = e.StackId
                })
                .ToArray();

            while (errors.Length > 0) {
                Delete(errors);
                // TODO: Need to decrement stats time bucket by the number of errors we removed. Add flag to delete to tell it to decrement stats docs.

                //var groups = errors.GroupBy(e => new {
                //    e.OrganizationId,
                //    e.ProjectId,
                //    e.ErrorStackId
                //}).ToList();
                //foreach (var grouping in groups) {
                //    if (_statsHelper == null)
                //        continue;

                //    _statsHelper.DecrementDayProjectStatsForTimeBucket(grouping.Key.ErrorStackId, grouping.Count());
                //}

                errors = Collection.Find(Query.And(
                    Query.EQ(FieldNames.RequestInfo_ClientIpAddress, new BsonString(clientIp)),
                    Query.GTE(FieldNames.Date_UTC, utcStartDate.Ticks),
                    Query.LTE(FieldNames.Date_UTC, utcEndDate.Ticks)))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                    .Select(e => new PersistentEvent {
                        Id = e.Id,
                        OrganizationId = e.OrganizationId,
                        ProjectId = e.ProjectId,
                        StackId = e.StackId
                    })
                    .ToArray();
            }
        }

        public async Task RemoveAllByClientIpAndDateAsync(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            await Task.Run(() => RemoveAllByClientIpAndDate(clientIp, utcStartDate, utcEndDate));
        }

        public override void Delete(IEnumerable<PersistentEvent> events) {
            var groups = events.GroupBy(e => new {
                e.OrganizationId,
                e.ProjectId
            }).ToList();
            foreach (var grouping in groups) {
                var result = _collection.Remove(Query.In(FieldNames.Id, grouping.ToArray().Select(error => new BsonObjectId(new ObjectId(error.Id)))));

                if (result.DocumentsAffected <= 0)
                    continue;

                IncrementOrganizationAndProjectErrorCounts(grouping.Key.OrganizationId, grouping.Key.ProjectId, result.DocumentsAffected);
                // TODO: Should be updating stack
            }

            foreach (PersistentEvent entity in events)
                InvalidateCache(entity);
        }

        private void IncrementOrganizationAndProjectErrorCounts(string organizationId, string projectId, long count) {
            _organizationRepository.IncrementStats(organizationId, eventCount: -count);
            _projectRepository.IncrementStats(projectId, eventCount: -count);
        }

        #region Queries

        public IEnumerable<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var conditions = new List<IMongoQuery> {
                Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId)))
            };

            if (utcStart != DateTime.MinValue)
                conditions.Add(Query.GTE(FieldNames.Date_UTC, utcStart.Ticks));
            if (utcEnd != DateTime.MaxValue)
                conditions.Add(Query.LTE(FieldNames.Date_UTC, utcEnd.Ticks));

            if (!includeHidden)
                conditions.Add(Query.NE(FieldNames.IsHidden, true));

            if (!includeFixed)
                conditions.Add(Query.NE(FieldNames.IsFixed, true));

            if (!includeNotFound)
                conditions.Add(Query.NE(FieldNames.Type, "404"));

            var cursor = _collection.FindAs<PersistentEvent>(Query.And(conditions));
            cursor.SetSortOrder(SortBy.Descending(FieldNames.Date_UTC));

            if (skip.HasValue)
                cursor.SetSkip(skip.Value);

            if (take.HasValue)
                cursor.SetLimit(take.Value);

            return cursor;
        }

        public IEnumerable<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, int? skip, int? take) {
            var cursor = _collection.FindAs<PersistentEvent>(Query.And(Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(stackId))), Query.GTE(FieldNames.Date_UTC, utcStart.Ticks), Query.LTE(FieldNames.Date_UTC, utcEnd.Ticks)));
            cursor.SetSortOrder(SortBy.Descending(FieldNames.Date_UTC));

            if (skip.HasValue)
                cursor.SetSkip(skip.Value);

            if (take.HasValue)
                cursor.SetLimit(take.Value);

            return cursor;
        }

        public string GetPreviousEventIdInStack(string id) {
            PersistentEvent data = GetByIdCached(id);
            if (data == null)
                return null;

            var cursor = _collection.FindAs<PersistentEvent>(
                                                   Query.And(
                                                             Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(data.StackId))),
                                                       Query.NE(FieldNames.Id, new BsonObjectId(new ObjectId(data.Id))),
                                                       Query.LTE(FieldNames.Date_UTC, data.Date.UtcTicks)));

            cursor.SetSortOrder(SortBy.Descending(FieldNames.Date_UTC));
            cursor.SetLimit(10);
            cursor.SetFields(FieldNames.Id, FieldNames.Date);

            var results = cursor.Select(e => Tuple.Create(e.Id, e.Date)).ToList();
            if (results.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (results.All(t => t.Item2 != data.Date))
                return results.OrderByDescending(t => t.Item2).ThenByDescending(t => t.Item1).First().Item1;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result before the target
            var unionResults = results.Union(new[] { Tuple.Create(data.Id, data.Date) })
                .OrderBy(t => t.Item2.UtcTicks).ThenBy(t => t.Item1)
                .ToList();

            var index = unionResults.FindIndex(t => t.Item1 == data.Id);
            return index == 0 ? null : unionResults[index - 1].Item1;
        }

        public string GetNextEventIdInStack(string id) {
            PersistentEvent data = GetByIdCached(id);
            if (data == null)
                return null;

            var cursor = _collection.FindAs<PersistentEvent>(Query.And(
                    Query.EQ(FieldNames.StackId, new BsonObjectId(new ObjectId(data.StackId))),
                    Query.NE(FieldNames.Id, new BsonObjectId(new ObjectId(data.Id))),
                    Query.GTE(FieldNames.Date_UTC, data.Date.UtcTicks)));

            cursor.SetSortOrder(SortBy.Ascending(FieldNames.Date_UTC));
            cursor.SetLimit(10);
            cursor.SetFields(FieldNames.Id, FieldNames.Date);

            var results = cursor.Select(e => Tuple.Create(e.Id, e.Date)).ToList();
            if (results.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (results.All(t => t.Item2 != data.Date))
                return results.OrderBy(t => t.Item2).ThenBy(t => t.Item1).First().Item1;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result after the target
            var unionResults = results.Union(new[] { Tuple.Create(data.Id, data.Date) })
                .OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1)
                .ToList();

            var index = unionResults.FindIndex(t => t.Item1 == data.Id);
            return index == unionResults.Count - 1 ? null : unionResults[index + 1].Item1;
        }

        #endregion
    }
}