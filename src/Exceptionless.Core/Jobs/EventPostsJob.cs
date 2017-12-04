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
using Exceptionless.Core.Services;
using FluentValidation;
using Foundatio.Jobs;
using Foundatio.Metrics;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Storage;
using Foundatio.Utility;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

#pragma warning disable 1998

namespace Exceptionless.Core.Jobs {
    [Job(Description = "Processes queued events.", InitialDelay = "2s")]
    public class EventPostsJob : QueueJobBase<EventPost> {
        private readonly EventParserPluginManager _eventParserPluginManager;
        private readonly EventPipeline _eventPipeline;
        private readonly IMetricsClient _metricsClient;
        private readonly UsageService _usageService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IFileStorage _storage;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public EventPostsJob(IQueue<EventPost> queue, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, IMetricsClient metricsClient, UsageService usageService, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, IFileStorage storage, JsonSerializerSettings jsonSerializerSettings, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
            _metricsClient = metricsClient;
            _usageService = usageService;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _storage = storage;
            _jsonSerializerSettings = jsonSerializerSettings;

            AutoComplete = false;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context) {
            var queueEntry = context.QueueEntry;
            string path = queueEntry.Value.FilePath;

            FileSpec fileInfo = null;
            await _metricsClient.TimeAsync(async () => fileInfo = await _storage.GetFileInfoAsync(path).AnyContext(), MetricNames.PostsFileInfoTime).AnyContext();
            if (fileInfo == null) {
                await _metricsClient.TimeAsync(() => queueEntry.AbandonAsync(), MetricNames.PostsAbandonTime).AnyContext();
                return JobResult.FailedWithMessage($"Unable to retrieve post data info '{path}'.");
            }

            _metricsClient.Gauge(MetricNames.PostsMessageSize, fileInfo.Size);
            if (fileInfo.Size > GetMaximumEventPostFileSize()) {
                await _metricsClient.TimeAsync(() => queueEntry.CompleteAsync(), MetricNames.PostsCompleteTime).AnyContext();
                return JobResult.FailedWithMessage($"Unable to process post data '{path}' ({fileInfo.Size} bytes): Maximum event post size limit ({Settings.Current.MaximumEventPostSize} bytes) reached.");
            }

            EventPostInfo ep = null;
            await _metricsClient.TimeAsync(async () => ep = await _storage.GetEventPostAsync(path, _logger, context.CancellationToken).AnyContext(), MetricNames.PostsMarkFileActiveTime).AnyContext();
            if (ep == null) {
                await AbandonEntryAsync(queueEntry).AnyContext();
                return JobResult.FailedWithMessage($"Unable to retrieve post data '{path}'.");
            }

            var projectTask = _projectRepository.GetByIdAsync(ep.ProjectId, o => o.Cache());
            var organizationTask = String.IsNullOrEmpty(ep.OrganizationId) ? _organizationRepository.GetByIdAsync(ep.OrganizationId, o => o.Cache()) : Task.FromResult<Organization>(null);

            using (_logger.BeginScope(new ExceptionlessState().Organization(ep.OrganizationId).Project(ep.ProjectId))) {
                _metricsClient.Gauge(MetricNames.PostsCompressedSize, ep.Data.Length);

                bool isDebugLogLevelEnabled = _logger.IsEnabled(LogLevel.Debug);
                bool isInternalProject = ep.ProjectId == Settings.Current.InternalProjectId;
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Information)) {
                    using (_logger.BeginScope(new ExceptionlessState().Tag("processing", "compressed", ep.ContentEncoding).Value(ep.Data.Length)))
                        _logger.LogInformation("Processing post: id={QueueEntryId} path={FilePath} project={project} ip={IpAddress} v={ApiVersion} agent={UserAgent}", queueEntry.Id, path, ep.ProjectId, ep.IpAddress, ep.ApiVersion, ep.UserAgent);
                }

                var project = await projectTask.AnyContext();
                if (project == null) {
                    if (!isInternalProject) _logger.LogError("Unable to process EventPost {FilePath}: Unable to load project: {Project}", path, ep.ProjectId);
                    await Task.WhenAll(CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow), organizationTask).AnyContext();
                    return JobResult.Success;
                }

                // The organization id will be null for legacy event posts.
                if (String.IsNullOrEmpty(ep.OrganizationId))
                    organizationTask = _organizationRepository.GetByIdAsync(project.OrganizationId, o => o.Cache());

                long maxEventPostSize = Settings.Current.MaximumEventPostSize;
                byte[] uncompressedData = ep.Data;
                if (!String.IsNullOrEmpty(ep.ContentEncoding)) {
                    if (!isInternalProject && isDebugLogLevelEnabled) {
                        using (_logger.BeginScope(new ExceptionlessState().Tag("decompressing", ep.ContentEncoding)))
                            _logger.LogDebug("Decompressing EventPost: {QueueEntryId} ({CompressedBytes} bytes)", queueEntry.Id, ep.Data.Length);
                    }

                    maxEventPostSize = GetMaximumUncompressedEventPostSize();
                    try {
                        _metricsClient.Time(() => {
                            uncompressedData = uncompressedData.Decompress(ep.ContentEncoding);
                        }, MetricNames.PostsDecompressionTime);
                    } catch (Exception ex) {
                        _metricsClient.Counter(MetricNames.PostsDecompressionErrors);
                        await Task.WhenAll(CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow), organizationTask).AnyContext();
                        return JobResult.FailedWithMessage($"Unable to decompress EventPost data '{path}' ({ep.Data.Length} bytes compressed): {ex.Message}");
                    }
                }

                _metricsClient.Gauge(MetricNames.PostsUncompressedSize, fileInfo.Size);
                if (uncompressedData.Length > maxEventPostSize) {
                    await Task.WhenAll(CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow), organizationTask).AnyContext();
                    return JobResult.FailedWithMessage($"Unable to process decompressed EventPost data '{path}' ({ep.Data.Length} bytes compressed, {uncompressedData.Length} bytes): Maximum uncompressed event post size limit ({maxEventPostSize} bytes) reached.");
                }

                if (!isInternalProject && isDebugLogLevelEnabled) {
                    using (_logger.BeginScope(new ExceptionlessState().Tag("uncompressed").Value(uncompressedData.Length)))
                        _logger.LogDebug("Processing uncompressed EventPost: {QueueEntryId}  ({UncompressedBytes} bytes)", queueEntry.Id, uncompressedData.Length);
                }

                var createdUtc = SystemClock.UtcNow;
                var events = ParseEventPost(ep, createdUtc, uncompressedData, queueEntry.Id, isInternalProject);
                if (events == null || events.Count == 0) {
                    await Task.WhenAll(CompleteEntryAsync(queueEntry, ep, createdUtc), organizationTask).AnyContext();
                    return JobResult.Success;
                }

                if (context.CancellationToken.IsCancellationRequested) {
                    await Task.WhenAll(AbandonEntryAsync(queueEntry), organizationTask).AnyContext();
                    return JobResult.Cancelled;
                }

                var organization = await organizationTask.AnyContext();
                if (organization == null) {
                    if (!isInternalProject)
                        _logger.LogError("Unable to process EventPost {FilePath}: Unable to load organization: {OrganizationId}", path, project.OrganizationId);

                    await CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow).AnyContext();
                    return JobResult.Success;
                }

                bool isSingleEvent = events.Count == 1;
                if (!isSingleEvent) {
                    await _metricsClient.TimeAsync(async () => {
                        // Don't process all the events if it will put the account over its limits.
                        int eventsToProcess = await _usageService.GetRemainingEventLimitAsync(organization).AnyContext();

                        // Add 1 because we already counted 1 against their limit when we received the event post.
                        if (eventsToProcess < Int32.MaxValue)
                            eventsToProcess += 1;

                        // Discard any events over there limit.
                        events = events.Take(eventsToProcess).ToList();

                        // Increment the count if greater than 1, since we already incremented it by 1 in the OverageHandler.
                        if (events.Count > 1)
                            await _usageService.IncrementUsageAsync(organization, project, false, events.Count - 1, applyHourlyLimit: false).AnyContext();
                    }, MetricNames.PostsUpdateEventLimitTime).AnyContext();
                }

                int errorCount = 0;
                var eventsToRetry = new List<PersistentEvent>();
                try {
                    var contexts = await _eventPipeline.RunAsync(events, organization, project, ep).AnyContext();
                    if (!isInternalProject && isDebugLogLevelEnabled) {
                        using (_logger.BeginScope(new ExceptionlessState().Value(contexts.Count)))
                            _logger.LogDebug("Ran {@value} events through the pipeline: id={QueueEntryId} success={SuccessCount} error={ErrorCount}", contexts.Count, queueEntry.Id, contexts.Count(r => r.IsProcessed), contexts.Count(r => r.HasError));
                    }

                    foreach (var ctx in contexts) {
                        if (ctx.IsCancelled)
                            continue;

                        if (!ctx.HasError)
                            continue;

                        if (!isInternalProject) _logger.LogError(ctx.Exception, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", queueEntry.Id, path, ctx.ErrorMessage);
                        if (ctx.Exception is ValidationException)
                            continue;

                        errorCount++;
                        if (!isSingleEvent) {
                            // Put this single event back into the queue so we can retry it separately.
                            eventsToRetry.Add(ctx.Event);
                        }
                    }
                } catch (Exception ex) {
                    if (!isInternalProject) _logger.LogError(ex, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", queueEntry.Id, path, ex.Message);
                    if (ex is ArgumentException || ex is DocumentNotFoundException) {
                        await CompleteEntryAsync(queueEntry, ep, createdUtc).AnyContext();
                        return JobResult.Success;
                    }

                    errorCount++;
                    if (!isSingleEvent)
                        eventsToRetry.AddRange(events);
                }

                if (eventsToRetry.Count > 0)
                    await _metricsClient.TimeAsync(() => RetryEventsAsync(eventsToRetry, ep, queueEntry, project, isInternalProject), MetricNames.PostsRetryTime).AnyContext();

                if (isSingleEvent && errorCount > 0)
                    await AbandonEntryAsync(queueEntry).AnyContext();
                else
                    await CompleteEntryAsync(queueEntry, ep, createdUtc).AnyContext();

                return JobResult.Success;
            }
        }

        private long GetMaximumEventPostFileSize() {
            return Settings.Current.MaximumEventPostSize + 1000;
        }

        private long GetMaximumUncompressedEventPostSize() {
            return Settings.Current.MaximumEventPostSize * 10;
        }

        private List<PersistentEvent> ParseEventPost(EventPostInfo ep, DateTime createdUtc, byte[] uncompressedData, string queueEntryId, bool isInternalProject) {
            using (_logger.BeginScope(new ExceptionlessState().Tag("parsing"))) {
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Parsing EventPost: {QueueEntryId}", queueEntryId);

                List<PersistentEvent> events = null;
                try {
                    var encoding = Encoding.UTF8;
                    if (!String.IsNullOrEmpty(ep.CharSet))
                        encoding = Encoding.GetEncoding(ep.CharSet);

                    _metricsClient.Time(() => {
                        string input = encoding.GetString(uncompressedData);
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
                    }, MetricNames.PostsParsingTime);
                    _metricsClient.Counter(MetricNames.PostsParsed);
                    _metricsClient.Gauge(MetricNames.PostsEventCount, events.Count);
                } catch (Exception ex) {
                    _metricsClient.Counter(MetricNames.PostsParseErrors);
                    if (!isInternalProject) _logger.LogError(ex, "An error occurred while processing the EventPost {QueueEntryId}: {Message}", queueEntryId, ex.Message);
                }

                if(!isInternalProject && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Parsed {ParsedCount} events from EventPost: {QueueEntryId}", events?.Count ?? 0, queueEntryId);

                return events;
            }
        }

        private async Task RetryEventsAsync(List<PersistentEvent> eventsToRetry, EventPostInfo ep, IQueueEntry<EventPost> queueEntry, Project project, bool isInternalProject) {
            _metricsClient.Gauge(MetricNames.EventsRetryCount, eventsToRetry.Count);
            foreach (var ev in eventsToRetry) {
                try {
                    string contentEncoding = null;
                    byte[] data = ev.GetBytes(_jsonSerializerSettings);
                    if (data.Length > 1000) {
                        data = data.Compress();
                        contentEncoding = "gzip";
                    }

                    // Put this single event back into the queue so we can retry it separately.
                    await _queue.Value.EnqueueAsync(new EventPostInfo {
                        ApiVersion = ep.ApiVersion,
                        CharSet = ep.CharSet,
                        ContentEncoding = contentEncoding,
                        Data = data,
                        IpAddress = ep.IpAddress,
                        MediaType = ep.MediaType,
                        OrganizationId = ep.OrganizationId ?? project.OrganizationId,
                        ProjectId = ep.ProjectId,
                        UserAgent = ep.UserAgent
                    }, _storage, false).AnyContext();
                } catch (Exception ex) {
                    if (!isInternalProject && _logger.IsEnabled(LogLevel.Critical)) {
                        using (_logger.BeginScope(new ExceptionlessState().Property("Event", new { ev.Date, ev.StackId, ev.Type, ev.Source, ev.Message, ev.Value, ev.Geo, ev.ReferenceId, ev.Tags })))
                            _logger.LogCritical(ex, "Error while requeuing event post {FilePath}: {Message}", queueEntry.Value.FilePath, ex.Message);
                    }

                    _metricsClient.Counter(MetricNames.EventsRetryErrors);
                }
            }
        }

        private Task AbandonEntryAsync(IQueueEntry<EventPost> queueEntry) {
            return _metricsClient.TimeAsync(queueEntry.AbandonAsync, MetricNames.PostsAbandonTime);
        }

        private Task CompleteEntryAsync(IQueueEntry<EventPost> queueEntry, EventPostInfo eventPostInfo, DateTime created) {
            return _metricsClient.TimeAsync(async () => {
                await queueEntry.CompleteAsync().AnyContext();
                await _storage.CompleteEventPostAsync(queueEntry.Value.FilePath, eventPostInfo.ProjectId, created, _logger, queueEntry.Value.ShouldArchive).AnyContext();
            }, MetricNames.PostsCompleteTime);
        }

        protected override void LogProcessingQueueEntry(IQueueEntry<EventPost> queueEntry) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Processing {QueueEntryName} queue entry ({QueueEntryId}).", _queueEntryName, queueEntry.Id);
        }

        protected override void LogAutoCompletedQueueEntry(IQueueEntry<EventPost> queueEntry) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Auto completed {QueueEntryName} queue entry ({QueueEntryId}).", _queueEntryName, queueEntry.Id);
        }
    }
}