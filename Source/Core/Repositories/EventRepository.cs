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
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Messaging;
using Exceptionless.Models;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Builders;

namespace Exceptionless.Core.Repositories {
    public class EventRepository : MongoRepositoryOwnedByOrganizationAndProjectAndStack<PersistentEvent>, IEventRepository {
        private readonly IProjectRepository _projectRepository;
        private readonly IOrganizationRepository _organizationRepository;

        public EventRepository(MongoDatabase database, IProjectRepository projectRepository, IOrganizationRepository organizationRepository, IValidator<PersistentEvent> validator = null, ICacheClient cacheClient = null, IMessagePublisher messagePublisher = null)
            : base(database, validator, cacheClient, messagePublisher) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;

            EnableNotifications = false;
        }

        public void UpdateFixedByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            var update = new UpdateBuilder();
            if (value)
                update.Set(FieldNames.IsFixed, true);
            else
                update.Unset(FieldNames.IsFixed);

            UpdateAll(new QueryOptions().WithStackId(stackId), update);
        }

        public void UpdateHiddenByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            var update = new UpdateBuilder();
            if (value)
                update.Set(FieldNames.IsHidden, true);
            else
                update.Unset(FieldNames.IsHidden);

            UpdateAll(new QueryOptions().WithStackId(stackId), update);
        }

        public void RemoveAllByDate(string organizationId, DateTime utcCutoffDate) {
            var query = Query.LT(FieldNames.Date_UTC, utcCutoffDate.Ticks);
            RemoveAll(new QueryOptions().WithOrganizationId(organizationId).WithQuery(query));
        }

        public void RemoveAllByClientIpAndDate(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            var query = Query.And(
                Query.EQ(FieldNames.RequestInfo_ClientIpAddress, new BsonString(clientIp)),
                Query.GTE(FieldNames.Date_UTC, utcStartDate.Ticks),
                Query.LTE(FieldNames.Date_UTC, utcEndDate.Ticks));
            RemoveAll(new QueryOptions().WithQuery(query));
        }

        public async Task RemoveAllByClientIpAndDateAsync(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            await Task.Run(() => RemoveAllByClientIpAndDate(clientIp, utcStartDate, utcEndDate));
        }

        private void IncrementOrganizationAndProjectEventCounts(string organizationId, string projectId, long count) {
            _organizationRepository.IncrementStats(organizationId, eventCount: -count);
            _projectRepository.IncrementStats(projectId, eventCount: -count);
        }

        public ICollection<PersistentEvent> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, PagingOptions paging, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            IMongoQuery query = Query.Null;
            
            if (utcStart != DateTime.MinValue)
                query = query.And(Query.GTE(FieldNames.Date_UTC, utcStart.Ticks));
            if (utcEnd != DateTime.MaxValue)
                query = query.And(Query.LTE(FieldNames.Date_UTC, utcEnd.Ticks));

            if (!includeHidden)
                query = query.And(Query.NE(FieldNames.IsHidden, true));

            if (!includeFixed)
                query = query.And(Query.NE(FieldNames.IsFixed, true));

            if (!includeNotFound)
                query = query.And(Query.NE(FieldNames.Type, "404"));

            return Find<PersistentEvent>(new MultiOptions().WithProjectId(projectId).WithQuery(query).WithPaging(paging).WithSort(SortBy.Descending(FieldNames.Date_UTC)));
        }

        public ICollection<PersistentEvent> GetByStackIdOccurrenceDate(string stackId, DateTime utcStart, DateTime utcEnd, PagingOptions paging) {
            IMongoQuery query = Query.Null;

            if (utcStart != DateTime.MinValue)
                query = query.And(Query.GTE(FieldNames.Date_UTC, utcStart.Ticks));
            if (utcEnd != DateTime.MaxValue)
                query = query.And(Query.LTE(FieldNames.Date_UTC, utcEnd.Ticks));

            return Find<PersistentEvent>(new MultiOptions().WithStackId(stackId).WithQuery(query).WithPaging(paging).WithSort(SortBy.Descending(FieldNames.Date_UTC)));
        }

        public ICollection<PersistentEvent> GetByReferenceId(string projectId, string referenceId) {
            return Find<PersistentEvent>(new MultiOptions().WithProjectId(projectId).WithQuery(Query.EQ(FieldNames.ReferenceId, referenceId)).WithSort(SortBy.Descending(FieldNames.Date_UTC)).WithLimit(10));
        }

        public void RemoveOldestEvents(string stackId, int maxEventsPerStack) {
            var options = new PagingOptions { Limit = maxEventsPerStack, Page = 2 };
            var events = GetOldestEvents(stackId, options);
            while (events.Count > 0) {
                Remove(events);

                if (!options.HasMore)
                    break;

                events = GetOldestEvents(stackId, options);
            }
        }

        private ICollection<PersistentEvent> GetOldestEvents(string stackId, PagingOptions options) {
            return Find<PersistentEvent>(new MultiOptions()
                .WithStackId(stackId)
                .WithFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId, FieldNames.StackId)
                .WithSort(SortBy.Descending(FieldNames.Date_UTC))
                .WithPaging(options));
        }

        public string GetPreviousEventIdInStack(string id) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            IMongoQuery query = Query.And(Query.NE(FieldNames.Id, new BsonObjectId(new ObjectId(data.Id))), Query.LTE(FieldNames.Date_UTC, data.Date.UtcTicks));

            var documents = Find<PersistentEvent>(new MultiOptions()
                .WithStackId(data.StackId)
                .WithSort(SortBy.Descending(FieldNames.Date_UTC))
                .WithLimit(10)
                .WithFields(FieldNames.Id, FieldNames.Date)
                .WithQuery(query));

            if (documents.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (documents.All(t => t.Date != data.Date))
                return documents.OrderByDescending(t => t.Date).ThenByDescending(t => t.Id).First().Id;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result before the target
            var unionResults = documents.Union(new[] { data })
                .OrderBy(t => t.Date.UtcTicks).ThenBy(t => t.Id)
                .ToList();

            var index = unionResults.FindIndex(t => t.Id == data.Id);
            return index == 0 ? null : unionResults[index - 1].Id;
        }

        public string GetNextEventIdInStack(string id) {
            PersistentEvent data = GetById(id, true);
            if (data == null)
                return null;

            IMongoQuery query = Query.And(Query.NE(FieldNames.Id, new BsonObjectId(new ObjectId(data.Id))), Query.GTE(FieldNames.Date_UTC, data.Date.UtcTicks));

            var documents = Find<PersistentEvent>(new MultiOptions()
                .WithStackId(data.StackId)
                .WithSort(SortBy.Ascending(FieldNames.Date_UTC))
                .WithLimit(10)
                .WithFields(FieldNames.Id, FieldNames.Date)
                .WithQuery(query));

            if (documents.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (documents.All(t => t.Date != data.Date))
                return documents.OrderBy(t => t.Date).ThenBy(t => t.Id).First().Id;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result after the target
            var unionResults = documents.Union(new[] { data })
                .OrderBy(t => t.Date.Ticks).ThenBy(t => t.Id)
                .ToList();

            var index = unionResults.FindIndex(t => t.Id == data.Id);
            return index == unionResults.Count - 1 ? null : unionResults[index + 1].Id;
        }

        public void MarkAsRegressedByStack(string id) {
            UpdateAll(new QueryOptions().WithStackId(id), Update.Unset(FieldNames.IsFixed));
        }

        public override ICollection<PersistentEvent> GetByOrganizationId(string organizationId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByOrganizationId(organizationId, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        public override ICollection<PersistentEvent> GetByOrganizationIds(ICollection<string> organizationIds, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByOrganizationIds(organizationIds, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        public override ICollection<PersistentEvent> GetByStackId(string stackId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByStackId(stackId, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        public override ICollection<PersistentEvent> GetByProjectId(string projectId, PagingOptions paging = null, bool useCache = false, TimeSpan? expiresIn = null) {
            bool sortByAscending = paging != null && !String.IsNullOrEmpty(paging.After);
            var results = base.GetByProjectId(projectId, GetPagingWithSortingOptions(paging, sortByAscending), useCache, expiresIn);
            return !sortByAscending ? results : results.OrderByDescending(e => e.Date).ThenByDescending(se => se.Id).ToList();
        }

        private PagingWithSortingOptions GetPagingWithSortingOptions(PagingOptions paging, bool sortByAscending) {
            var pagingWithSorting = new PagingWithSortingOptions(paging) {
                SortBy = sortByAscending ? SortBy.Ascending(FieldNames.Date_UTC, FieldNames.Id) : SortBy.Descending(FieldNames.Date_UTC, FieldNames.Id)
            };

            if (!String.IsNullOrEmpty(paging.Before) && paging.Before.IndexOf('-') > 0) {
                var parts = paging.Before.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                ObjectId id;
                long beforeUtcTicks;
                if (parts.Length == 2 && Int64.TryParse(parts[0], out beforeUtcTicks) && ObjectId.TryParse(parts[1], out id))
                    pagingWithSorting.BeforeQuery = Query.Or(Query.And(Query.EQ(FieldNames.Date_UTC, beforeUtcTicks), Query.LT(FieldNames.Id, new BsonObjectId(id))), Query.LT(FieldNames.Date_UTC, beforeUtcTicks));
            }

            if (!String.IsNullOrEmpty(paging.After) && paging.After.IndexOf('-') > 0) {
                var parts = paging.After.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);

                ObjectId id;
                long afterUtcTicks;
                if (parts.Length == 2 && Int64.TryParse(parts[0], out afterUtcTicks) && ObjectId.TryParse(parts[1], out id))
                    pagingWithSorting.AfterQuery = Query.Or(Query.And(Query.EQ(FieldNames.Date_UTC, afterUtcTicks), Query.GT(FieldNames.Id, new BsonObjectId(id))), Query.GT(FieldNames.Date_UTC, afterUtcTicks));
            }

            return pagingWithSorting;
        }

        protected override void AfterRemove(ICollection<PersistentEvent> documents, bool sendNotification = true) {
            base.AfterRemove(documents, sendNotification);

            var groups = documents.GroupBy(e => new {
                    e.OrganizationId,
                    e.ProjectId
                }).ToList();

            foreach (var grouping in groups) {
                if (!grouping.Any())
                    continue;

                IncrementOrganizationAndProjectEventCounts(grouping.Key.OrganizationId, grouping.Key.ProjectId, grouping.Count());
                // TODO: Should be updating stack
            }

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
        }

        protected override void AfterAdd(ICollection<PersistentEvent> documents, bool addToCache = false, TimeSpan? expiresIn = null) {
            bool enableNotifications = EnableNotifications;
            EnableNotifications = false;
            base.AfterAdd(documents, addToCache, expiresIn);
            EnableNotifications = enableNotifications;
        }

        #region Collection Setup

        public const string CollectionName = "event";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        private static class FieldNames {
            public const string Id = CommonFieldNames.Id;
            public const string OrganizationId = CommonFieldNames.OrganizationId;
            public const string ProjectId = CommonFieldNames.ProjectId;
            public const string StackId = CommonFieldNames.StackId;
            public const string Type = "typ";
            public const string Source = "src";
            public const string Date = CommonFieldNames.Date;
            public const string Date_UTC = CommonFieldNames.Date_UTC;
            public const string Tags = "tag";
            public const string Message = "msg";
            public const string Data = CommonFieldNames.Data;
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

            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId, FieldNames.ReferenceId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.StackId), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Ascending(FieldNames.OrganizationId, FieldNames.Date_UTC), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Descending(FieldNames.ProjectId, FieldNames.Date_UTC, FieldNames.IsFixed, FieldNames.IsHidden, FieldNames.Type), IndexOptions.SetBackground(true));
            _collection.CreateIndex(IndexKeys.Descending(FieldNames.StackId, FieldNames.Date_UTC), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<PersistentEvent> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.IsFixed).SetElementName(FieldNames.IsFixed).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsHidden).SetElementName(FieldNames.IsHidden).SetIgnoreIfDefault(true);

            if (!BsonClassMap.IsClassMapRegistered(typeof(Event))) {
                BsonClassMap.RegisterClassMap<Event>(evcm => {
                    evcm.AutoMap();
                    evcm.SetIgnoreExtraElements(false);
                    evcm.SetIgnoreExtraElementsIsInherited(true);
                    evcm.MapExtraElementsProperty(c => c.Data);
                    evcm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.Source).SetElementName(FieldNames.Source).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.Message).SetElementName(FieldNames.Message).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.ReferenceId).SetElementName(FieldNames.ReferenceId).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.SessionId).SetElementName(FieldNames.SessionId).SetIgnoreIfDefault(true);
                    evcm.GetMemberMap(c => c.Date).SetElementName(FieldNames.Date).SetSerializer(new UtcDateTimeOffsetSerializer());
                    evcm.GetMemberMap(c => c.Data).SetElementName(FieldNames.Data).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Event)obj).Data.Any());
                    evcm.MapMember(c => c.Tags).SetElementName(FieldNames.Tags).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Event)obj).Tags.Any());
                });
            }
        }
        
        #endregion
    }
}