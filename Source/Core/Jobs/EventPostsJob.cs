﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using FluentValidation;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Storage;
using Newtonsoft.Json;
using NLog.Fluent;
#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class EventPostsJob : JobBase {
        private readonly IQueue<EventPost> _queue;
        private readonly EventParserPluginManager _eventParserPluginManager;
        private readonly EventPipeline _eventPipeline;
        private readonly IMetricsClient _metricsClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IFileStorage _storage;

        public EventPostsJob(IQueue<EventPost> queue, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, IMetricsClient metricsClient, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IFileStorage storage) {
            _queue = queue;
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
            _metricsClient = metricsClient;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _storage = storage;
        }

        public void RunUntilEmpty() {
            while (_queue.GetQueueCount() > 0)
                Run();
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            QueueEntry<EventPost> queueEntry = null;
            try {
                queueEntry = _queue.Dequeue(TimeSpan.FromSeconds(1));
            } catch (Exception ex) {
                if (!(ex is TimeoutException)) {
                    Log.Error().Exception(ex).Message("An error occurred while trying to dequeue the next EventPost: {0}", ex.Message).Write();
                    return JobResult.FromException(ex);
                }
            }

            if (queueEntry == null)
                return JobResult.Success;

            if (token.IsCancellationRequested) {
                queueEntry.Abandon();
                return JobResult.Cancelled;
            }

            EventPostInfo eventPostInfo = await _storage.GetEventPostAndSetActiveAsync(queueEntry.Value.FilePath, token);
            if (eventPostInfo == null) {
                queueEntry.Abandon();
                await _storage.SetNotActiveAsync(queueEntry.Value.FilePath, token);
                return JobResult.FailedWithMessage(String.Format("Unable to retrieve post data '{0}'.", queueEntry.Value.FilePath));
            }

            bool isInternalProject = eventPostInfo.ProjectId == Settings.Current.InternalProjectId;
            await _metricsClient.CounterAsync(MetricNames.PostsDequeued);
            Log.Info().Message("Processing post: id={0} path={1} project={2} ip={3} v={4} agent={5}", queueEntry.Id, queueEntry.Value.FilePath, eventPostInfo.ProjectId, eventPostInfo.IpAddress, eventPostInfo.ApiVersion, eventPostInfo.UserAgent).WriteIf(!isInternalProject);
            
            List<PersistentEvent> events = null;
            try {
                _metricsClient.Time(() => {
                    events = ParseEventPost(eventPostInfo);
                    Log.Info().Message("Parsed {0} events for post: id={1}", events.Count, queueEntry.Id).WriteIf(!isInternalProject);
                }, MetricNames.PostsParsingTime);
                await _metricsClient.CounterAsync(MetricNames.PostsParsed);
                await _metricsClient.GaugeAsync(MetricNames.PostsEventCount, events.Count);
            } catch (Exception ex) {
                // TODO: Change to async once vnext is released.
                _metricsClient.Counter(MetricNames.PostsParseErrors);
                queueEntry.Abandon();
                _storage.SetNotActiveAsync(queueEntry.Value.FilePath, token).Wait(token);

                Log.Error().Exception(ex).Message("An error occurred while processing the EventPost '{0}': {1}", queueEntry.Id, ex.Message).Write();
                return JobResult.FromException(ex, String.Format("An error occurred while processing the EventPost '{0}': {1}", queueEntry.Id, ex.Message));
            }

            if (token.IsCancellationRequested) {
                queueEntry.Abandon();
                return JobResult.Cancelled;
            }
       
            if (events == null) {
                queueEntry.Abandon();
                await _storage.SetNotActiveAsync(queueEntry.Value.FilePath, token);
                return JobResult.Success;
            }

            int eventsToProcess = events.Count;
            bool isSingleEvent = events.Count == 1;
            if (!isSingleEvent) {
                var project = _projectRepository.GetById(eventPostInfo.ProjectId, true);
                // Don't process all the events if it will put the account over its limits.
                eventsToProcess = _organizationRepository.GetRemainingEventLimit(project.OrganizationId);

                // Add 1 because we already counted 1 against their limit when we received the event post.
                if (eventsToProcess < Int32.MaxValue)
                    eventsToProcess += 1;

                // Increment by count - 1 since we already incremented it by 1 in the OverageHandler.
                _organizationRepository.IncrementUsage(project.OrganizationId, false, events.Count - 1);
            }

            if (events == null) {
                queueEntry.Abandon();
                await _storage.SetNotActiveAsync(queueEntry.Value.FilePath, token);
                return JobResult.Success;
            }

            var errorCount = 0;
            var created = DateTime.UtcNow;
            try {
                events.ForEach(e => e.CreatedUtc = created);
                var results = await _eventPipeline.RunAsync(events.Take(eventsToProcess).ToList());
                Log.Info().Message("Ran {0} events through the pipeline: id={1} project={2} success={3} error={4}", results.Count, queueEntry.Id, eventPostInfo.ProjectId, results.Count(r => r.IsProcessed), results.Count(r => r.HasError)).WriteIf(!isInternalProject);
                foreach (var eventContext in results) {
                    if (eventContext.IsCancelled)
                        continue;

                    if (!eventContext.HasError)
                        continue;

                    Log.Error().Exception(eventContext.Exception).Project(eventPostInfo.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, eventContext.ErrorMessage).Write();
                    if (eventContext.Exception is ValidationException)
                        continue;

                    errorCount++;

                    if (!isSingleEvent) {
                        // Put this single event back into the queue so we can retry it separately.
                        await _queue.EnqueueAsync(new EventPostInfo {
                            ApiVersion = eventPostInfo.ApiVersion,
                            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventContext.Event)),
                            IpAddress = eventPostInfo.IpAddress,
                            MediaType = eventPostInfo.MediaType,
                            CharSet = eventPostInfo.CharSet,
                            ProjectId = eventPostInfo.ProjectId,
                            UserAgent = eventPostInfo.UserAgent
                        }, _storage, false, token);
                    }
                }
            } catch (ArgumentException ex) {
                Log.Error().Exception(ex).Project(eventPostInfo.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, ex.Message).Write();
                queueEntry.Complete();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Project(eventPostInfo.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, ex.Message).Write();
                errorCount++;
            }

            if (isSingleEvent && errorCount > 0) {
                queueEntry.Abandon();
                await _storage.SetNotActiveAsync(queueEntry.Value.FilePath, token);
            } else {
                queueEntry.Complete();
                if (queueEntry.Value.ShouldArchive)
                    await _storage.CompleteEventPostAsync(queueEntry.Value.FilePath, eventPostInfo.ProjectId, created, queueEntry.Value.ShouldArchive, token);
                else {
                    await _storage.DeleteFileAsync(queueEntry.Value.FilePath, token);
                    await _storage.SetNotActiveAsync(queueEntry.Value.FilePath, token);
                }
            }

            return JobResult.Success;
        }

        private List<PersistentEvent> ParseEventPost(EventPostInfo ep) {
            byte[] data = ep.Data;
            if (!String.IsNullOrEmpty(ep.ContentEncoding))
                data = data.Decompress(ep.ContentEncoding);

            var encoding = Encoding.UTF8;
            if (!String.IsNullOrEmpty(ep.CharSet))
                encoding = Encoding.GetEncoding(ep.CharSet);

            string input = encoding.GetString(data);
            List<PersistentEvent> events = _eventParserPluginManager.ParseEvents(input, ep.ApiVersion, ep.UserAgent);
            events.ForEach(e => {
                // set the project id on all events
                e.ProjectId = ep.ProjectId;

                // set the reference id to the event id if one was defined.
                if (!String.IsNullOrEmpty(e.Id) && String.IsNullOrEmpty(e.ReferenceId))
                    e.ReferenceId = e.Id;

                // the event id, stack id and organization id should never be set for posted events
                e.Id = e.StackId = e.OrganizationId = null;
            });

            return events;
        }
    }
}