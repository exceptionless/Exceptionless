namespace Exceptionless.Web.Models;

public record InvoiceGridModel
{
    public required string Id { get; set; }
    public required DateTime Date { get; set; }
    public required bool Paid { get; set; }
}
