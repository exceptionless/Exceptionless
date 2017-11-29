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
        private readonly IProjectRepository _projectRepository;
        private readonly IFileStorage _storage;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public EventPostsJob(IQueue<EventPost> queue, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, IMetricsClient metricsClient, UsageService usageService, IProjectRepository projectRepository, IFileStorage storage, JsonSerializerSettings jsonSerializerSettings, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
            _metricsClient = metricsClient;
            _usageService = usageService;
            _projectRepository = projectRepository;
            _storage = storage;
            _jsonSerializerSettings = jsonSerializerSettings;

            AutoComplete = false;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context) {
            var queueEntry = context.QueueEntry;
            FileSpec fileInfo = null;
            await _metricsClient.TimeAsync(async () => fileInfo = await _storage.GetFileInfoAsync(queueEntry.Value.FilePath).AnyContext(), MetricNames.PostsFileInfoTime).AnyContext();
            if (fileInfo == null) {
                await _metricsClient.TimeAsync(() => queueEntry.AbandonAsync(), MetricNames.PostsAbandonTime).AnyContext();
                return JobResult.FailedWithMessage($"Unable to retrieve post data info '{queueEntry.Value.FilePath}'.");
            }

            _metricsClient.Gauge(MetricNames.PostsMessageSize, fileInfo.Size);
            if (fileInfo.Size > GetMaximumEventPostFileSize()) {
                await _metricsClient.TimeAsync(() => queueEntry.CompleteAsync(), MetricNames.PostsCompleteTime).AnyContext();
                return JobResult.FailedWithMessage($"Unable to process post data '{queueEntry.Value.FilePath}' ({fileInfo.Size} bytes): Maximum event post size limit ({Settings.Current.MaximumEventPostSize} bytes) reached.");
            }

            EventPostInfo ep = null;
            await _metricsClient.TimeAsync(async () => ep = await _storage.GetEventPostAsync(queueEntry.Value.FilePath, _logger, context.CancellationToken).AnyContext(), MetricNames.PostsMarkFileActiveTime).AnyContext();
            if (ep == null) {
                await AbandonEntryAsync(queueEntry).AnyContext();
                return JobResult.FailedWithMessage($"Unable to retrieve post data '{queueEntry.Value.FilePath}'.");
            }

            using (_logger.BeginScope(new ExceptionlessState().Organization(ep.OrganizationId).Project(ep.ProjectId))) {
                _metricsClient.Gauge(MetricNames.PostsCompressedSize, ep.Data.Length);
                bool isInternalProject = ep.ProjectId == Settings.Current.InternalProjectId;
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Information)) {
                    using (_logger.BeginScope(new ExceptionlessState().Tag("processing", "compressed", ep.ContentEncoding).Value(ep.Data.Length)))
                        _logger.LogInformation("Processing post: id={QueueEntryId} path={FilePath} project={project} ip={IpAddress} v={ApiVersion} agent={UserAgent}", queueEntry.Id, queueEntry.Value.FilePath, ep.ProjectId, ep.IpAddress, ep.ApiVersion, ep.UserAgent);
                }

                var project = await _projectRepository.GetByIdAsync(ep.ProjectId, o => o.Cache()).AnyContext();
                if (project == null) {
                    if (!isInternalProject) _logger.LogError("Unable to process EventPost {FilePath}: Unable to load project: {project}", queueEntry.Value.FilePath, ep.ProjectId);
                    await CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow).AnyContext();
                    return JobResult.Success;
                }

                long maxEventPostSize = Settings.Current.MaximumEventPostSize;
                byte[] uncompressedData = ep.Data;
                if (!String.IsNullOrEmpty(ep.ContentEncoding)) {
                    if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug)) {
                        using (_logger.BeginScope(new ExceptionlessState().Tag("decompressing", ep.ContentEncoding)))
                            _logger.LogDebug("Decompressing EventPost: {QueueEntryId} ({CompressedBytes} bytes)", queueEntry.Id, ep.Data.Length);
                    }
                    maxEventPostSize = GetMaximumUncompressedEventPostSize();
                    try {
                        await _metricsClient.TimeAsync(async () => {
                            uncompressedData = uncompressedData.Decompress(ep.ContentEncoding);
                        }, MetricNames.PostsDecompressionTime).AnyContext();
                    } catch (Exception ex) {
                        _metricsClient.Counter(MetricNames.PostsDecompressionErrors);
                        await CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow).AnyContext();
                        return JobResult.FailedWithMessage($"Unable to decompress EventPost data '{queueEntry.Value.FilePath}' ({ep.Data.Length} bytes compressed): {ex.Message}");
                    }
                }

                _metricsClient.Gauge(MetricNames.PostsUncompressedSize, fileInfo.Size);
                if (uncompressedData.Length > maxEventPostSize) {
                    await CompleteEntryAsync(queueEntry, ep, SystemClock.UtcNow).AnyContext();
                    return JobResult.FailedWithMessage($"Unable to process decompressed EventPost data '{queueEntry.Value.FilePath}' ({ep.Data.Length} bytes compressed, {uncompressedData.Length} bytes): Maximum uncompressed event post size limit ({maxEventPostSize} bytes) reached.");
                }

                if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug)) {
                    using (_logger.BeginScope(new ExceptionlessState().Tag("uncompressed").Value(uncompressedData.Length)))
                        _logger.LogDebug("Processing uncompressed EventPost: {QueueEntryId}  ({UncompressedBytes} bytes)", queueEntry.Id, uncompressedData.Length);
                }

                var createdUtc = SystemClock.UtcNow;
                var events = await ParseEventPostAsync(ep, createdUtc, uncompressedData, queueEntry.Id, isInternalProject).AnyContext();
                if (events == null || events.Count == 0) {
                    await CompleteEntryAsync(queueEntry, ep, createdUtc).AnyContext();
                    return JobResult.Success;
                }

                if (context.CancellationToken.IsCancellationRequested) {
                    await AbandonEntryAsync(queueEntry).AnyContext();
                    return JobResult.Cancelled;
                }

                bool isSingleEvent = events.Count == 1;
                if (!isSingleEvent) {
                    await _metricsClient.TimeAsync(async () => {
                        // Don't process all the events if it will put the account over its limits.
                        int eventsToProcess = await _usageService.GetRemainingEventLimitAsync(project.OrganizationId).AnyContext();

                        // Add 1 because we already counted 1 against their limit when we received the event post.
                        if (eventsToProcess < Int32.MaxValue)
                            eventsToProcess += 1;

                        // Discard any events over there limit.
                        events = events.Take(eventsToProcess).ToList();

                        // Increment the count if greater than 1, since we already incremented it by 1 in the OverageHandler.
                        if (events.Count > 1)
                            await _usageService.IncrementUsageAsync(project.OrganizationId, project.Id, false, events.Count - 1, applyHourlyLimit: false).AnyContext();
                    }, MetricNames.PostsUpdateEventLimitTime).AnyContext();
                }

                int errorCount = 0;
                var eventsToRetry = new List<PersistentEvent>();
                try {
                    var contexts = await _eventPipeline.RunAsync(events, ep).AnyContext();
                    if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug)) {
                        using (_logger.BeginScope(new ExceptionlessState().Value(contexts.Count)))
                            _logger.LogDebug("Ran {@value} events through the pipeline: id={QueueEntryId} success={SuccessCount} error={ErrorCount}", contexts.Count, queueEntry.Id, contexts.Count(r => r.IsProcessed), contexts.Count(r => r.HasError));
                    }
                    foreach (var ctx in contexts) {
                        if (ctx.IsCancelled)
                            continue;

                        if (!ctx.HasError)
                            continue;

                        if (!isInternalProject) _logger.LogError(ctx.Exception, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", queueEntry.Id, queueEntry.Value.FilePath, ctx.ErrorMessage);
                        if (ctx.Exception is ValidationException)
                            continue;

                        errorCount++;
                        if (!isSingleEvent) {
                            // Put this single event back into the queue so we can retry it separately.
                            eventsToRetry.Add(ctx.Event);
                        }
                    }
                } catch (Exception ex) {
                    if (!isInternalProject) _logger.LogError(ex, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", queueEntry.Id, queueEntry.Value.FilePath, ex.Message);
                    if (ex is ArgumentException || ex is DocumentNotFoundException) {
                        await CompleteEntryAsync(queueEntry, ep, createdUtc).AnyContext();
                        return JobResult.Success;
                    }

                    errorCount++;
                    if (!isSingleEvent)
                        eventsToRetry.AddRange(events);
                }

                if (eventsToRetry.Count > 0)
                    await _metricsClient.TimeAsync(() => RetryEvents(eventsToRetry, ep, queueEntry, isInternalProject), MetricNames.PostsRetryTime).AnyContext();

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

        private async Task<List<PersistentEvent>> ParseEventPostAsync(EventPostInfo ep, DateTime createdUtc, byte[] uncompressedData, string queueEntryId, bool isInternalProject) {
            using (_logger.BeginScope(new ExceptionlessState().Tag("parsing"))) {
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("Parsing EventPost: {QueueEntryId}", queueEntryId);

                List<PersistentEvent> events = null;
                try {
                    var encoding = Encoding.UTF8;
                    if (!String.IsNullOrEmpty(ep.CharSet))
                        encoding = Encoding.GetEncoding(ep.CharSet);

                    await _metricsClient.TimeAsync(async () => {
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
                    }, MetricNames.PostsParsingTime).AnyContext();
                    _metricsClient.Counter(MetricNames.PostsParsed);
                    _metricsClient.Gauge(MetricNames.PostsEventCount, events.Count);
                } catch (Exception ex) {
                    _metricsClient.Counter(MetricNames.PostsParseErrors);
                    if (!isInternalProject) _logger.LogError(ex, "An error occurred while processing the EventPost {QueueEntryId}: {Message}", queueEntryId, ex.Message);
                }

                if(!isInternalProject && _logger.IsEnabled(LogLevel.Debug)) _logger.LogDebug("Parsed {ParsedCount} events from EventPost: {QueueEntryId}", events?.Count ?? 0, queueEntryId);
                return events;
            }
        }

        private async Task RetryEvents(List<PersistentEvent> eventsToRetry, EventPostInfo ep, IQueueEntry<EventPost> queueEntry, bool isInternalProject) {
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
                        OrganizationId = ep.OrganizationId,
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