using System.Text;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventParser;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Repositories.Base;
using Exceptionless.Core.Services;
using Exceptionless.Core.Validation;
using FluentValidation;
using Foundatio.Jobs;
using Foundatio.Queues;
using Foundatio.Repositories;
using Foundatio.Resilience;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Exceptionless.Core.Jobs;

[Job(Description = "Processes queued events.", InitialDelay = "2s")]
public class EventPostsJob : QueueJobBase<EventPost>
{
    private readonly long _maximumEventPostFileSize;
    private readonly long _maximumUncompressedEventPostSize;

    private readonly EventPostService _eventPostService;
    private readonly EventParserPluginManager _eventParserPluginManager;
    private readonly EventPipeline _eventPipeline;
    private readonly UsageService _usageService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly JsonSerializerSettings _jsonSerializerSettings;
    private readonly AppOptions _appOptions;

    public EventPostsJob(IQueue<EventPost> queue, EventPostService eventPostService, EventParserPluginManager eventParserPluginManager, EventPipeline eventPipeline, UsageService usageService, IOrganizationRepository organizationRepository, IProjectRepository projectRepository, JsonSerializerSettings jsonSerializerSettings, AppOptions appOptions, TimeProvider timeProvider,
        IResiliencePolicyProvider resiliencePolicyProvider, ILoggerFactory loggerFactory) : base(queue, timeProvider, resiliencePolicyProvider, loggerFactory)
    {
        _eventPostService = eventPostService;
        _eventParserPluginManager = eventParserPluginManager;
        _eventPipeline = eventPipeline;
        _usageService = usageService;
        _organizationRepository = organizationRepository;
        _projectRepository = projectRepository;
        _jsonSerializerSettings = jsonSerializerSettings;

        _appOptions = appOptions;
        _maximumEventPostFileSize = _appOptions.MaximumEventPostSize + 1024;
        _maximumUncompressedEventPostSize = _appOptions.MaximumEventPostSize * 10;

        AutoComplete = false;
    }

    protected override async Task<JobResult> ProcessQueueEntryAsync(QueueEntryContext<EventPost> context)
    {
        var entry = context.QueueEntry;
        var ep = entry.Value;
        using var _ = _logger.BeginScope(new ExceptionlessState().Organization(ep.OrganizationId).Project(ep.ProjectId));

        string payloadPath = Path.ChangeExtension(entry.Value.FilePath, ".payload");
        var payloadTask = AppDiagnostics.PostsMarkFileActiveTime.TimeAsync(() => _eventPostService.GetEventPostPayloadAsync(payloadPath));
        var projectTask = _projectRepository.GetByIdAsync(ep.ProjectId, o => o.Cache());
        var organizationTask = _organizationRepository.GetByIdAsync(ep.OrganizationId, o => o.Cache());

        byte[]? payload = await payloadTask;
        if (payload is null)
        {
            await Task.WhenAll(AbandonEntryAsync(entry), projectTask, organizationTask);
            return JobResult.FailedWithMessage($"Unable to retrieve payload '{payloadPath}'.");
        }

        AppDiagnostics.PostsMessageSize.Record(payload.LongLength);
        if (payload.LongLength > _maximumEventPostFileSize)
        {
            await Task.WhenAll(AppDiagnostics.PostsCompleteTime.TimeAsync(() => entry.CompleteAsync()), projectTask, organizationTask);
            return JobResult.FailedWithMessage($"Unable to process payload '{payloadPath}' ({payload.LongLength} bytes): Maximum event post size limit ({_appOptions.MaximumEventPostSize} bytes) reached.");
        }

        AppDiagnostics.PostsCompressedSize.Record(payload.Length);

        bool isDebugLogLevelEnabled = _logger.IsEnabled(LogLevel.Debug);
        bool isInternalProject = ep.ProjectId == _appOptions.InternalProjectId;
        if (!isInternalProject && _logger.IsEnabled(LogLevel.Information))
        {
            using var processingScope = _logger.BeginScope(new ExceptionlessState().Tag("processing").Tag("compressed").Tag(ep.ContentEncoding).Value(payload.Length));
            _logger.LogInformation("Processing post: id={QueueEntryId} path={FilePath} project={Project} ip={IpAddress} v={ApiVersion} agent={UserAgent}", entry.Id, payloadPath, ep.ProjectId, ep.IpAddress, ep.ApiVersion, ep.UserAgent);
        }

        var project = await projectTask;
        if (project is null)
        {
            if (!isInternalProject) _logger.LogError("Unable to process EventPost {FilePath}: Unable to load project: {Project}", payloadPath, ep.ProjectId);
            await Task.WhenAll(CompleteEntryAsync(entry, ep, _timeProvider.GetUtcNow().UtcDateTime), organizationTask);
            return JobResult.Success;
        }

        long maxEventPostSize = _appOptions.MaximumEventPostSize;
        byte[] uncompressedData = payload;
        if (!String.IsNullOrEmpty(ep.ContentEncoding))
        {
            if (!isInternalProject && isDebugLogLevelEnabled)
            {
                using (_logger.BeginScope(new ExceptionlessState().Tag("decompressing").Tag(ep.ContentEncoding)))
                    _logger.LogDebug("Decompressing EventPost: {QueueEntryId} ({CompressedBytes} bytes)", entry.Id, payload.Length);
            }

            maxEventPostSize = _maximumUncompressedEventPostSize;
            try
            {
                AppDiagnostics.PostsDecompressionTime.Time(() =>
                {
                    uncompressedData = uncompressedData.Decompress(ep.ContentEncoding);
                });
            }
            catch (Exception ex)
            {
                AppDiagnostics.PostsDecompressionErrors.Add(1);
                await Task.WhenAll(CompleteEntryAsync(entry, ep, _timeProvider.GetUtcNow().UtcDateTime), organizationTask);
                return JobResult.FailedWithMessage($"Unable to decompress EventPost data '{payloadPath}' ({payload.Length} bytes compressed): {ex.Message}");
            }
        }

        AppDiagnostics.PostsUncompressedSize.Record(payload.LongLength);
        if (uncompressedData.Length > maxEventPostSize)
        {
            var org = await organizationTask;
            await _usageService.IncrementTooBigAsync(org.Id, project.Id);
            await CompleteEntryAsync(entry, ep, _timeProvider.GetUtcNow().UtcDateTime);
            return JobResult.FailedWithMessage($"Unable to process decompressed EventPost data '{payloadPath}' ({payload.Length} bytes compressed, {uncompressedData.Length} bytes): Maximum uncompressed event post size limit ({maxEventPostSize} bytes) reached.");
        }

        if (!isInternalProject && isDebugLogLevelEnabled)
        {
            using (_logger.BeginScope(new ExceptionlessState().Tag("uncompressed").Value(uncompressedData.Length)))
                _logger.LogDebug("Processing uncompressed EventPost: {QueueEntryId}  ({UncompressedBytes} bytes)", entry.Id, uncompressedData.Length);
        }

        var createdUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var events = ParseEventPost(ep, createdUtc, uncompressedData, entry.Id, isInternalProject);
        if (events is null || events.Count == 0)
        {
            await Task.WhenAll(CompleteEntryAsync(entry, ep, createdUtc), organizationTask);
            return JobResult.Success;
        }

        if (context.CancellationToken.IsCancellationRequested)
        {
            await Task.WhenAll(AbandonEntryAsync(entry), organizationTask);
            return JobResult.Cancelled;
        }

        var organization = await organizationTask;
        if (organization is null)
        {
            if (!isInternalProject)
                _logger.LogError("Unable to process EventPost {FilePath}: Unable to load organization: {Organization}", payloadPath, project.OrganizationId);

            await CompleteEntryAsync(entry, ep, _timeProvider.GetUtcNow().UtcDateTime);
            return JobResult.Success;
        }

        // Don't process all the events if it will put the account over its limits.
        int eventsToProcess = await _usageService.GetEventsLeftAsync(organization.Id);
        if (eventsToProcess < 1)
        {
            if (!isInternalProject)
                _logger.LogDebug("Unable to process EventPost {FilePath}: Over plan limits", payloadPath);

            await _usageService.IncrementBlockedAsync(organization.Id, project.Id, events.Count);

            await CompleteEntryAsync(entry, ep, _timeProvider.GetUtcNow().UtcDateTime);
            return JobResult.Success;
        }

        // Keep track of the original event payload size, we can save some processing for retries in the case it was a massive batch.
        bool isSingleEvent = events.Count == 1;

        // Discard any events over the plan limit.
        if (eventsToProcess < events.Count)
        {
            int discarded = events.Count - eventsToProcess;
            events = events.Take(eventsToProcess).ToList();

            await _usageService.IncrementBlockedAsync(organization.Id, project.Id, discarded);
        }

        int errorCount = 0;
        var eventsToRetry = new List<PersistentEvent>();
        try
        {
            var contexts = await _eventPipeline.RunAsync(events, organization, project, ep);
            if (!isInternalProject && isDebugLogLevelEnabled)
            {
                using (_logger.BeginScope(new ExceptionlessState().Value(contexts.Count)))
                    _logger.LogDebug("Ran {@Value} events through the pipeline: id={QueueEntryId} success={SuccessCount} error={ErrorCount}", contexts.Count, entry.Id, contexts.Count(r => r.IsProcessed), contexts.Count(r => r.HasError));
            }

            // increment the plan usage counters (note: OverageHandler already incremented usage by 1)
            int processedEvents = contexts.Count(c => c.IsProcessed);
            await _usageService.IncrementTotalAsync(organization.Id, project.Id, processedEvents);

            int discardedEvents = contexts.Count(c => c.IsDiscarded);
            await _usageService.IncrementDiscardedAsync(organization.Id, project.Id, discardedEvents);

            foreach (var ctx in contexts)
            {
                if (ctx.IsCancelled)
                    continue;

                if (!ctx.HasError)
                    continue;

                if (!isInternalProject) _logger.LogError(ctx.Exception, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", entry.Id, payloadPath, ctx.ErrorMessage);
                if (ctx.Exception is ValidationException or MiniValidatorException)
                    continue;

                errorCount++;
                if (!isSingleEvent)
                {
                    // Put this single event back into the queue so we can retry it separately.
                    eventsToRetry.Add(ctx.Event);
                }
            }
        }
        catch (Exception ex)
        {
            if (!isInternalProject) _logger.LogError(ex, "Error processing EventPost {QueueEntryId} {FilePath}: {Message}", entry.Id, payloadPath, ex.Message);
            if (ex is ArgumentException || ex is DocumentNotFoundException)
            {
                await CompleteEntryAsync(entry, ep, createdUtc);
                return JobResult.Success;
            }

            errorCount++;
            if (!isSingleEvent)
                eventsToRetry.AddRange(events);
        }

        if (eventsToRetry.Count > 0)
            await AppDiagnostics.PostsRetryTime.TimeAsync(() => RetryEventsAsync(eventsToRetry, ep, entry, project, isInternalProject));

        if (isSingleEvent && errorCount > 0)
            await AbandonEntryAsync(entry);
        else
            await CompleteEntryAsync(entry, ep, createdUtc);

        return JobResult.Success;
    }

    private List<PersistentEvent>? ParseEventPost(EventPostInfo ep, DateTime createdUtc, byte[] uncompressedData, string queueEntryId, bool isInternalProject)
    {
        using (_logger.BeginScope(new ExceptionlessState().Tag("parsing")))
        {
            if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Parsing EventPost: {QueueEntryId}", queueEntryId);

            var events = new List<PersistentEvent>();
            try
            {
                var encoding = Encoding.UTF8;
                if (!String.IsNullOrEmpty(ep.CharSet))
                    encoding = Encoding.GetEncoding(ep.CharSet);

                AppDiagnostics.PostsParsingTime.Time(() =>
                {
                    string input = encoding.GetString(uncompressedData);
                    events = _eventParserPluginManager.ParseEvents(input, ep.ApiVersion, ep.UserAgent);
                    foreach (var ev in events)
                    {
                        ev.CreatedUtc = createdUtc;
                        ev.OrganizationId = ep.OrganizationId;
                        ev.ProjectId = ep.ProjectId;

                        // set the reference id to the event id if one was defined.
                        if (!String.IsNullOrEmpty(ev.Id) && String.IsNullOrEmpty(ev.ReferenceId))
                            ev.ReferenceId = ev.Id;

                        // the event id and stack id should never be set for posted events
                        ev.Id = ev.StackId = null!;
                    }
                });
                AppDiagnostics.PostsParsed.Add(1);
                AppDiagnostics.PostsEventCount.Record(events.Count);
            }
            catch (Exception ex)
            {
                AppDiagnostics.PostsParseErrors.Add(1);
                if (!isInternalProject) _logger.LogError(ex, "An error occurred while processing the EventPost {QueueEntryId}: {Message}", queueEntryId, ex.Message);
            }

            if (!isInternalProject && _logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Parsed {ParsedCount} events from EventPost: {QueueEntryId}", events?.Count ?? 0, queueEntryId);

            return events;
        }
    }

    private async Task RetryEventsAsync(List<PersistentEvent> eventsToRetry, EventPostInfo ep, IQueueEntry<EventPost> queueEntry, Project project, bool isInternalProject)
    {
        AppDiagnostics.EventsRetryCount.Add(eventsToRetry.Count);
        foreach (var ev in eventsToRetry)
        {
            try
            {
                var stream = new MemoryStream(ev.GetBytes(_jsonSerializerSettings));

                // Put this single event back into the queue so we can retry it separately.
                await _eventPostService.EnqueueAsync(new EventPost(false)
                {
                    ApiVersion = ep.ApiVersion,
                    CharSet = ep.CharSet,
                    ContentEncoding = null,
                    IpAddress = ep.IpAddress,
                    MediaType = ep.MediaType,
                    OrganizationId = ep.OrganizationId ?? project.OrganizationId,
                    ProjectId = ep.ProjectId ?? project.Id,
                    UserAgent = ep.UserAgent
                }, stream);
            }
            catch (Exception ex)
            {
                if (!isInternalProject && _logger.IsEnabled(LogLevel.Critical))
                {
                    using (_logger.BeginScope(new ExceptionlessState().Property("Event", new { ev.Date, ev.StackId, ev.Type, ev.Source, ev.Message, ev.Value, ev.Geo, ev.ReferenceId, ev.Tags })))
                        _logger.LogCritical(ex, "Error while requeuing event post {FilePath}: {Message}", queueEntry.Value.FilePath, ex.Message);
                }

                AppDiagnostics.EventsRetryErrors.Add(1);
            }
        }
    }

    private Task AbandonEntryAsync(IQueueEntry<EventPost> queueEntry)
    {
        return AppDiagnostics.PostsAbandonTime.TimeAsync(queueEntry.AbandonAsync);
    }

    private Task CompleteEntryAsync(IQueueEntry<EventPost> entry, EventPostInfo eventPostInfo, DateTime created)
    {
        return AppDiagnostics.PostsCompleteTime.TimeAsync(async () =>
        {
            await entry.CompleteAsync();
            await _eventPostService.CompleteEventPostAsync(entry.Value.FilePath, eventPostInfo.ProjectId, created, entry.Value.ShouldArchive);
        });
    }

    protected override void LogProcessingQueueEntry(IQueueEntry<EventPost> entry)
    {
        _logger.LogDebug("Processing {QueueName} queue entry ({QueueEntryId})", _queueName, entry.Id);
    }

    protected override void LogAutoCompletedQueueEntry(IQueueEntry<EventPost> entry)
    {
        _logger.LogDebug("Auto completed {QueueName} queue entry ({QueueEntryId})", _queueName, entry.Id);
    }
}
