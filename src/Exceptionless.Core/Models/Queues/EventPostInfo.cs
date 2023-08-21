namespace Exceptionless.Core.Queues.Models;

public record EventPostInfo
{
    public required string OrganizationId { get; init; }
    public required string ProjectId { get; init; }
    public required string CharSet { get; init; }
    public required string MediaType { get; init; }
    public required int ApiVersion { get; init; }
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
    public string FilePath { get; set; } = null!;
}
