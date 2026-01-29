using Exceptionless.Core.Attributes;

namespace Exceptionless.Web.Models;

public record Invoice
{
    public required string Id { get; set; }
    [ObjectId]
    public required string OrganizationId { get; set; }
    public required string OrganizationName { get; set; }

    public required DateTime Date { get; set; }
    public required bool Paid { get; set; }
    public required decimal Total { get; set; }

    public IList<InvoiceLineItem> Items { get; set; } = new List<InvoiceLineItem>();
}
