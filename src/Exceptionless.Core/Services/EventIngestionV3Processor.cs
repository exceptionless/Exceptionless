using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Data;
using Exceptionless.Core.Models.Ingestion;
using Exceptionless.Core.Pipeline;
using Exceptionless.Core.Plugins.EventProcessor;
using Exceptionless.Core.Queues.Models;
using Exceptionless.Core.Serialization;
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
    private const string PendingClaim = "pending";
    private const string StoredClaim = "stored";

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
        var completed = new List<PendingEvent>();
        int index = 0;
        foreach (var context in contexts)
        {
            var item = pending[index++];
            if (context.IsProcessed)
            {
                response.Persisted++;
                completed.Add(item);
            }
            else if (context.IsCancelled)
            {
                response.Discarded++;
                completed.Add(item);
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
        await MarkIdempotencyKeysStoredAsync(completed, project.Id);

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

        // Apply the same normalization V2 applies to deserialized event data before adding the
        // typed first-class values, which V2 clients send under the same data keys.
        EventDataNormalizer.Normalize(ev.Data);
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

        // An id is claimed as pending before processing. It becomes stored for the idempotency window
        // once the event is stored or discarded, and is released when the event is not processed, so
        // a resend after a failure is processed again. A pending claim expires shortly after the
        // request timeout in case this instance stops before finishing.
        var ingestionOptions = options.EventIngestionV3;
        TimeSpan pendingExpiration = TimeSpan.FromTicks(Math.Min(ingestionOptions.IdempotencyWindow.Ticks, (ingestionOptions.RequestTimeout + TimeSpan.FromMinutes(1)).Ticks));
        bool[] claimed;
        try
        {
            claimed = await Task.WhenAll(keyed.Select(p => cacheClient.AddAsync(p.IdempotencyKey!, PendingClaim, pendingExpiration)));
        }
        catch
        {
            // Some claims may have succeeded. Releasing them can at worst allow a duplicate,
            // while keeping them could drop the events when they are resent.
            await ReleaseIdempotencyKeysAsync(keyed);
            throw;
        }

        var unclaimed = keyed.Where((_, index) => !claimed[index]).ToList();
        if (unclaimed.Count == 0)
        {
            return pending;
        }

        var claims = await cacheClient.GetAllAsync<string>(unclaimed.Select(p => p.IdempotencyKey!));
        foreach (var item in unclaimed)
        {
            if (claims.TryGetValue(item.IdempotencyKey!, out var claim) && claim.HasValue && claim.Value == StoredClaim)
            {
                response.Duplicate++;
                continue;
            }

            // Another request is still processing this id. Reporting it as a duplicate would lose the
            // event if that request fails, so ask the client to resend it later instead.
            response.Failed++;
            response.AddError(item.Record.Index, item.Record.Event.Id, EventIngestionV3ErrorCodes.EventInProgress, "Another request is processing an event with this id. Resend it after the Retry-After delay.");
        }

        var unclaimedSet = new HashSet<PendingEvent>(unclaimed, ReferenceEqualityComparer.Instance);
        return pending.Where(p => !unclaimedSet.Contains(p)).ToList();
    }

    private async Task MarkIdempotencyKeysStoredAsync(IReadOnlyCollection<PendingEvent> events, string projectId)
    {
        var keys = events.Select(p => p.IdempotencyKey).OfType<string>().ToDictionary(key => key, _ => StoredClaim);
        if (keys.Count == 0)
        {
            return;
        }

        try
        {
            await cacheClient.SetAllAsync(keys, options.EventIngestionV3.IdempotencyWindow);
        }
        catch (Exception ex)
        {
            // The events are stored. Failing the request would make the client resend them while the
            // pending claims still exist, so only log; resends after the claims expire may duplicate.
            logger.LogError(ex, "Unable to record stored V3 event ids for project {ProjectId}: {Message}", projectId, ex.Message);
        }
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
