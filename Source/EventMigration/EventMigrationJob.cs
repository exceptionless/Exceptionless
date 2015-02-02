using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Caching;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Lock;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Utility;
using Exceptionless.Extensions;
using Exceptionless.Models;
using Exceptionless.Models.Data;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using OldModels = Exceptionless.EventMigration.Models;

namespace Exceptionless.EventMigration {
    public class EventMigrationJob : MigrationJobBase {
        private readonly IQueue<EventMigrationBatch> _queue;

        public EventMigrationJob(IElasticClient elasticClient, EventUpgraderPluginManager eventUpgraderPluginManager, IValidator<Stack> stackValidator, IValidator<PersistentEvent> eventValidator, IGeoIPResolver geoIpResolver, ILockProvider lockProvider, ICacheClient cache)
            : base(elasticClient, eventUpgraderPluginManager, stackValidator, eventValidator, geoIpResolver, lockProvider, cache) {
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            // TODO: Queue up all days greater than the last day that we have completed which should be stored in redis.

            OutputPublicIp();
            QueueEntry<EventMigrationBatch> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue();
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("Error trying to dequeue message: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }

            if (queueEntry == null)
                return JobResult.Success;

            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var errorCollection = GetErrorCollection();
            var knownStackIds = new List<string>();

            var serializerSettings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Ignore };
            serializerSettings.AddModelConverters();

            var query = Query.And(Query.GTE(ErrorFieldNames.OccurrenceDate_UTC, queueEntry.Value.StartTicks), Query.LT(ErrorFieldNames.OccurrenceDate_UTC, queueEntry.Value.EndTicks));
            var errors = errorCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorFieldNames.OccurrenceDate_UTC)).SetLimit(_batchSize).ToList();
            while (errors.Count > 0) {
                Log.Info().Message("Migrating events {0}-{1} {2:N0} total {3:N0}/s...", errors.First().Id, errors.Last().Id, total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();

                var upgradedErrors = JArray.FromObject(errors);
                var ctx = new EventUpgraderContext(upgradedErrors, new Version(1, 5), true);
                _eventUpgraderPluginManager.Upgrade(ctx);

                var upgradedEvents = upgradedErrors.FromJson<PersistentEvent>(serializerSettings);

                var stackIdsToCheck = upgradedEvents.Where(e => !knownStackIds.Contains(e.StackId)).Select(e => e.StackId).Distinct().ToArray();
                if (stackIdsToCheck.Length > 0)
                    knownStackIds.AddRange(_eventRepository.ExistsByStackIds(stackIdsToCheck));
                        
                upgradedEvents.ForEach(e => {
                    if (e.Date.UtcDateTime > DateTimeOffset.UtcNow.AddHours(1))
                        e.Date = DateTimeOffset.Now;

                    e.CreatedUtc = e.Date.ToUniversalTime().DateTime;

                    if (!knownStackIds.Contains(e.StackId)) {
                        // We haven't processed this stack id yet in this run. Check to see if this stack has already been imported..
                        e.IsFirstOccurrence = true;
                        knownStackIds.Add(e.StackId);
                    }

                    var request = e.GetRequestInfo();   
                    if (request != null)
                        e.AddRequestInfo(request.ApplyDataExclusions(RequestInfoPlugin.DefaultExclusions, RequestInfoPlugin.MAX_VALUE_LENGTH));

                    foreach (var ip in GetIpAddresses(e, request)) {
                        var location = _geoIpResolver.ResolveIp(ip);
                        if (location == null || !location.IsValid())
                            continue;

                        e.Geo = location.ToString();
                        break;
                    }

                    if (e.Type == Event.KnownTypes.NotFound && request != null) {
                        if (String.IsNullOrWhiteSpace(e.Source)) {
                            e.Message = null;
                            e.Source = request.GetFullPath(includeHttpMethod: true, includeHost: false, includeQueryString: false);
                        }

                        return;
                    }
                         
                    var error = e.GetError();
                    if (error == null) {
                        Debugger.Break();
                        Log.Error().Project(e.ProjectId).Message("Unable to get parse error model: {0}", e.Id).Write();
                        return;
                    }

                    var stackingTarget = error.GetStackingTarget();
                    if (stackingTarget != null && stackingTarget.Method != null && !String.IsNullOrEmpty(stackingTarget.Method.GetDeclaringTypeFullName()))
                        e.Source = stackingTarget.Method.GetDeclaringTypeFullName().Truncate(2000);

                    var signature = new ErrorSignature(error);
                    if (signature.SignatureInfo.Count <= 0)
                        return;

                    var targetInfo = new SettingsDictionary(signature.SignatureInfo);
                    if (stackingTarget != null && stackingTarget.Error != null && !targetInfo.ContainsKey("Message"))
                        targetInfo["Message"] = error.GetStackingTarget().Error.Message;

                    error.Data[Error.KnownDataKeys.TargetInfo] = targetInfo;
                });

                try {
                    _eventRepository.Add(upgradedEvents, sendNotification: false);
                } catch (Exception) {
                    foreach (var persistentEvent in upgradedEvents) {
                        try {
                            _eventRepository.Add(persistentEvent, sendNotification: false);
                        } catch (Exception ex) {
                            //Debugger.Break();
                            Log.Error().Exception(ex).Message("An error occurred while migrating event '{0}': {1}", persistentEvent.Id, ex.Message).Write();
                        }
                    }
                }

                total += upgradedEvents.Count;
                var lastId = upgradedEvents.Last().Id;
                _cache.Set("migration-errorid", lastId);
                errors = errorCollection.Find(Query.GT(ErrorFieldNames.Id, ObjectId.Parse(lastId))).SetSortOrder(SortBy.Ascending(ErrorFieldNames.Id)).SetLimit(_batchSize).ToList();
            }

            queueEntry.Complete();

            return JobResult.Success;
        }
    }

    public class EventMigrationBatch {
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }
    }
}