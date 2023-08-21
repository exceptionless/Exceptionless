namespace Exceptionless.Core.Models;

public record WebHookEvent
{
    private readonly string _baseUrl;

    public WebHookEvent(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public string Id { get; init; } = null!;
    public string Url => String.Concat(_baseUrl, "/event/", Id);
    public DateTimeOffset? OccurrenceDate { get; init; }
    public TagSet? Tags { get; init; }
    public string? Type { get; init; }
    public string? Source { get; init; }
    public string? Message { get; init; }
    public string ProjectId { get; init; } = null!;
    public string ProjectName { get; init; } = null!;
    public string OrganizationId { get; init; } = null!;
    public string OrganizationName { get; init; } = null!;
    public string StackId { get; init; } = null!;
    public string StackUrl => String.Concat(_baseUrl, "/stack/", StackId);
    public string StackTitle { get; init; } = null!;
    public string? StackDescription { get; init; }
    public TagSet StackTags { get; init; } = null!;
    public int TotalOccurrences { get; init; }
    public DateTime FirstOccurrence { get; init; }
    public DateTime LastOccurrence { get; init; }
    public DateTime? DateFixed { get; init; }
    public bool IsNew { get; init; }
    public bool IsRegression { get; init; }
    public bool IsCritical => Tags is not null && Tags.Contains("Critical");
}
