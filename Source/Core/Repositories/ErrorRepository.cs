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
using Exceptionless.Core.Utility;
using Exceptionless.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using ServiceStack.CacheAccess;

namespace Exceptionless.Core {
    public class ErrorRepository : MongoRepositoryOwnedByOrganization<Error>, IErrorRepository {
        private readonly ProjectRepository _projectRepository;
        private readonly OrganizationRepository _organizationRepository;
        //private readonly ErrorStatsHelper _statsHelper;

        public ErrorRepository(MongoDatabase database, ProjectRepository projectRepository, OrganizationRepository organizationRepository, ICacheClient cacheClient = null)
            : base(database, cacheClient) {
            _projectRepository = projectRepository;
            _organizationRepository = organizationRepository;
            //_statsHelper = statsHelper;
        }

        public const string CollectionName = "error";

        protected override string GetCollectionName() {
            return CollectionName;
        }

        #region Class Mapping

        public new static class FieldNames {
            public const string Id = "_id";
            public const string ProjectId = "pid";
            public const string ErrorStackId = "sid";
            public const string OrganizationId = "oid";
            public const string Message = "msg";
            public const string Type = "typ";
            public const string OccurrenceDate = "dt";
            public const string OccurrenceDate_UTC = "dt.0";
            public const string Tags = "tag";
            public const string UserEmail = "u-em";
            public const string UserName = "u-nm";
            public const string UserDescription = "u-dsc";
            public const string RequestInfo = "req";
            public const string ExceptionlessClientInfo = "cli";
            public const string Modules = "mod";
            public const string EnvironmentInfo = "env";
            public const string Code = "cod";
            public const string ExtendedData = "ext";
            public const string Inner = "inr";
            public const string StackTrace = "st";
            public const string TargetMethod = "meth";
            public const string UserAgent = "ag";
            public const string HttpMethod = "verb";
            public const string IsSecure = "sec";
            public const string Host = "hst";
            public const string Port = "prt";
            public const string Path = "url";
            public const string RequestInfo_Path = RequestInfo + "." + Path;
            public const string Referrer = "ref";
            public const string ClientIpAddress = "ip";
            public const string RequestInfo_ClientIpAddress = "ip";
            public const string Cookies = "cok";
            public const string PostData = "pst";
            public const string QueryString = "qry";
            public const string DeclaringNamespace = "ns";
            public const string DeclaringType = "dtyp";
            public const string Name = "nm";
            public const string GenericArguments = "arg";
            public const string Parameters = "prm";
            public const string Version = "ver";
            public const string InstallIdentifier = "iid";
            public const string InstallDate = "idt";
            public const string InstallDate_UTC = "idt.0";
            public const string IsSignatureTarget = "sig";
            public const string StartCount = "stc";
            public const string SubmitCount = "subc";
            public const string Platform = "pla";
            public const string SubmissionMethod = "sm";
            public const string ProcessorCount = "cpus";
            public const string TotalPhysicalMemory = "mem";
            public const string AvailablePhysicalMemory = "amem";
            public const string CommandLine = "cmd";
            public const string ProcessName = "pnm";
            public const string ProcessId = "pid";
            public const string ProcessMemorySize = "pmem";
            public const string ThreadName = "thr";
            public const string ThreadId = "tid";
            public const string Architecture = "arc";
            public const string OSName = "os";
            public const string OSVersion = "osv";
            public const string MachineName = "nm";
            public const string RuntimeVersion = "run";
            public const string IpAddress = "ip";
            public const string ModuleId = "mid";
            public const string TypeNamespace = "tns";
            public const string FileName = "fil";
            public const string LineNumber = "lin";
            public const string Column = "col";
            public const string IsEntry = "ent";
            public const string CreatedDate = "crt";
            public const string ModifiedDate = "mod";
            public const string IsFixed = "fix";
            public const string IsHidden = "hid";
        }

        protected override void InitializeCollection(MongoCollection<Error> collection) {
            base.InitializeCollection(collection);

            collection.CreateIndex(IndexKeys.Ascending(FieldNames.ProjectId), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Ascending(FieldNames.ErrorStackId), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Ascending(FieldNames.OrganizationId, FieldNames.OccurrenceDate_UTC), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Descending(FieldNames.ProjectId, FieldNames.OccurrenceDate_UTC, FieldNames.IsFixed, FieldNames.IsHidden, FieldNames.Code), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Descending(FieldNames.RequestInfo_ClientIpAddress, FieldNames.OccurrenceDate_UTC), IndexOptions.SetBackground(true));
            collection.CreateIndex(IndexKeys.Descending(FieldNames.ErrorStackId, FieldNames.OccurrenceDate_UTC), IndexOptions.SetBackground(true));
        }

        protected override void ConfigureClassMap(BsonClassMap<Error> cm) {
            base.ConfigureClassMap(cm);
            cm.GetMemberMap(c => c.ErrorStackId).SetElementName(FieldNames.ErrorStackId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.ProjectId).SetElementName(FieldNames.ProjectId).SetRepresentation(BsonType.ObjectId);
            cm.GetMemberMap(c => c.OccurrenceDate).SetElementName(FieldNames.OccurrenceDate).SetSerializer(new UtcDateTimeOffsetSerializer());
            cm.GetMemberMap(c => c.Tags).SetElementName(FieldNames.Tags).SetIgnoreIfNull(true).SetShouldSerializeMethod(obj => ((Error)obj).Tags.Any());
            cm.GetMemberMap(c => c.UserEmail).SetElementName(FieldNames.UserEmail).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.UserName).SetElementName(FieldNames.UserName).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.UserDescription).SetElementName(FieldNames.UserDescription).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.RequestInfo).SetElementName(FieldNames.RequestInfo).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.ExceptionlessClientInfo).SetElementName(FieldNames.ExceptionlessClientInfo).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.Modules).SetElementName(FieldNames.Modules).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.EnvironmentInfo).SetElementName(FieldNames.EnvironmentInfo).SetIgnoreIfNull(true);
            cm.GetMemberMap(c => c.IsFixed).SetElementName(FieldNames.IsFixed).SetIgnoreIfDefault(true);
            cm.GetMemberMap(c => c.IsHidden).SetElementName(FieldNames.IsHidden).SetIgnoreIfDefault(true);

            if (!BsonClassMap.IsClassMapRegistered(typeof(ErrorInfo))) {
                BsonClassMap.RegisterClassMap<ErrorInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Message).SetElementName(FieldNames.Message).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type);
                    cmm.GetMemberMap(c => c.Code).SetElementName(FieldNames.Code);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(FieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((ErrorInfo)obj).ExtendedData.Any());
                    cmm.GetMemberMap(c => c.Inner).SetElementName(FieldNames.Inner);
                    cmm.GetMemberMap(c => c.StackTrace).SetElementName(FieldNames.StackTrace).SetShouldSerializeMethod(obj => ((ErrorInfo)obj).StackTrace.Any());
                    cmm.GetMemberMap(c => c.TargetMethod).SetElementName(FieldNames.TargetMethod).SetIgnoreIfNull(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(RequestInfo))) {
                BsonClassMap.RegisterClassMap<RequestInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.UserAgent).SetElementName(FieldNames.UserAgent);
                    cmm.GetMemberMap(c => c.HttpMethod).SetElementName(FieldNames.HttpMethod);
                    cmm.GetMemberMap(c => c.IsSecure).SetElementName(FieldNames.IsSecure);
                    cmm.GetMemberMap(c => c.Host).SetElementName(FieldNames.Host);
                    cmm.GetMemberMap(c => c.Port).SetElementName(FieldNames.Port);
                    cmm.GetMemberMap(c => c.Path).SetElementName(FieldNames.Path);
                    cmm.GetMemberMap(c => c.Referrer).SetElementName(FieldNames.Referrer).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.ClientIpAddress).SetElementName(FieldNames.ClientIpAddress);
                    cmm.GetMemberMap(c => c.Cookies).SetElementName(FieldNames.Cookies).SetShouldSerializeMethod(obj => ((RequestInfo)obj).Cookies.Any());
                    cmm.GetMemberMap(c => c.PostData).SetElementName(FieldNames.PostData).SetShouldSerializeMethod(obj => ShouldSerializePostData(obj as RequestInfo));
                    cmm.GetMemberMap(c => c.QueryString).SetElementName(FieldNames.QueryString).SetShouldSerializeMethod(obj => ((RequestInfo)obj).QueryString.Any());
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(FieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((RequestInfo)obj).ExtendedData.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(ExceptionlessClientInfo))) {
                BsonClassMap.RegisterClassMap<ExceptionlessClientInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Version).SetElementName(FieldNames.Version);
                    cmm.GetMemberMap(c => c.InstallIdentifier).SetElementName(FieldNames.InstallIdentifier);
                    cmm.GetMemberMap(c => c.InstallDate).SetElementName(FieldNames.InstallDate);
                    cmm.GetMemberMap(c => c.StartCount).SetElementName(FieldNames.StartCount);
                    cmm.GetMemberMap(c => c.SubmitCount).SetElementName(FieldNames.SubmitCount);
                    cmm.GetMemberMap(c => c.Platform).SetElementName(FieldNames.Platform);
                    cmm.GetMemberMap(c => c.SubmissionMethod).SetElementName(FieldNames.SubmissionMethod);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(EnvironmentInfo))) {
                BsonClassMap.RegisterClassMap<EnvironmentInfo>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.ProcessorCount).SetElementName(FieldNames.ProcessorCount);
                    cmm.GetMemberMap(c => c.TotalPhysicalMemory).SetElementName(FieldNames.TotalPhysicalMemory);
                    cmm.GetMemberMap(c => c.AvailablePhysicalMemory).SetElementName(FieldNames.AvailablePhysicalMemory);
                    cmm.GetMemberMap(c => c.CommandLine).SetElementName(FieldNames.CommandLine);
                    cmm.GetMemberMap(c => c.ProcessName).SetElementName(FieldNames.ProcessName);
                    cmm.GetMemberMap(c => c.ProcessId).SetElementName(FieldNames.ProcessId);
                    cmm.GetMemberMap(c => c.ProcessMemorySize).SetElementName(FieldNames.ProcessMemorySize);
                    cmm.GetMemberMap(c => c.ThreadName).SetElementName(FieldNames.ThreadName).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.ThreadId).SetElementName(FieldNames.ThreadId);
                    cmm.GetMemberMap(c => c.Architecture).SetElementName(FieldNames.Architecture);
                    cmm.GetMemberMap(c => c.OSName).SetElementName(FieldNames.OSName);
                    cmm.GetMemberMap(c => c.OSVersion).SetElementName(FieldNames.OSVersion);
                    cmm.GetMemberMap(c => c.MachineName).SetElementName(FieldNames.MachineName);
                    cmm.GetMemberMap(c => c.RuntimeVersion).SetElementName(FieldNames.RuntimeVersion);
                    cmm.GetMemberMap(c => c.IpAddress).SetElementName(FieldNames.IpAddress);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(FieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((EnvironmentInfo)obj).ExtendedData.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Method))) {
                BsonClassMap.RegisterClassMap<Method>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.DeclaringNamespace).SetElementName(FieldNames.DeclaringNamespace);
                    cmm.GetMemberMap(c => c.DeclaringType).SetElementName(FieldNames.DeclaringType);
                    cmm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name);
                    cmm.GetMemberMap(c => c.ModuleId).SetElementName(FieldNames.ModuleId);
                    cmm.GetMemberMap(c => c.IsSignatureTarget).SetElementName(FieldNames.IsSignatureTarget);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(FieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((Method)obj).ExtendedData.Any());
                    cmm.GetMemberMap(c => c.GenericArguments).SetElementName(FieldNames.GenericArguments).SetShouldSerializeMethod(obj => ((Method)obj).GenericArguments.Any());
                    cmm.GetMemberMap(c => c.Parameters).SetElementName(FieldNames.Parameters).SetShouldSerializeMethod(obj => ((Method)obj).Parameters.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Parameter))) {
                BsonClassMap.RegisterClassMap<Parameter>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name);
                    cmm.GetMemberMap(c => c.Type).SetElementName(FieldNames.Type);
                    cmm.GetMemberMap(c => c.TypeNamespace).SetElementName(FieldNames.TypeNamespace);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(FieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((Parameter)obj).ExtendedData.Any());
                    cmm.GetMemberMap(c => c.GenericArguments).SetElementName(FieldNames.GenericArguments).SetShouldSerializeMethod(obj => ((Parameter)obj).GenericArguments.Any());
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(StackFrame))) {
                BsonClassMap.RegisterClassMap<StackFrame>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.FileName).SetElementName(FieldNames.FileName).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.LineNumber).SetElementName(FieldNames.LineNumber).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.Column).SetElementName(FieldNames.Column).SetIgnoreIfDefault(true);
                });
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Module))) {
                BsonClassMap.RegisterClassMap<Module>(cmm => {
                    cmm.AutoMap();
                    cmm.SetIgnoreExtraElements(true);
                    cmm.GetMemberMap(c => c.ModuleId).SetElementName(FieldNames.ModuleId).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.Name).SetElementName(FieldNames.Name);
                    cmm.GetMemberMap(c => c.Version).SetElementName(FieldNames.Version).SetIgnoreIfNull(true);
                    cmm.GetMemberMap(c => c.IsEntry).SetElementName(FieldNames.IsEntry).SetIgnoreIfDefault(true);
                    cmm.GetMemberMap(c => c.CreatedDate).SetElementName(FieldNames.CreatedDate);
                    cmm.GetMemberMap(c => c.ModifiedDate).SetElementName(FieldNames.ModifiedDate);
                    cmm.GetMemberMap(c => c.ExtendedData).SetElementName(FieldNames.ExtendedData).SetShouldSerializeMethod(obj => ((Module)obj).ExtendedData.Any());
                });
            }
        }

        private bool ShouldSerializePostData(RequestInfo requestInfo) {
            if (requestInfo == null)
                return false;

            if (requestInfo.PostData is Dictionary<string, string>)
                return ((Dictionary<string, string>)requestInfo.PostData).Any();

            return requestInfo.PostData != null;
        }

        #endregion

        public override Error Add(Error error, bool addToCache = false) {
            if (error == null)
                throw new ArgumentNullException("error");
            if (String.IsNullOrEmpty(error.OrganizationId))
                throw new ArgumentException("OrganizationId must be set.", "error");
            if (String.IsNullOrEmpty(error.ProjectId))
                throw new ArgumentException("ProjectId must be set.", "error");

            return base.Add(error, addToCache);
        }

        public override void Add(IEnumerable<Error> errors, bool addToCache = false) {
            foreach (Error error in errors)
                Add(error, addToCache);
        }

        public void UpdateFixedByStackId(string stackId, bool value) {
            if (String.IsNullOrEmpty(stackId))
                throw new ArgumentNullException("stackId");

            IMongoQuery query = Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(stackId)));

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

            IMongoQuery query = Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(stackId)));

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
                .Select(es => new Error {
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
                    .Select(es => new Error {
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

        public void RemoveAllByErrorStackId(string errorStackId) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId))))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                .Select(e => new Error {
                    Id = e.Id,
                    OrganizationId = e.OrganizationId,
                    ProjectId = e.ProjectId,
                    ErrorStackId = errorStackId
                })
                .ToArray();

            while (errors.Length > 0) {
                Delete(errors);

                errors = Collection.Find(Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId))))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                    .Select(e => new Error {
                        Id = e.Id,
                        OrganizationId = e.OrganizationId,
                        ProjectId = e.ProjectId,
                        ErrorStackId = errorStackId
                    })
                    .ToArray();
            }
        }

        public async Task RemoveAllByErrorStackIdAsync(string errorStackId) {
            await Task.Run(() => RemoveAllByErrorStackId(errorStackId));
        }

        public void RemoveAllByDate(string organizationId, DateTime utcCutoffDate) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.And(
                Query.EQ(FieldNames.OrganizationId, new BsonObjectId(new ObjectId(organizationId))),
                Query.LT(FieldNames.OccurrenceDate_UTC, utcCutoffDate.Ticks)))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                .Select(e => new Error {
                    Id = e.Id,
                    OrganizationId = e.OrganizationId,
                    ProjectId = e.ProjectId,
                    ErrorStackId = e.ErrorStackId
                })
                .ToArray();

            while (errors.Length > 0) {
                Delete(errors);

                errors = Collection.Find(Query.And(
                    Query.EQ(FieldNames.OrganizationId, new BsonObjectId(new ObjectId(organizationId))),
                    Query.LT(FieldNames.OccurrenceDate_UTC, utcCutoffDate.Ticks)))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                    .Select(e => new Error {
                        Id = e.Id,
                        OrganizationId = e.OrganizationId,
                        ProjectId = e.ProjectId,
                        ErrorStackId = e.ErrorStackId
                    }).ToArray();
            }
        }

        public void RemoveAllByClientIpAndDate(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            const int batchSize = 150;

            var errors = Collection.Find(Query.And(
                Query.EQ(FieldNames.RequestInfo_ClientIpAddress, new BsonString(clientIp)),
                Query.GTE(FieldNames.OccurrenceDate_UTC, utcStartDate.Ticks),
                Query.LTE(FieldNames.OccurrenceDate_UTC, utcEndDate.Ticks)))
                .SetLimit(batchSize)
                .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                .Select(e => new Error {
                    Id = e.Id,
                    OrganizationId = e.OrganizationId,
                    ProjectId = e.ProjectId,
                    ErrorStackId = e.ErrorStackId
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
                    Query.GTE(FieldNames.OccurrenceDate_UTC, utcStartDate.Ticks),
                    Query.LTE(FieldNames.OccurrenceDate_UTC, utcEndDate.Ticks)))
                    .SetLimit(batchSize)
                    .SetFields(FieldNames.Id, FieldNames.OrganizationId, FieldNames.ProjectId)
                    .Select(e => new Error {
                        Id = e.Id,
                        OrganizationId = e.OrganizationId,
                        ProjectId = e.ProjectId,
                        ErrorStackId = e.ErrorStackId
                    })
                    .ToArray();
            }
        }

        public async Task RemoveAllByClientIpAndDateAsync(string clientIp, DateTime utcStartDate, DateTime utcEndDate) {
            await Task.Run(() => RemoveAllByClientIpAndDate(clientIp, utcStartDate, utcEndDate));
        }

        public override void Delete(IEnumerable<Error> errors) {
            var groups = errors.GroupBy(e => new {
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

            foreach (Error entity in errors)
                InvalidateCache(entity);
        }

        private void IncrementOrganizationAndProjectErrorCounts(string organizationId, string projectId, long count) {
            _organizationRepository.IncrementStats(organizationId, errorCount: -count);
            _projectRepository.IncrementStats(projectId, errorCount: -count);
        }

        #region Queries

        public IEnumerable<Error> GetMostRecent(string projectId, DateTime utcStart, DateTime utcEnd, int? skip, int? take, bool includeHidden = false, bool includeFixed = false, bool includeNotFound = true) {
            var conditions = new List<IMongoQuery> {
                Query.EQ(FieldNames.ProjectId, new BsonObjectId(new ObjectId(projectId)))
            };

            if (utcStart != DateTime.MinValue)
                conditions.Add(Query.GTE(FieldNames.OccurrenceDate_UTC, utcStart.Ticks));
            if (utcEnd != DateTime.MaxValue)
                conditions.Add(Query.LTE(FieldNames.OccurrenceDate_UTC, utcEnd.Ticks));

            if (!includeHidden)
                conditions.Add(Query.NE(FieldNames.IsHidden, true));

            if (!includeFixed)
                conditions.Add(Query.NE(FieldNames.IsFixed, true));

            if (!includeNotFound)
                conditions.Add(Query.NE(FieldNames.Code, "404"));

            var cursor = _collection.FindAs<Error>(Query.And(conditions));
            cursor.SetSortOrder(SortBy.Descending(FieldNames.OccurrenceDate_UTC));

            if (skip.HasValue)
                cursor.SetSkip(skip.Value);

            if (take.HasValue)
                cursor.SetLimit(take.Value);

            return cursor;
        }

        public IEnumerable<Error> GetByErrorStackIdOccurrenceDate(string errorStackId, DateTime utcStart, DateTime utcEnd, int? skip, int? take) {
            var cursor = _collection.FindAs<Error>(Query.And(Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(errorStackId))), Query.GTE(FieldNames.OccurrenceDate_UTC, utcStart.Ticks), Query.LTE(FieldNames.OccurrenceDate_UTC, utcEnd.Ticks)));
            cursor.SetSortOrder(SortBy.Descending(FieldNames.OccurrenceDate_UTC));

            if (skip.HasValue)
                cursor.SetSkip(skip.Value);

            if (take.HasValue)
                cursor.SetLimit(take.Value);

            return cursor;
        }

        public string GetPreviousErrorOccurrenceId(string id) {
            Error error = GetByIdCached(id);
            if (error == null)
                return null;

            var cursor = _collection.FindAs<Error>(
                                                   Query.And(
                                                             Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(error.ErrorStackId))),
                                                       Query.NE(FieldNames.Id, new BsonObjectId(new ObjectId(error.Id))),
                                                       Query.LTE(FieldNames.OccurrenceDate_UTC, error.OccurrenceDate.UtcTicks)));

            cursor.SetSortOrder(SortBy.Descending(FieldNames.OccurrenceDate_UTC));
            cursor.SetLimit(10);
            cursor.SetFields(FieldNames.Id, FieldNames.OccurrenceDate);

            var results = cursor.Select(e => Tuple.Create(e.Id, e.OccurrenceDate)).ToList();
            if (results.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (results.All(t => t.Item2 != error.OccurrenceDate))
                return results.OrderByDescending(t => t.Item2).ThenByDescending(t => t.Item1).First().Item1;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result before the target
            var unionResults = results.Union(new[] { Tuple.Create(error.Id, error.OccurrenceDate) })
                .OrderBy(t => t.Item2.UtcTicks).ThenBy(t => t.Item1)
                .ToList();

            var index = unionResults.FindIndex(t => t.Item1 == error.Id);
            return index == 0 ? null : unionResults[index - 1].Item1;
        }

        public string GetNextErrorOccurrenceId(string id) {
            Error error = GetByIdCached(id);
            if (error == null)
                return null;

            var cursor = _collection.FindAs<Error>(
                                                   Query.And(
                                                             Query.EQ(FieldNames.ErrorStackId, new BsonObjectId(new ObjectId(error.ErrorStackId))),
                                                       Query.NE(FieldNames.Id, new BsonObjectId(new ObjectId(error.Id))),
                                                       Query.GTE(FieldNames.OccurrenceDate_UTC, error.OccurrenceDate.UtcTicks)));

            cursor.SetSortOrder(SortBy.Ascending(FieldNames.OccurrenceDate_UTC));
            cursor.SetLimit(10);
            cursor.SetFields(FieldNames.Id, FieldNames.OccurrenceDate);

            var results = cursor.Select(e => Tuple.Create(e.Id, e.OccurrenceDate)).ToList();
            if (results.Count == 0)
                return null;

            // make sure we don't have records with the exact same occurrence date
            if (results.All(t => t.Item2 != error.OccurrenceDate))
                return results.OrderBy(t => t.Item2).ThenBy(t => t.Item1).First().Item1;

            // we have records with the exact same occurrence date, we need to figure out the order of those
            // put our target error into the mix, sort it and return the result after the target
            var unionResults = results.Union(new[] { Tuple.Create(error.Id, error.OccurrenceDate) })
                .OrderBy(t => t.Item2.Ticks).ThenBy(t => t.Item1)
                .ToList();

            var index = unionResults.FindIndex(t => t.Item1 == error.Id);
            return index == unionResults.Count - 1 ? null : unionResults[index + 1].Item1;
        }

        #endregion
    }
}