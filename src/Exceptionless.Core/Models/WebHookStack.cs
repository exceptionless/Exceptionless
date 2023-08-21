namespace Exceptionless.Core.Models;

public record WebHookStack
{
    private readonly string _baseUrl;

    public WebHookStack(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public required string Id { get; set; }
    public string Url => String.Concat(_baseUrl, "/stack/", Id);
    public required string Title { get; set; }
    public required string? Description { get; set; }

    public required TagSet Tags { get; set; }
    public string? RequestPath { get; set; }
    public string? Type { get; set; }
    public string? TargetMethod { get; set; }
    public required string ProjectId { get; set; }
    public required string ProjectName { get; set; }
    public required string OrganizationId { get; set; }
    public required string OrganizationName { get; set; }
    public int TotalOccurrences { get; set; }
    public DateTime FirstOccurrence { get; set; }
    public DateTime LastOccurrence { get; set; }
    public DateTime? DateFixed { get; set; }
    public string? FixedInVersion { get; set; }
    public bool IsRegression { get; set; }
    public bool IsCritical { get; set; }
}
