namespace Exceptionless.Core.Queues.Models;

public record EventPostInfo
{
    public string? OrganizationId { get; init; }
    public string? ProjectId { get; init; }
    public string? CharSet { get; init; }
    public string? MediaType { get; init; }
    public int ApiVersion { get; init; }
    public string? UserAgent { get; init; }
    public string? ContentEncoding { get; init; }
    public string? IpAddress { get; init; }
}

public record EventPost : EventPostInfo
{
    public EventPost(bool enableArchive)
    {
        ShouldArchive = enableArchive;
    }

    public bool ShouldArchive { get; init; }
    public required string FilePath { get; set; }
}
