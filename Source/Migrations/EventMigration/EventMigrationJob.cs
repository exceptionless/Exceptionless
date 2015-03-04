using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Geo;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Plugins.EventUpgrader;
using Exceptionless.Core.Utility;
using FluentValidation;
using Foundatio.Caching;
using Foundatio.Jobs;
using Foundatio.Lock;
using Foundatio.Queues;
using MongoDB.Driver.Builders;
using Nest;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog.Fluent;
using UAParser;
using OldModels = Exceptionless.EventMigration.Models;
#pragma warning disable 1998

namespace Exceptionless.EventMigration {
    public class EventMigrationJob : MigrationJobBase {
        private readonly JsonSerializerSettings _settings;
        private readonly IQueue<EventMigrationBatch> _queue;

        public EventMigrationJob(JsonSerializerSettings settings, IQueue<EventMigrationBatch> queue, IElasticClient elasticClient, EventUpgraderPluginManager eventUpgraderPluginManager, IValidator<Stack> stackValidator, IValidator<PersistentEvent> eventValidator, IGeoIPResolver geoIpResolver, ILockProvider lockProvider, ICacheClient cache)
            : base(elasticClient, eventUpgraderPluginManager, stackValidator, eventValidator, geoIpResolver, lockProvider, cache) {
            _settings = settings; 
            _queue = queue;
        }

        protected override async Task<JobResult> RunInternalAsync(CancellationToken token) {
            OutputPublicIp();
            QueueEntry<EventMigrationBatch> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue(TimeSpan.FromSeconds(1));
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("Error trying to dequeue message: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }

            if (queueEntry == null)
                return JobResult.Success;

            Log.Info().Message("Processing event migration jobs for date range: {0}-{1}", new DateTimeOffset(queueEntry.Value.StartTicks, TimeSpan.Zero).ToString("O"), new DateTimeOffset(queueEntry.Value.EndTicks, TimeSpan.Zero).ToString("O")).Write();
       
            int total = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var errorCollection = GetErrorCollection();
            var knownStackIds = new List<string>();

            var userAgentParser = Parser.GetDefault();

            var query = Query.And(Query.GTE(ErrorFieldNames.OccurrenceDate_UTC, queueEntry.Value.StartTicks), Query.LT(ErrorFieldNames.OccurrenceDate_UTC, queueEntry.Value.EndTicks));
            var errors = errorCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorFieldNames.OccurrenceDate_UTC)).SetLimit(_batchSize).ToList();
            int batch = 0;
            while (errors.Count > 0) {
                Log.Info().Message("Migrating events {0}-{1} {2:N0} total {3:N0}/s...", errors.First().Id, errors.Last().Id, total, total > 0 ? total / stopwatch.Elapsed.TotalSeconds : 0).Write();

                var upgradedErrors = JArray.FromObject(errors);
                var ctx = new EventUpgraderContext(upgradedErrors, new Version(1, 5), true);
                _eventUpgraderPluginManager.Upgrade(ctx);

                var upgradedEvents = upgradedErrors.FromJson<PersistentEvent>(_settings);

                var stackIdsToCheck = upgradedEvents.Where(e => !knownStackIds.Contains(e.StackId)).Select(e => e.StackId).Distinct().ToArray();
                if (stackIdsToCheck.Length > 0)
                    knownStackIds.AddRange(_eventRepository.ExistsByStackIds(stackIdsToCheck));
                        
                upgradedEvents.ForEach(e => {
                    if (e.Date.UtcDateTime > DateTimeOffset.UtcNow.AddHours(1))
                        e.Date = DateTimeOffset.Now;

                    e.CreatedUtc = e.Date.ToUniversalTime().DateTime;

                    // Truncate really large fields
                    if (e.Message != null && e.Message.Length > 2000) {
                        Log.Error().Project(e.ProjectId).Message("Event: {0} Message is Too Big: {1}", e.Id, e.Message.Length).Write();
                        e.Message = e.Message.Truncate(2000);
                    }

                    if (e.Source != null && e.Source.Length > 2000) {
                        Log.Error().Project(e.ProjectId).Message("Event: {0} Source is Too Big: {1}", e.Id, e.Source.Length).Write();
                        e.Source = e.Source.Truncate(2000);
                    }

                    if (!knownStackIds.Contains(e.StackId)) {
                        // We haven't processed this stack id yet in this run. Check to see if this stack has already been imported..
                        e.IsFirstOccurrence = true;
                        knownStackIds.Add(e.StackId);
                    }

                    var request = e.GetRequestInfo();
                    if (request != null) {
                        request = request.ApplyDataExclusions(RequestInfoPlugin.DefaultExclusions, RequestInfoPlugin.MAX_VALUE_LENGTH);

                        if (!String.IsNullOrEmpty(request.UserAgent)) {
                            try {
                                var info = userAgentParser.Parse(request.UserAgent);
                                if (!String.Equals(info.UserAgent.Family, "Other")) {
                                    request.Data[RequestInfo.KnownDataKeys.Browser] = info.UserAgent.Family;
                                    if (!String.IsNullOrEmpty(info.UserAgent.Major)) {
                                        request.Data[RequestInfo.KnownDataKeys.BrowserVersion] = String.Join(".", new[] { info.UserAgent.Major, info.UserAgent.Minor, info.UserAgent.Patch }.Where(v => !String.IsNullOrEmpty(v)));
                                        request.Data[RequestInfo.KnownDataKeys.BrowserMajorVersion] = info.UserAgent.Major;
                                    }
                                }

                                if (!String.Equals(info.Device.Family, "Other"))
                                    request.Data[RequestInfo.KnownDataKeys.Device] = info.Device.Family;


                                if (!String.Equals(info.OS.Family, "Other")) {
                                    request.Data[RequestInfo.KnownDataKeys.OS] = info.OS.Family;
                                    if (!String.IsNullOrEmpty(info.OS.Major)) {
                                        request.Data[RequestInfo.KnownDataKeys.OSVersion] = String.Join(".", new[] { info.OS.Major, info.OS.Minor, info.OS.Patch }.Where(v => !String.IsNullOrEmpty(v)));
                                        request.Data[RequestInfo.KnownDataKeys.OSMajorVersion] = info.OS.Major;
                                    }
                                }

                                request.Data[RequestInfo.KnownDataKeys.IsBot] = info.Device.IsSpider;
                            } catch (Exception ex) {
                                Log.Warn().Project(e.ProjectId).Message("Unable to parse user agent {0}. Exception: {1}", request.UserAgent, ex.Message).Write();
                            }
                        }

                        e.AddRequestInfo(request);
                    }

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
                        targetInfo["Message"] = stackingTarget.Error.Message;

                    error.Data[Error.KnownDataKeys.TargetInfo] = targetInfo;
                });

                Log.Info().Message("Saving events {0}-{1} {2:N0} total", errors.First().Id, errors.Last().Id, upgradedEvents.Count).Write();
                try {
                    _eventRepository.Add(upgradedEvents, sendNotification: false);
                } catch (Exception) {
                    foreach (var persistentEvent in upgradedEvents) {
                        try {
                            _eventRepository.Add(persistentEvent, sendNotification: false);
                        } catch (Exception ex) {
                            //Debugger.Break();
                            Log.Error().Exception(ex).Project(persistentEvent.ProjectId).Message("An error occurred while migrating event '{0}': {1}", persistentEvent.Id, ex.Message).Write();
                        }
                    }
                }

                batch++;
                total += upgradedEvents.Count;

                Log.Info().Message("Getting next batch of events").Write();
                var sw = new Stopwatch();
                sw.Start();
                errors = errorCollection.Find(query).SetSortOrder(SortBy.Ascending(ErrorFieldNames.OccurrenceDate_UTC)).SetLimit(_batchSize).SetSkip(_batchSize * batch).ToList();
                sw.Stop();
                Log.Info().Message("Finished getting next batch of events in {0}ms", sw.ElapsedMilliseconds).Write();
            }

            Log.Info().Message("Finished processing event migration jobs for date range: {0}-{1}", new DateTimeOffset(queueEntry.Value.StartTicks, TimeSpan.Zero).ToString("O"), new DateTimeOffset(queueEntry.Value.EndTicks, TimeSpan.Zero).ToString("O")).Write();
            _cache.Set("migration-completedperiod", queueEntry.Value.EndTicks);
            queueEntry.Complete();

            return JobResult.Success;
        }
    }

    public class EventMigrationBatch {
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }
    }
}