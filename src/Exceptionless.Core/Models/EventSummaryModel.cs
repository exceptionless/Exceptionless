namespace Exceptionless.Core.Models;

public record EventSummaryModel : SummaryData
{
    public required string Id { get; set; }
    public DateTimeOffset Date { get; set; }
}
