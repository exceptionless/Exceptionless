namespace Exceptionless.Core.Models;

public record SummaryData
{
    public required string TemplateKey { get; set; }
    public required object Data { get; set; }
}
