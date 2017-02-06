using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using FluentValidation;
using Foundatio.Jobs;
using Foundatio.Logging;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Storage;
using Foundatio.Utility;
using Newtonsoft.Json;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    public class EventPostsJob : QueueJobBase<EventPost> {
        private readonly EventParserPluginManager _eventParserPluginManager;
        private readonly EventPipeline _eventPipeline;
        private readonly IMetricsClient _metricsClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IFileStorage _storage;

        public EventPostsJob(IQueue<EventPost> queue, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, IMetricsClient metricsClient, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IFileStorage storage, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
            _metricsClient = metricsClient;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _storage = storage;

            AutoComplete = false;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context) {
            var queueEntry = context.QueueEntry;
            var eventPostInfo = await _storage.GetEventPostAndSetActiveAsync(queueEntry.Value.FilePath, _logger, context.CancellationToken).AnyContext();
            if (eventPostInfo == null) {
                await AbandonEntryAsync(queueEntry).AnyContext();
                return JobResult.FailedWithMessage($"Unable to retrieve post data '{queueEntry.Value.FilePath}'.");
            }

            bool isInternalProject = eventPostInfo.ProjectId == Settings.Current.InternalProjectId;
            _logger.Info()
                .Message("Processing post: id={0} path={1} project={2} ip={3} v={4} agent={5}", queueEntry.Id, queueEntry.Value.FilePath, eventPostInfo.ProjectId, eventPostInfo.IpAddress, eventPostInfo.ApiVersion, eventPostInfo.UserAgent)
                .Property("ApiVersion", eventPostInfo.ApiVersion)
                .Property("IpAddress", eventPostInfo.IpAddress)
                .Property("Client", eventPostInfo.UserAgent)
                .Property("Project", eventPostInfo.ProjectId)
                .Property("@stack", "event-posted")
                .WriteIf(!isInternalProject);

            var project = await _projectRepository.GetByIdAsync(eventPostInfo.ProjectId, true).AnyContext();
            if (project == null) {
                _logger.Error().Project(eventPostInfo.ProjectId).Message("Unable to process EventPost \"{0}\": Unable to load project: {1}", queueEntry.Value.FilePath, eventPostInfo.ProjectId).WriteIf(!isInternalProject);
                await CompleteEntryAsync(queueEntry, eventPostInfo, SystemClock.UtcNow).AnyContext();
                return JobResult.Success;
            }

            var createdUtc = SystemClock.UtcNow;
            var events = await ParseEventPostAsync(eventPostInfo, createdUtc, queueEntry.Id, isInternalProject).AnyContext();
            if (events == null || events.Count == 0) {
                await CompleteEntryAsync(queueEntry, eventPostInfo, createdUtc).AnyContext();
                return JobResult.Success;
            }

            if (context.CancellationToken.IsCancellationRequested) {
                await AbandonEntryAsync(queueEntry).AnyContext();
                return JobResult.Cancelled;
            }

            bool isSingleEvent = events.Count == 1;
            if (!isSingleEvent) {
                // Don't process all the events if it will put the account over its limits.
                int eventsToProcess = await _organizationRepository.GetRemainingEventLimitAsync(project.OrganizationId).AnyContext();

                // Add 1 because we already counted 1 against their limit when we received the event post.
                if (eventsToProcess < Int32.MaxValue)
                    eventsToProcess += 1;

                // Discard any events over there limit.
                events = events.Take(eventsToProcess).ToList();

                // Increment the count if greater than 1, since we already incremented it by 1 in the OverageHandler.
                if (events.Count > 1)
                    await _organizationRepository.IncrementUsageAsync(project.OrganizationId, false, events.Count - 1, applyHourlyLimit: false).AnyContext();
            }

            int errorCount = 0;
            var eventsToRetry = new List<PersistentEvent>();
            try {
                var results = await _eventPipeline.RunAsync(events, eventPostInfo).AnyContext();
                _logger.Info()
                    .Message(() => $"Ran {results.Count} events through the pipeline: id={queueEntry.Id} project={eventPostInfo.ProjectId} success={results.Count(r => r.IsProcessed)} error={results.Count(r => r.HasError)}")
                    .Property("@value", results.Count)
                    .Property("@stack", "event-processed")
                    .Property("Project", eventPostInfo.ProjectId)
                    .WriteIf(!isInternalProject);

                foreach (var eventContext in results) {
                    if (eventContext.IsCancelled)
                        continue;

                    if (!eventContext.HasError)
                        continue;

                    _logger.Error().Exception(eventContext.Exception).Project(eventPostInfo.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, eventContext.ErrorMessage).Write();

                    if (eventContext.Exception is ValidationException)
                        continue;

                    errorCount++;
                    if (!isSingleEvent) {
                        // Put this single event back into the queue so we can retry it separately.
                        eventsToRetry.Add(eventContext.Event);
                    }
                }
            } catch (Exception ex) {
                _logger.Error().Exception(ex).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, ex.Message).Project(eventPostInfo.ProjectId).Write();
                if (ex is ArgumentException || ex is DocumentNotFoundException) {
                    await CompleteEntryAsync(queueEntry, eventPostInfo, createdUtc).AnyContext();
                    return JobResult.Success;
                }

                errorCount++;
                if (!isSingleEvent)
                    eventsToRetry.AddRange(events);
            }

            foreach (var requeueEvent in eventsToRetry) {
                // Put this single event back into the queue so we can retry it separately.
                await _queue.EnqueueAsync(new EventPostInfo {
                    ApiVersion = eventPostInfo.ApiVersion,
                    Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requeueEvent)),
                    IpAddress = eventPostInfo.IpAddress,
                    MediaType = eventPostInfo.MediaType,
                    CharSet = eventPostInfo.CharSet,
                    ProjectId = eventPostInfo.ProjectId,
                    UserAgent = eventPostInfo.UserAgent
                }, _storage, false, context.CancellationToken).AnyContext();
            }

            if (isSingleEvent && errorCount > 0)
                await AbandonEntryAsync(queueEntry).AnyContext();
            else
                await CompleteEntryAsync(queueEntry, eventPostInfo, createdUtc).AnyContext();

            return JobResult.Success;
        }

        private async Task AbandonEntryAsync(IQueueEntry<EventPost> queueEntry) {
            await queueEntry.AbandonAsync().AnyContext();
            await _storage.SetNotActiveAsync(queueEntry.Value.FilePath, _logger).AnyContext();
        }

        private async Task CompleteEntryAsync(IQueueEntry<EventPost> queueEntry, EventPostInfo eventPostInfo, DateTime created) {
            await queueEntry.CompleteAsync().AnyContext();
            await _storage.CompleteEventPostAsync(queueEntry.Value.FilePath, eventPostInfo.ProjectId, created, _logger, queueEntry.Value.ShouldArchive).AnyContext();
        }

        private async Task<List<PersistentEvent>> ParseEventPostAsync(EventPostInfo ep, DateTime createdUtc, string queueEntryId, bool isInternalProject) {
            List<PersistentEvent> events = null;

            try {
                await _metricsClient.TimeAsync(async () => {
                    byte[] data = ep.Data;
                    if (!String.IsNullOrEmpty(ep.ContentEncoding))
                        data = await data.DecompressAsync(ep.ContentEncoding).AnyContext();

                    var encoding = Encoding.UTF8;
                    if (!String.IsNullOrEmpty(ep.CharSet))
                        encoding = Encoding.GetEncoding(ep.CharSet);

                    string input = encoding.GetString(data);

                    events = _eventParserPluginManager.ParseEvents(input, ep.ApiVersion, ep.UserAgent) ?? new List<PersistentEvent>(0);
                    foreach (var ev in events) {
                        ev.CreatedUtc = createdUtc;

                        // set the project id on all events
                        ev.ProjectId = ep.ProjectId;

                        // set the reference id to the event id if one was defined.
                        if (!String.IsNullOrEmpty(ev.Id) && String.IsNullOrEmpty(ev.ReferenceId))
                            ev.ReferenceId = ev.Id;

                        // the event id, stack id and organization id should never be set for posted events
                        ev.Id = ev.StackId = ev.OrganizationId = null;
                    }
                }, MetricNames.PostsParsingTime).AnyContext();
                await _metricsClient.CounterAsync(MetricNames.PostsParsed).AnyContext();
                await _metricsClient.GaugeAsync(MetricNames.PostsEventCount, events.Count).AnyContext();
            } catch (Exception ex) {
                await _metricsClient.CounterAsync(MetricNames.PostsParseErrors).AnyContext();
                _logger.Error().Exception(ex).Message("An error occurred while processing the EventPost '{0}': {1}", queueEntryId, ex.Message).WriteIf(!isInternalProject);
            }

            return events;
        }

        protected override void LogProcessingQueueEntry(IQueueEntry<EventPost> queueEntry) {
            _logger.Debug().Message(() => $"Processing {_queueEntryName} queue entry ({queueEntry.Id}).").Write();
        }

        protected override void LogAutoCompletedQueueEntry(IQueueEntry<EventPost> queueEntry) {
            _logger.Debug().Message(() => $"Auto completed {_queueEntryName} queue entry ({queueEntry.Id}).").Write();
        }
    }
}
