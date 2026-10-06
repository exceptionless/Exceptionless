namespace Exceptionless.Core.Models;

public record EventSummaryModel : SummaryData
{
    public string? Environment { get; set; }
    public DateTimeOffset Date { get; set; }
    public string ProjectId { get; set; } = null!;
    public string? ProjectName { get; set; }
    public IReadOnlyCollection<string> Tags { get; set; } = [];
    public string? Type { get; set; }
    public string? Version { get; set; }
}
