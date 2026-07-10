namespace Exceptionless.Core.Services;

public sealed record EventPostEnqueueResult(string? QueueEntryId = null, int? RejectedStatusCode = null, string? RejectionReason = null)
{
    public bool IsQueued => !String.IsNullOrEmpty(QueueEntryId);
    public bool IsRejected => RejectedStatusCode.HasValue;

    public static EventPostEnqueueResult Queued(string queueEntryId)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueEntryId);
        return new EventPostEnqueueResult(queueEntryId);
    }

    public static EventPostEnqueueResult Rejected(int statusCode, string? reason)
    {
        return new EventPostEnqueueResult(RejectedStatusCode: statusCode, RejectionReason: reason);
    }

    public static EventPostEnqueueResult Failed { get; } = new();
}
