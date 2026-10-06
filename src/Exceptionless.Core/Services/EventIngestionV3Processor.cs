using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Core.Validation;
using Foundatio.Caching;
using Microsoft.Extensions.Logging;

namespace Exceptionless.Core.Services;

/// <summary>
/// Processes one microbatch of V3 events inline through the same <see cref="EventPipeline"/>
/// that processes V2 event posts, so both versions stack, enrich, store, and notify identically.
/// </summary>
public sealed class EventIngestionV3Processor(
    EventPipeline eventPipeline,
    UsageService usageService,
    ICacheClient cacheClient,
    JsonSerializerOptions jsonOptions,
    AppOptions options,
    TimeProvider timeProvider,
    ILogger<EventIngestionV3Processor> logger)
{
    /// <summary>
    /// Options for reading V3 events. Like V2 event parsing, missing values are accepted rather than
    /// rejected so clients are not required to send every property of the nested data models.
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; } = new(jsonOptions) { RespectNullableAnnotations = false };

    public async Task<EventIngestionV3Response> ProcessAsync(
        IReadOnlyList<EventIngestionV3Record> records,
        Organization organization,
        Project project,
        EventPostInfo eventPostInfo,
        CancellationToken cancellationToken)
    {
        var response = new EventIngestionV3Response { Received = records.Count };
        if (records.Count == 0)
        {
            return response;
        }

        using var activity = AppDiagnostics.StartActivity("Ingestion V3 Microbatch");
        AppDiagnostics.IngestionV3MicroBatchSize.Record(records.Count);

        DateTimeOffset receivedDate = timeProvider.GetUtcNow();
        var pending = new List<PendingEvent>(records.Count);
        var batchKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            string? idempotencyKey = GetIdempotencyKey(project.Id, record.Event.Id);
            if (idempotencyKey is not null && !batchKeys.Add(idempotencyKey))
            {
                response.Duplicate++;
                continue;
            }

            pending.Add(new PendingEvent(record, ToPersistentEvent(record.Event, receivedDate), idempotencyKey));
        }

        cancellationToken.ThrowIfCancellationRequested();
        pending = await RemoveDuplicatesAsync(pending, response);
        if (pending.Count == 0)
        {
            return RecordOutcome(response);
        }

        // From here until the pipeline returns, any failure releases the claimed ids so a resend
        // of these events is processed instead of being acknowledged as a duplicate.
        ICollection<EventContext> contexts;
        try
        {
            int eventsLeft = Math.Max(await usageService.GetEventsLeftAsync(organization.Id), 0);
            if (eventsLeft < pending.Count)
            {
                var blocked = pending.GetRange(eventsLeft, pending.Count - eventsLeft);
                pending.RemoveRange(eventsLeft, blocked.Count);
                response.Blocked = blocked.Count;
                await ReleaseIdempotencyKeysAsync(blocked);
                await usageService.IncrementBlockedAsync(organization.Id, project.Id, blocked.Count);
            }

            if (pending.Count == 0)
            {
                return RecordOutcome(response);
            }

            contexts = await eventPipeline.RunAsync(pending.Select(p => p.Event), organization, project, eventPostInfo);
        }
        catch
        {
            await ReleaseIdempotencyKeysAsync(pending);
            throw;
        }

        var released = new List<PendingEvent>();
        int index = 0;
        foreach (var context in contexts)
        {
            var item = pending[index++];
            if (context.IsProcessed)
            {
                response.Persisted++;
            }
            else if (context.IsCancelled)
            {
                response.Discarded++;
            }
            else if (context.Exception is MiniValidatorException)
            {
                response.Invalid++;
                response.AddError(item.Record.Index, item.Record.Event.Id, EventIngestionV3ErrorCodes.InvalidEvent, context.ErrorMessage ?? "The event is invalid.");
                released.Add(item);
            }
            else
            {
                response.Failed++;
                response.AddError(item.Record.Index, item.Record.Event.Id, EventIngestionV3ErrorCodes.ProcessingFailed, "The event could not be processed. Resend it.");
                released.Add(item);
                if (logger.IsEnabled(LogLevel.Error))
                {
                    logger.LogError(context.Exception, "Error processing V3 event for project {ProjectId}: {Message}", project.Id, context.ErrorMessage);
                }
            }
        }

        await ReleaseIdempotencyKeysAsync(released);

        // Match EventPostsJob usage accounting for events that completed the pipeline.
        if (response.Persisted > 0)
        {
            await usageService.IncrementTotalAsync(organization.Id, project.Id, response.Persisted);
        }

        int discardedByRule = contexts.Count(c => c.IsDiscarded);
        if (discardedByRule > 0)
        {
            await usageService.IncrementDiscardedAsync(organization.Id, project.Id, discardedByRule);
        }

        return RecordOutcome(response);
    }

    internal static PersistentEvent ToPersistentEvent(EventIngestionV3Event source, DateTimeOffset receivedDate)
    {
        var ev = new PersistentEvent
        {
            Type = source.Type,
            Date = source.Date ?? receivedDate,
            Source = source.Source,
            Message = source.Message,
            ReferenceId = source.ReferenceId,
            Value = source.Value,
            Tags = source.Tags is { Length: > 0 } tags ? new TagSet(tags) : [],
            Data = source.Data ?? [],
            CreatedUtc = receivedDate.UtcDateTime
        };

        // First-class properties are stored under the same data keys V2 clients use.
        if (source.Error is not null)
        {
            ev.Data[Event.KnownDataKeys.Error] = source.Error;
        }
        else if (!String.IsNullOrWhiteSpace(source.ExceptionType) || !String.IsNullOrWhiteSpace(source.StackTrace))
        {
            ev.Data[Event.KnownDataKeys.SimpleError] = new SimpleError
            {
                Message = source.Message,
                Type = source.ExceptionType,
                StackTrace = source.StackTrace
            };
        }

        SetDataValue(ev, Event.KnownDataKeys.ManualStackingInfo, source.Stacking);
        SetDataValue(ev, Event.KnownDataKeys.UserInfo, source.User);
        SetDataValue(ev, Event.KnownDataKeys.RequestInfo, source.Request);
        SetDataValue(ev, Event.KnownDataKeys.EnvironmentInfo, source.Environment);
        if (!String.IsNullOrWhiteSpace(source.Version))
        {
            ev.Data[Event.KnownDataKeys.Version] = source.Version.Trim();
        }

        if (!String.IsNullOrWhiteSpace(source.Level))
        {
            ev.Data[Event.KnownDataKeys.Level] = source.Level.Trim();
        }

        if (String.IsNullOrWhiteSpace(ev.Type))
        {
            ev.Type = ev.HasError() || ev.HasSimpleError() ? Event.KnownTypes.Error : Event.KnownTypes.Log;
        }

        return ev;
    }

    private static void SetDataValue(PersistentEvent ev, string key, object? value)
    {
        if (value is not null)
        {
            ev.Data![key] = value;
        }
    }

    private async Task<List<PendingEvent>> RemoveDuplicatesAsync(List<PendingEvent> pending, EventIngestionV3Response response)
    {
        var keyed = pending.Where(p => p.IdempotencyKey is not null).ToList();
        if (keyed.Count == 0)
        {
            return pending;
        }

        // A key is claimed before processing and released again when the event is not stored,
        // so a resend after a failure is processed while a resend after success is a duplicate.
        bool[] claimed;
        try
        {
            claimed = await Task.WhenAll(keyed.Select(p => cacheClient.AddAsync(p.IdempotencyKey!, true, options.EventIngestionV3.IdempotencyWindow)));
        }
        catch
        {
            // Some claims may have succeeded. Releasing them can at worst allow a duplicate,
            // while keeping them could drop the events when they are resent.
            await ReleaseIdempotencyKeysAsync(keyed);
            throw;
        }
        var duplicates = new HashSet<PendingEvent>(ReferenceEqualityComparer.Instance);
        for (int index = 0; index < keyed.Count; index++)
        {
            if (!claimed[index])
            {
                duplicates.Add(keyed[index]);
            }
        }

        if (duplicates.Count == 0)
        {
            return pending;
        }

        response.Duplicate += duplicates.Count;
        return pending.Where(p => !duplicates.Contains(p)).ToList();
    }

    private async Task ReleaseIdempotencyKeysAsync(IEnumerable<PendingEvent> events)
    {
        string[] keys = events.Select(p => p.IdempotencyKey).OfType<string>().ToArray();
        if (keys.Length > 0)
        {
            await cacheClient.RemoveAllAsync(keys);
        }
    }

    private static string? GetIdempotencyKey(string projectId, string? id)
    {
        return String.IsNullOrEmpty(id) ? null : String.Concat("ingestion:v3:id:", projectId, ":", id.ToSHA256());
    }

    private static EventIngestionV3Response RecordOutcome(EventIngestionV3Response response)
    {
        AppDiagnostics.IngestionV3Persisted.Add(response.Persisted);
        AppDiagnostics.IngestionV3Discarded.Add(response.Discarded);
        AppDiagnostics.IngestionV3Duplicate.Add(response.Duplicate);
        AppDiagnostics.IngestionV3Blocked.Add(response.Blocked);
        AppDiagnostics.IngestionV3Invalid.Add(response.Invalid);
        AppDiagnostics.IngestionV3Failed.Add(response.Failed);
        return response;
    }

    private sealed class PendingEvent(EventIngestionV3Record record, PersistentEvent ev, string? idempotencyKey)
    {
        public EventIngestionV3Record Record { get; } = record;
        public PersistentEvent Event { get; } = ev;
        public string? IdempotencyKey { get; } = idempotencyKey;
    }
}
