using System.Net;
using System.Text;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Endpoints;

public class StripeEndpointTests : IntegrationTestsBase
{
    public StripeEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task PostAsync_WithEmptyBody_ReturnsBadRequest()
    {
        // Arrange
        using var content = new StringContent(String.Empty, Encoding.UTF8, "application/json");

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(content)
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PostAsync_WithInvalidSignature_ReturnsBadRequest()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"evt_test","type":"charge.succeeded"}""";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(content)
            .Header("Stripe-Signature", "t=1234,v1=invalid_signature")
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PostAsync_WithMissingSignatureHeader_ReturnsBadRequest()
    {
        // Arrange
        /* language=json */
        const string json = """{"id":"evt_test","type":"charge.succeeded"}""";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(content)
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PostAsync_WithNonJsonContentType_ReturnsUnsupportedMediaType()
    {
        // Arrange
        using var content = new StringContent("not json", Encoding.UTF8, "text/plain");

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(content)
            .ExpectedStatus(HttpStatusCode.UnsupportedMediaType)
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }
}
