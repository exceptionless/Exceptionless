using Exceptionless.Web.Models;

namespace Exceptionless.Web.Mapping;

/// <summary>
/// Mapper for Stripe Invoice to InvoiceGridModel.
/// Note: Created manually due to required properties and custom transformations.
/// </summary>
public class InvoiceMapper
{
    public InvoiceGridModel MapToInvoiceGridModel(Stripe.Invoice source)
        => new()
        {
            Id = source.Id[3..], // Strip "in_" prefix
            Date = source.Created,
            Paid = source.Paid
        };

    public List<InvoiceGridModel> MapToInvoiceGridModels(IEnumerable<Stripe.Invoice> source)
        => source.Select(MapToInvoiceGridModel).ToList();
}
