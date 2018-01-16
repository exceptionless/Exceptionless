using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly long _maximumEventPostFileSize = Settings.Current.MaximumEventPostSize + 1000;
        private readonly long _maximumUncompressedEventPostSize = Settings.Current.MaximumEventPostSize * 10;

        private readonly EventPostService _eventPostService;
        private readonly EventParserPluginManager _eventParserPluginManager;
        private readonly EventPipeline _eventPipeline;
        private readonly IMetricsClient _metrics;
        private readonly UsageService _usageService;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public EventPostsJob(IQueue<EventPost> queue, EventPostService eventPostService, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, IMetricsClient metrics, UsageService usageService, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, JsonSerializerSettings jsonSerializerSettings, ILoggerFactory loggerFactory = null) : base(queue, loggerFactory) {
            _eventPostService = eventPostService;
            _eventParserPluginManager = eventParserPluginManager;
            _eventPipeline = eventPipeline;
            _metrics = metrics;
            _usageService = usageService;
            _organizationRepository = organizationRepository;
            _projectRepository = projectRepository;
            _jsonSerializerSettings = jsonSerializerSettings;

            AutoComplete = false;
        }

        protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context) {
            var entry = context.QueueEntry;
            var ep = entry.Value;
            string payloadPath = Path.ChangeExtension(entry.Value.FilePath, ".payload");
            var payloadTask = _metrics.TimeAsync(() => _eventPostService.GetEventPostPayloadAsync(payloadPath, context.CancellationToken), MetricNames.PostsMarkFileActiveTime);
            var projectTask = _projectRepository.GetByIdAsync(ep.ProjectId, o => o.Cache());
            var organizationTask = _organizationRepository.GetByIdAsync(ep.OrganizationId, o => o.Cache());

            var payload = await payloadTask.AnyContext();
            if (payload == null) {
                await Task.WhenAll(AbandonEntryAsync(entry), projectTask, organizationTask).AnyContext();
                return JobResult.FailedWithMessage($"Unable to retrieve payload '{payloadPath}'.");
            }

            _metrics.Gauge(MetricNames.PostsMessageSize, payload.LongLength);
            if (payload.LongLength > _maximumEventPostFileSize) {
                await Task.WhenAll(_metrics.TimeAsync(() => entry.CompleteAsync(), MetricNames.PostsCompleteTime), projectTask, organizationTask).AnyContext();
                return JobResult.FailedWithMessage($"Unable to process payload '{payloadPath}' ({payload.LongLength} bytes): Maximum event post size limit ({Settings.Current.MaximumEventPostSize} bytes) reached.");
            }

            using (_logger.BeginScope(new ExceptionlessState().Organization(ep.OrganizationId).Project(ep.ProjectId))) {
                _metrics.Gauge(MetricNames.PostsCompressedSize, payload.Length);

                bool isDebugLogLevelEnabled = _logger.IsEnabled(LogLevel.Debug);
                bool isInternalProject = ep.ProjectId == Settings.Current.InternalProjectId;
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Information)) {
                    using (_logger.BeginScope(new ExceptionlessState().Tag("processing").Tag("compressed").Tag(ep.ContentEncoding).Value(payload.Length)))
                        _logger.LogInformation("Processing post: id={QueueEntryId} path={FilePath} project={project} ip={IpAddress} v={ApiVersion} agent={UserAgent}", entry.Id, payloadPath, ep.ProjectId, ep.IpAddress, ep.ApiVersion, ep.UserAgent);
                }

                var project = await projectTask.AnyContext();
                if (project == null) {
                    if (!isInternalProject) _logger.LogError("Unable to process EventPost {FilePath}: Unable to load project: {Project}", payloadPath, ep.ProjectId);
                    await Task.WhenAll(CompleteEntryAsync(entry, ep, SystemClock.UtcNow), organizationTask).AnyContext();
                    return JobResult.Success;
                }

                long maxEventPostSize = Settings.Current.MaximumEventPostSize;
                var uncompressedData = payload;
                if (!String.IsNullOrEmpty(ep.ContentEncoding)) {
                    if (!isInternalProject && isDebugLogLevelEnabled) {
                        using (_logger.BeginScope(new ExceptionlessState().Tag("decompressing").Tag(ep.ContentEncoding)))
                            _logger.LogDebug("Decompressing EventPost: {QueueEntryId} ({CompressedBytes} bytes)", entry.Id, payload.Length);
                    }

                    maxEventPostSize = _maximumUncompressedEventPostSize;
                    try {
                        _metrics.Time(() => {
                            uncompressedData = uncompressedData.Decompress(ep.ContentEncoding);
                        }, MetricNames.PostsDecompressionTime);
                    } catch (Exception ex) {
                        _metrics.Counter(MetricNames.PostsDecompressionErrors);
                        await Task.WhenAll(CompleteEntryAsync(entry, ep, SystemClock.UtcNow), organizationTask).AnyContext();
                        return JobResult.FailedWithMessage($"Unable to decompress EventPost data '{payloadPath}' ({payload.Length} bytes compressed): {ex.Message}");
                    }
                }

                _metrics.Gauge(MetricNames.PostsUncompressedSize, payload.LongLength);
                if (uncompressedData.Length > maxEventPostSize) {
                    await Task.WhenAll(CompleteEntryAsync(entry, ep, SystemClock.UtcNow), organizationTask).AnyContext();
                    return JobResult.FailedWithMessage($"Unable to process decompressed EventPost data '{payloadPath}' ({payload.Length} bytes compressed, {uncompressedData.Length} bytes): Maximum uncompressed event post size limit ({maxEventPostSize} bytes) reached.");
                }

                if (!isInternalProject && isDebugLogLevelEnabled) {
                    using (_logger.BeginScope(new ExceptionlessState().Tag("uncompressed").Value(uncompressedData.Length)))
                        _logger.LogDebug("Processing uncompressed EventPost: {QueueEntryId}  ({UncompressedBytes} bytes)", entry.Id, uncompressedData.Length);
                }

                var createdUtc = SystemClock.UtcNow;
                var events = ParseEventPost(ep, payload, createdUtc, uncompressedData, entry.Id, isInternalProject);
                if (events == null || events.Count == 0) {
                    await Task.WhenAll(CompleteEntryAsync(entry, ep, createdUtc), organizationTask).AnyContext();
                    return JobResult.Success;
                }

                if (context.CancellationToken.IsCancellationRequested) {
                    await Task.WhenAll(AbandonEntryAsync(entry), organizationTask).AnyContext();
                    return JobResult.Cancelled;
                }

                var organization = await organizationTask.AnyContext();
                if (organization == null) {
                    if (!isInternalProject)
                        _logger.LogError("Unable to process EventPost {FilePath}: Unable to load organization: {OrganizationId}", payloadPath, project.OrganizationId);

                    await CompleteEntryAsync(entry, ep, SystemClock.UtcNow).AnyContext();
                    return JobResult.Success;
                }

                bool isSingleEvent = events.Count == 1;
                if (!isSingleEvent) {
                    await _metrics.TimeAsync(async () => {
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
                            _logger.LogDebug("Ran {@value} events through the pipeline: id={QueueEntryId} success={SuccessCount} error={ErrorCount}", contexts.Count, entry.Id, contexts.Count(r => r.IsProcessed), contexts.Count(r => r.HasError));
                    }

                    foreach (var ctx in contexts) {
                        if (ctx.IsCancelled)
                            continue;

                        if (!ctx.HasError)
                            continue;

                        if (!isInternalProject) _logger.LogError(ctx.Exception, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", entry.Id, payloadPath, ctx.ErrorMessage);
                        if (ctx.Exception is ValidationException)
                            continue;

                        errorCount++;
                        if (!isSingleEvent) {
                            // Put this single event back into the queue so we can retry it separately.
                            eventsToRetry.Add(ctx.Event);
                        }
                    }
                } catch (Exception ex) {
                    if (!isInternalProject) _logger.LogError(ex, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", entry.Id, payloadPath, ex.Message);
                    if (ex is ArgumentException || ex is DocumentNotFoundException) {
                        await CompleteEntryAsync(entry, ep, createdUtc).AnyContext();
                        return JobResult.Success;
                    }

                    errorCount++;
                    if (!isSingleEvent)
                        eventsToRetry.AddRange(events);
                }

                if (eventsToRetry.Count > 0)
                    await _metrics.TimeAsync(() => RetryEventsAsync(eventsToRetry, ep, entry, project, isInternalProject), MetricNames.PostsRetryTime).AnyContext();

                if (isSingleEvent && errorCount > 0)
                    await AbandonEntryAsync(entry).AnyContext();
                else
                    await CompleteEntryAsync(entry, ep, createdUtc).AnyContext();

                return JobResult.Success;
            }
        }

        private List<PersistentEvent> ParseEventPost(EventPostInfo ep, byte[] data, DateTime createdUtc, byte[] uncompressedData, string queueEntryId, bool isInternalProject) {
            using (_logger.BeginScope(new ExceptionlessState().Tag("parsing"))) {
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Parsing EventPost: {QueueEntryId}", queueEntryId);

                List<PersistentEvent> events = null;
                try {
                    var encoding = Encoding.UTF8;
                    if (!String.IsNullOrEmpty(ep.CharSet))
                        encoding = Encoding.GetEncoding(ep.CharSet);

                    _metrics.Time(() => {
                        string input = encoding.GetString(uncompressedData);
                        events = _eventParserPluginManager.ParseEvents(input, ep.ApiVersion, ep.UserAgent) ?? new List<PersistentEvent>(0);
                        foreach (var ev in events) {
                            ev.CreatedUtc = createdUtc;
                            ev.OrganizationId = ep.OrganizationId;
                            ev.ProjectId = ep.ProjectId;

                            // set the reference id to the event id if one was defined.
                            if (!String.IsNullOrEmpty(ev.Id) && String.IsNullOrEmpty(ev.ReferenceId))
                                ev.ReferenceId = ev.Id;

                            // the event id and stack id should never be set for posted events
                            ev.Id = ev.StackId = null;
                        }
                    }, MetricNames.PostsParsingTime);
                    _metrics.Counter(MetricNames.PostsParsed);
                    _metrics.Gauge(MetricNames.PostsEventCount, events.Count);
                } catch (Exception ex) {
                    _metrics.Counter(MetricNames.PostsParseErrors);
                    if (!isInternalProject) _logger.LogError(ex, "An error occurred while processing the EventPost {QueueEntryId}: {Message}", queueEntryId, ex.Message);
                }

                if(!isInternalProject && _logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug("Parsed {ParsedCount} events from EventPost: {QueueEntryId}", events?.Count ?? 0, queueEntryId);

                return events;
            }
        }

        private async Task RetryEventsAsync(List<PersistentEvent> eventsToRetry, EventPostInfo ep, IQueueEntry<EventPost> queueEntry, Project project, bool isInternalProject) {
            _metrics.Gauge(MetricNames.EventsRetryCount, eventsToRetry.Count);
            foreach (var ev in eventsToRetry) {
                try {
                    var stream = new MemoryStream(ev.GetBytes(_jsonSerializerSettings));

                    // Put this single event back into the queue so we can retry it separately.
                    await _eventPostService.EnqueueAsync(new EventPost {
                        ApiVersion = ep.ApiVersion,
                        CharSet = ep.CharSet,
                        ContentEncoding = null,
                        IpAddress = ep.IpAddress,
                        MediaType = ep.MediaType,
                        OrganizationId = ep.OrganizationId ?? project.OrganizationId,
                        ProjectId = ep.ProjectId,
                        UserAgent = ep.UserAgent,
                        ShouldArchive = false
                    }, stream).AnyContext();
                } catch (Exception ex) {
                    if (!isInternalProject && _logger.IsEnabled(LogLevel.Critical)) {
                        using (_logger.BeginScope(new ExceptionlessState().Property("Event", new { ev.Date, ev.StackId, ev.Type, ev.Source, ev.Message, ev.Value, ev.Geo, ev.ReferenceId, ev.Tags })))
                            _logger.LogCritical(ex, "Error while requeuing event post {FilePath}: {Message}", queueEntry.Value.FilePath, ex.Message);
                    }

                    _metrics.Counter(MetricNames.EventsRetryErrors);
                }
            }
        }

        private Task AbandonEntryAsync(IQueueEntry<EventPost> queueEntry) {
            return _metrics.TimeAsync(queueEntry.AbandonAsync, MetricNames.PostsAbandonTime);
        }

        private Task CompleteEntryAsync(IQueueEntry<EventPost> entry, EventPostInfo eventPostInfo, DateTime created) {
            return _metrics.TimeAsync(async () => {
                await entry.CompleteAsync().AnyContext();
                await _eventPostService.CompleteEventPostAsync(entry.Value.FilePath, eventPostInfo.ProjectId, created, entry.Value.ShouldArchive).AnyContext();
            }, MetricNames.PostsCompleteTime);
        }

        protected override void LogProcessingQueueEntry(IQueueEntry<EventPost> entry) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Processing {QueueEntryName} queue entry ({QueueEntryId}).", _queueEntryName, entry.Id);
        }

        protected override void LogAutoCompletedQueueEntry(IQueueEntry<EventPost> entry) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Auto completed {QueueEntryName} queue entry ({QueueEntryId}).", _queueEntryName, entry.Id);
        }
    }
}