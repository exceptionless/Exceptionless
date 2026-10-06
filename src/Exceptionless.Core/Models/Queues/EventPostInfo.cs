using System.Text.Json.Serialization;

namespace Exceptionless.Core.Queues.Models;

public record EventPostInfo
{
    public string OrganizationId { get; init; } = null!;
    public string ProjectId { get; init; } = null!;
    public string? CharSet { get; init; }
    public string? MediaType { get; init; }
    public int ApiVersion { get; init; }
    // Internal GET submissions and retries already contain normalized event data.
    // Missing metadata on older queued posts retains the normal ingestion path.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsNormalized { get; init; }
    public string? UserAgent { get; init; }
    public string? ContentEncoding { get; init; }
    public string? IpAddress { get; init; }
    public string? ClientKeyHash { get; init; }
}

public record EventPost : EventPostInfo
{
    public EventPost(bool shouldArchive)
    {
        ShouldArchive = shouldArchive;
    }

    public bool ShouldArchive { get; init; }
    public string FilePath { get; set; } = null!;
}
