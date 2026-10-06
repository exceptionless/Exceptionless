namespace Exceptionless.Core.Models.Ingestion;

/// <summary>
/// The outcome of a V3 ingestion request. Every received event is counted in exactly one outcome.
/// </summary>
public sealed record EventIngestionV3Response
{
    public const int MaximumErrors = 100;

    /// <summary>The number of events read from the request.</summary>
    public int Received { get; set; }

    /// <summary>Events that were stored.</summary>
    public int Persisted { get; set; }

    /// <summary>Events that were accepted but intentionally not stored, such as events for a discarded stack.</summary>
    public int Discarded { get; set; }

    /// <summary>Events whose id was already received within the idempotency window.</summary>
    public int Duplicate { get; set; }

    /// <summary>Events rejected because the organization reached its event limit.</summary>
    public int Blocked { get; set; }

    /// <summary>Events that could not be read or failed validation. Resending them unchanged fails again.</summary>
    public int Invalid { get; set; }

    /// <summary>Events that failed because of a temporary server problem. Resend them.</summary>
    public int Failed { get; set; }

    /// <summary>Details for up to the first 100 invalid or failed events.</summary>
    public List<EventIngestionV3Error> Errors { get; init; } = [];

    public void AddError(int index, string? id, string code, string message)
    {
        if (Errors.Count < MaximumErrors)
        {
            Errors.Add(new EventIngestionV3Error(index, id, code, message));
        }
    }

    public void Add(EventIngestionV3Response other)
    {
        Received += other.Received;
        Persisted += other.Persisted;
        Discarded += other.Discarded;
        Duplicate += other.Duplicate;
        Blocked += other.Blocked;
        Invalid += other.Invalid;
        Failed += other.Failed;

        foreach (var error in other.Errors)
        {
            AddError(error.Index, error.Id, error.Code, error.Message);
        }
    }
}

/// <summary>Describes why one event was not processed.</summary>
/// <param name="Index">The zero-based position of the event in the request.</param>
/// <param name="Id">The event id, when the event had one.</param>
/// <param name="Code">A stable machine-readable error code.</param>
/// <param name="Message">A description of the problem.</param>
public sealed record EventIngestionV3Error(int Index, string? Id, string Code, string Message);

public static class EventIngestionV3ErrorCodes
{
    public const string InvalidJson = "invalid_json";
    public const string InvalidEvent = "invalid_event";
    public const string EventTooLarge = "event_too_large";
    public const string ProcessingFailed = "processing_failed";
}
