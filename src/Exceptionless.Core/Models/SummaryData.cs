namespace Exceptionless.Core.Models;

public record SummaryData
{
    public required string Id { get; set; }
    public required string TemplateKey { get; set; }
    public object? Data { get; set; }
}
