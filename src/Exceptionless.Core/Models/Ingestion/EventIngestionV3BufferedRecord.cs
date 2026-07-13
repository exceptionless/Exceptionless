using System.Buffers;
using System.Text.Json;
using Exceptionless.Core.Serialization;

namespace Exceptionless.Core.Models.Ingestion;

/// <summary>
/// Owns one bounded NDJSON record and its routing-only projection. The complete event is
/// materialized only after stack routing proves that the record will continue through ingestion.
/// </summary>
internal sealed class EventIngestionV3BufferedRecord : IDisposable
{
    private IMemoryOwner<byte>? _owner;
    private EventIngestionV3Event? _materializedEvent;

    public EventIngestionV3BufferedRecord(
        IMemoryOwner<byte> owner,
        int length,
        EventIngestionV3Event routingEvent)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(routingEvent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (length > owner.Memory.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        _owner = owner;
        Length = length;
        RoutingEvent = routingEvent;
    }

    public int Length { get; }

    public EventIngestionV3Event RoutingEvent { get; }

    internal bool IsMaterialized => _materializedEvent is not null;

    public EventIngestionV3Event Materialize()
    {
        if (_materializedEvent is not null)
            return _materializedEvent;

        ObjectDisposedException.ThrowIf(_owner is null, this);
        var payload = JsonSerializer.Deserialize(
            _owner.Memory.Span[..Length],
            EventIngestionV3SurvivorJsonContext.Default.EventIngestionV3SurvivorPayload)
            ?? throw new JsonException("The stream cannot contain null events.");

        // Routing strings can dominate an error event, especially the raw stack trace. They were
        // already decoded by the cheap routing projection, so deserialize only the survivor-only
        // fields and reuse those references instead of allocating a second LOH-sized stack string.
        _materializedEvent = new EventIngestionV3Event
        {
            Id = RoutingEvent.Id,
            Type = RoutingEvent.Type,
            Date = payload.Date,
            Source = RoutingEvent.Source,
            Message = payload.Message,
            ReferenceId = payload.ReferenceId,
            Value = payload.Value,
            Tags = payload.Tags,
            Version = payload.Version,
            Level = payload.Level,
            Client = payload.Client,
            ExceptionType = RoutingEvent.ExceptionType,
            StackTrace = RoutingEvent.StackTrace,
            Stacking = RoutingEvent.Stacking is null
                ? null
                : new EventIngestionV3Stacking
                {
                    Title = payload.Stacking?.Title,
                    SignatureData = RoutingEvent.Stacking.SignatureData
                },
            User = payload.User,
            Request = payload.Request,
            Environment = payload.Environment,
            Data = payload.Data
        };
        return _materializedEvent;
    }

    public void Dispose()
    {
        _owner?.Dispose();
        _owner = null;
    }
}

/// <summary>
/// The fields that are needed only after discard routing. Routing properties are intentionally
/// absent so System.Text.Json skips their values without decoding duplicate strings.
/// </summary>
internal sealed record EventIngestionV3SurvivorPayload
{
    public DateTimeOffset? Date { get; init; }
    public string? Message { get; init; }
    public string? ReferenceId { get; init; }
    public decimal? Value { get; init; }
    public string[]? Tags { get; init; }
    public string? Version { get; init; }
    public string? Level { get; init; }
    public EventIngestionV3Client? Client { get; init; }
    public EventIngestionV3SurvivorStacking? Stacking { get; init; }
    public EventIngestionV3User? User { get; init; }
    public EventIngestionV3Request? Request { get; init; }
    public EventIngestionV3Environment? Environment { get; init; }
    public JsonElement? Data { get; init; }
}

internal sealed record EventIngestionV3SurvivorStacking
{
    public string? Title { get; init; }
}
