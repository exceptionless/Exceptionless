namespace Exceptionless.Core.Models.Ingestion;

public sealed record EventIngestionV3Response
{
    public int Received { get; set; }
    public int Persisted { get; set; }
    public int Discarded { get; set; }
    public int Duplicate { get; set; }
    public int Blocked { get; set; }
    public int Invalid { get; set; }
    public List<EventIngestionV3Error> Errors { get; init; } = [];

    public void Add(EventIngestionV3Response other)
    {
        Received += other.Received;
        Persisted += other.Persisted;
        Discarded += other.Discarded;
        Duplicate += other.Duplicate;
        Blocked += other.Blocked;
        Invalid += other.Invalid;

        int remaining = 100 - Errors.Count;
        if (remaining > 0 && other.Errors.Count > 0)
        {
            Errors.AddRange(other.Errors.Take(remaining));
        }
    }
}

public sealed record EventIngestionV3Error(string Id, string Code, string Message);
