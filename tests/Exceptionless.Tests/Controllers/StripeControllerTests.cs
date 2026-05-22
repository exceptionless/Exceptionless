using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using FluentRest;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public class StripeControllerTests : IntegrationTestsBase
{
    public StripeControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory) { }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task PostAsync_WithEmptyBody_ReturnsBadRequest()
    {
        // Arrange & Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(new StringContent("", Encoding.UTF8, "application/json"))
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAsync_WithInvalidSignature_ReturnsBadRequest()
    {
        // Arrange
        string json = "{\"id\":\"evt_test\",\"type\":\"charge.succeeded\"}";

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(new StringContent(json, Encoding.UTF8, "application/json"))
            .Header("Stripe-Signature", "t=1234,v1=invalid_signature")
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAsync_WithMissingSignatureHeader_ReturnsBadRequest()
    {
        // Arrange
        string json = "{\"id\":\"evt_test\",\"type\":\"charge.succeeded\"}";

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AppendPath("stripe")
            .Content(new StringContent(json, Encoding.UTF8, "application/json"))
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
