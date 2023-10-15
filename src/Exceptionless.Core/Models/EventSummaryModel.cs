namespace Exceptionless.Core.Models;

public record EventSummaryModel : SummaryData
{
    public DateTimeOffset Date { get; set; }
}
