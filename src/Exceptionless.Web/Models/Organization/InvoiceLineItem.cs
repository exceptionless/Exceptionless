namespace Exceptionless.Web.Models;

public record InvoiceLineItem
{
    public required string Description { get; set; }
    public string? Date { get; set; }
    public required decimal Amount { get; set; }
}
