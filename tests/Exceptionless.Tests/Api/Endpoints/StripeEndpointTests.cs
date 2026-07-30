using System.Net;
using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public class StripeEndpointTests : IntegrationTestsBase
{
    private const string WebhookSigningSecret = "whsec_local_test";
    private readonly IOrganizationRepository _organizationRepository;

    public StripeEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
    }

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

    [Theory]
    [InlineData(BillingStatus.Trialing)]
    [InlineData(BillingStatus.Active)]
    public async Task PostAsync_WithStaleSubscriptionDeletedEvent_DoesNotOverwriteNewerBillingState(BillingStatus billingStatus)
    {
        // Arrange
        var eventCreatedUtc = new DateTime(2026, 6, 22, 19, 3, 23, DateTimeKind.Utc);
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.BillingChangeDate = eventCreatedUtc.AddSeconds(20);
        organization.BillingStatus = billingStatus;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        /* language=json */
        const string json = $$"""
            {
              "id": "evt_subscription_deleted",
              "object": "event",
              "created": 1782155003,
              "data": {
                "object": {
                  "id": "sub_old",
                  "object": "subscription",
                  "customer": "cus_existing",
                  "status": "canceled"
                }
              },
              "livemode": false,
              "pending_webhooks": 1,
              "type": "customer.subscription.deleted"
            }
            """;

        long signatureTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        byte[] signatureBytes = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(WebhookSigningSecret),
            Encoding.UTF8.GetBytes($"{signatureTimestamp}.{json}")
        );

        var options = GetService<AppOptions>();
        string? originalSigningSecret = options.StripeOptions.StripeWebHookSigningSecret;
        options.StripeOptions.StripeWebHookSigningSecret = WebhookSigningSecret;
        try
        {
            // Act
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            await SendRequestAsync(r => r
                .Post()
                .AppendPath("stripe")
                .Content(content)
                .Header("Stripe-Signature", $"t={signatureTimestamp},v1={Convert.ToHexStringLower(signatureBytes)}")
                .StatusCodeShouldBeOk()
            );
        }
        finally
        {
            options.StripeOptions.StripeWebHookSigningSecret = originalSigningSecret;
        }

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(billingStatus, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
    }
}
