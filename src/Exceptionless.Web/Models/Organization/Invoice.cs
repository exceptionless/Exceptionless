namespace Exceptionless.Web.Models;

public class Invoice
{
    public string Id { get; set; }
    public string OrganizationId { get; set; }
    public string OrganizationName { get; set; }

    public DateTime Date { get; set; }
    public bool Paid { get; set; }
    public decimal Total { get; set; }

    public IList<InvoiceLineItem> Items { get; set; } = new List<InvoiceLineItem>();
}
