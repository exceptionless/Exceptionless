using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exceptionless.Core.AppStats;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Storage;
using Exceptionless.Json;
using Exceptionless.Models;
using FluentValidation;
using NLog.Fluent;

namespace Exceptionless.Core.Jobs {
    public class EventPostsJob : JobBase {
        private readonly IQueue<EventPostFileInfo> _queue;
        private readonly EventParserPluginManager _eventParserPluginManager;
        private readonly EventPipeline _eventPipeline;
        private readonly IAppStatsClient _statsClient;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IFileStorage _storage;

        public EventPostsJob(IQueue<EventPostFileInfo> queue, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, IAppStatsClient statsClient, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IFileStorage storage) {
            _queue = queue;
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
            _statsClient = statsClient;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _storage = storage;
        }

        public void RunUntilEmpty() {
            while (_queue.GetQueueCount() > 0)
                Run();
        }

        protected async override Task<JobResult> RunInternalAsync(CancellationToken token) {
            Log.Trace().Message("Process events job starting").Write();

            QueueEntry<EventPostFileInfo> queueEntry = null;
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

            EventPost eventPost = _storage.GetEventPostAndSetActive(queueEntry.Value.FilePath);
            if (eventPost == null) {
                queueEntry.Abandon();
                _storage.SetNotActive(queueEntry.Value.FilePath);
                return JobResult.FailedWithMessage(String.Format("Unable to retrieve post data '{0}'.", queueEntry.Value.FilePath));
            }

            _statsClient.Counter(StatNames.PostsDequeued);
            Log.Info().Message("Processing post: id={0} path={1} project={2} ip={3} v={4} agent={5}", queueEntry.Id, queueEntry.Value.FilePath, eventPost.ProjectId, eventPost.IpAddress, eventPost.ApiVersion, eventPost.UserAgent).Write();
            
            List<PersistentEvent> events = null;
            try {
                _statsClient.Time(() => {
                    events = ParseEventPost(eventPost);
                    Log.Info().Message("Parsed {0} events for post: id={1}", events.Count, queueEntry.Id).Write();
                }, StatNames.PostsParsingTime);
                _statsClient.Counter(StatNames.PostsParsed);
                _statsClient.Gauge(StatNames.PostsEventCount, events.Count);
            } catch (Exception ex) {
                _statsClient.Counter(StatNames.PostsParseErrors);
                queueEntry.Abandon();
                _storage.SetNotActive(queueEntry.Value.FilePath);

                Log.Error().Exception(ex).Message("An error occurred while processing the EventPost '{0}': {1}", queueEntry.Id, ex.Message).Write();
                return JobResult.FromException(ex, String.Format("An error occurred while processing the EventPost '{0}': {1}", queueEntry.Id, ex.Message));
            }

            if (token.IsCancellationRequested) {
                queueEntry.Abandon();
                return JobResult.Cancelled;
            }
       
            if (events == null) {
                queueEntry.Abandon();
                _storage.SetNotActive(queueEntry.Value.FilePath);
                return JobResult.Success;
            }

            int eventsToProcess = events.Count;
            bool isSingleEvent = events.Count == 1;
            if (!isSingleEvent) {
                var project = _projectRepository.GetById(eventPost.ProjectId, true);
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
                _storage.SetNotActive(queueEntry.Value.FilePath);
                return JobResult.Success;
            }

            var errorCount = 0;
            var created = DateTime.UtcNow;
            try {
                events.ForEach(e => e.CreatedUtc = created);
                var results = _eventPipeline.Run(events.Take(eventsToProcess).ToList());
                Log.Info().Message("Ran {0} events through the pipeline: id={1} project={2} success={3} error={4}", results.Count, queueEntry.Id, eventPost.ProjectId, results.Count(r => r.IsProcessed), results.Count(r => r.HasError)).Write();
                foreach (var eventContext in results) {
                    if (eventContext.IsCancelled)
                        continue;

                    if (!eventContext.HasError)
                        continue;

                    Log.Error().Exception(eventContext.Exception).Project(eventPost.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, eventContext.ErrorMessage).Write();
                    if (eventContext.Exception is ValidationException)
                        continue;

                    errorCount++;

                    if (!isSingleEvent) {
                        // Put this single event back into the queue so we can retry it separately.
                        _queue.Enqueue(new EventPost {
                            ApiVersion = eventPost.ApiVersion,
                            CharSet = eventPost.CharSet,
                            ContentEncoding = "application/json",
                            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventContext.Event)),
                            IpAddress = eventPost.IpAddress,
                            MediaType = eventPost.MediaType,
                            ProjectId = eventPost.ProjectId,
                            UserAgent = eventPost.UserAgent
                        }, _storage, false);
                    }
                }
            } catch (ArgumentException ex) {
                Log.Error().Exception(ex).Project(eventPost.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, ex.Message).Write();
                queueEntry.Complete();
            } catch (Exception ex) {
                Log.Error().Exception(ex).Project(eventPost.ProjectId).Message("Error while processing event post \"{0}\": {1}", queueEntry.Value.FilePath, ex.Message).Write();
                errorCount++;
            }

            if (isSingleEvent && errorCount > 0) {
                queueEntry.Abandon();
                _storage.SetNotActive(queueEntry.Value.FilePath);
            } else {
                queueEntry.Complete();
                if (queueEntry.Value.ShouldArchive)
                    _storage.CompleteEventPost(queueEntry.Value.FilePath, eventPost.ProjectId, created, queueEntry.Value.ShouldArchive);
                else {
                    _storage.DeleteFile(queueEntry.Value.FilePath);
                    _storage.SetNotActive(queueEntry.Value.FilePath);
                }
            }

            return JobResult.Success;
        }

        private List<PersistentEvent> ParseEventPost(EventPost ep) {
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
            });

            return events;
        }
    }
}