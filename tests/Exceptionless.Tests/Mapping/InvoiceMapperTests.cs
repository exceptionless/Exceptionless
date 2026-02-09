using Exceptionless.Web.Mapping;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class InvoiceMapperTests
{
    private readonly InvoiceMapper _mapper;

    public InvoiceMapperTests()
    {
        _mapper = new InvoiceMapper();
    }

    [Fact]
    public void MapToInvoiceGridModel_WithValidInvoice_StripsIdPrefix()
    {
        // Arrange
        var source = new Stripe.Invoice
        {
            Id = "in_abc123",
            Created = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            Paid = true
        };

        // Act
        var result = _mapper.MapToInvoiceGridModel(source);

        // Assert
        Assert.Equal("abc123", result.Id);
    }

    [Fact]
    public void MapToInvoiceGridModel_WithValidInvoice_MapsDateAndPaid()
    {
        // Arrange
        var expectedDate = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        var source = new Stripe.Invoice
        {
            Id = "in_test123",
            Created = expectedDate,
            Paid = true
        };

        // Act
        var result = _mapper.MapToInvoiceGridModel(source);

        // Assert
        Assert.Equal(expectedDate, result.Date);
        Assert.True(result.Paid);
    }

    [Fact]
    public void MapToInvoiceGridModel_WithUnpaidInvoice_PaidIsFalse()
    {
        // Arrange
        var source = new Stripe.Invoice
        {
            Id = "in_unpaid",
            Created = DateTime.UtcNow,
            Paid = false
        };

        // Act
        var result = _mapper.MapToInvoiceGridModel(source);

        // Assert
        Assert.False(result.Paid);
    }

    [Fact]
    public void MapToInvoiceGridModels_WithMultipleInvoices_MapsAll()
    {
        // Arrange
        var invoices = new List<Stripe.Invoice>
        {
            new() { Id = "in_invoice1", Created = DateTime.UtcNow, Paid = true },
            new() { Id = "in_invoice2", Created = DateTime.UtcNow, Paid = false },
            new() { Id = "in_invoice3", Created = DateTime.UtcNow, Paid = true }
        };

        // Act
        var result = _mapper.MapToInvoiceGridModels(invoices);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("invoice1", result[0].Id);
        Assert.Equal("invoice2", result[1].Id);
        Assert.Equal("invoice3", result[2].Id);
    }

    [Fact]
    public void MapToInvoiceGridModels_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var invoices = new List<Stripe.Invoice>();

        // Act
        var result = _mapper.MapToInvoiceGridModels(invoices);

        // Assert
        Assert.Empty(result);
    }
}
