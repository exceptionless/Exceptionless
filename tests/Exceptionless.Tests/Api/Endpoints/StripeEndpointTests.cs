using System.Net;
using System.Security.Cryptography;
using System.Text;
using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using FluentRest;
using Foundatio.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public class StripeEndpointTests : IntegrationTestsBase
{
    private const string WebhookSigningSecret = "whsec_local_test";
    private readonly IOrganizationRepository _organizationRepository;
    private readonly BillingPlans _plans;
    private FakeStripeBillingClient StripeBillingClient => (FakeStripeBillingClient)GetService<IStripeBillingClient>();

    public StripeEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _plans = GetService<BillingPlans>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.ReplaceSingleton<IStripeBillingClient, FakeStripeBillingClient>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        StripeBillingClient.Reset();
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
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionEventDate = eventCreatedUtc.AddSeconds(20);
        organization.BillingStatus = billingStatus;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_subscription_deleted", 1782155003, "customer.subscription.deleted", "sub_old", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(billingStatus, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
    }

    [Fact]
    public async Task PostAsync_WithQueuedUpdateBeforeOlderDeletion_UsesStripeEventWatermark()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_new";
        organization.BillingChangeDate = eventCreatedUtc.AddMinutes(-1);
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string updatedJson = CreateSubscriptionEvent("evt_subscription_updated", 1782155023, "customer.subscription.updated", "sub_new", "active");
        string deletedJson = CreateSubscriptionEvent("evt_subscription_deleted", 1782155003, "customer.subscription.deleted", "sub_old", "canceled");

        // Act
        await PostStripeEventAsync(updatedJson);
        await PostStripeEventAsync(deletedJson);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime, organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public async Task PostAsync_WithLegacyOrganizationAndDeletion_DoesNotUseLocalBillingChangeDateForOrdering()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155003).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = null;
        organization.StripeSubscriptionEventDate = null;
        organization.BillingChangeDate = eventCreatedUtc.AddSeconds(20);
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_legacy_subscription_deleted", 1782155003, "customer.subscription.deleted", "sub_old", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Canceled, organization.BillingStatus);
        Assert.True(organization.IsSuspended);
        Assert.Equal(eventCreatedUtc, organization.StripeSubscriptionEventDate);
        Assert.Null(organization.StripeSubscriptionId);
    }

    [Fact]
    public async Task PostAsync_WithLegacyOrganizationAndMultipleSubscriptions_ResolvesHealthyCurrentPlan()
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = null;
        organization.StripeSubscriptionEventDate = null;
        organization.BillingStatus = BillingStatus.PastDue;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_obsolete", "past_due", _plans.MediumPlan.Id));
        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_current", "active", _plans.SmallPlan.Id));
        string json = CreateSubscriptionEvent("evt_legacy_subscription_updated", 1782155023, "customer.subscription.updated", "sub_obsolete", "past_due");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal("sub_current", organization.StripeSubscriptionId);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Equal("cus_existing", StripeBillingClient.LastSubscriptionListOptions?.Customer);
    }

    [Fact]
    public async Task PostAsync_WithLegacyOrganizationAndObsoleteDeletion_PreservesResolvedLiveSubscription()
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = null;
        organization.StripeSubscriptionEventDate = null;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_current", "active", _plans.SmallPlan.Id));
        string json = CreateSubscriptionEvent("evt_obsolete_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_obsolete", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal("sub_current", organization.StripeSubscriptionId);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
    }

    [Fact]
    public async Task PostAsync_WithCurrentSubscriptionDeletionAndLiveReplacement_ReconcilesReplacement()
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_deleted";
        organization.StripeSubscriptionEventDate = DateTimeOffset.FromUnixTimeSeconds(1782155003).UtcDateTime;
        organization.BillingStatus = BillingStatus.PastDue;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_replacement", "active", _plans.SmallPlan.Id));
        string json = CreateSubscriptionEvent("evt_current_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_deleted", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal("sub_replacement", organization.StripeSubscriptionId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime, organization.StripeSubscriptionEventDate);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
    }

    [Fact]
    public async Task PostAsync_WithLegacyOrganizationAndTerminalUpdate_AppliesTerminalState()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = null;
        organization.StripeSubscriptionEventDate = null;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_legacy_terminal_update", 1782155023, "customer.subscription.updated", "sub_canceled", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Canceled, organization.BillingStatus);
        Assert.True(organization.IsSuspended);
        Assert.Null(organization.StripeSubscriptionId);
        Assert.Equal(eventCreatedUtc, organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public async Task PostAsync_WithNewerSubscriptionDeletedEvent_SuspendsOrganization()
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = DateTimeOffset.FromUnixTimeSeconds(1782155003).UtcDateTime;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_current", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Canceled, organization.BillingStatus);
        Assert.True(organization.IsSuspended);
        Assert.Equal("000000000000000000000000", organization.SuspendedByUserId);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime, organization.StripeSubscriptionEventDate);
        Assert.Null(organization.StripeSubscriptionId);
    }

    [Fact]
    public async Task PostAsync_WithSameSecondUpdateAfterDeletion_DoesNotReactivateDeletedSubscription()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = eventCreatedUtc.AddSeconds(-1);
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string deletedJson = CreateSubscriptionEvent("evt_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_current", "canceled");
        string updatedJson = CreateSubscriptionEvent("evt_subscription_updated", 1782155023, "customer.subscription.updated", "sub_current", "active");

        // Act
        await PostStripeEventAsync(deletedJson);
        await PostStripeEventAsync(updatedJson);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Canceled, organization.BillingStatus);
        Assert.True(organization.IsSuspended);
        Assert.Equal(eventCreatedUtc, organization.StripeSubscriptionEventDate);
        Assert.Null(organization.StripeSubscriptionId);
    }

    [Fact]
    public async Task PostAsync_WithSameSecondUpdates_ReconcilesCurrentProviderState()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = eventCreatedUtc;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_current", "active", _plans.SmallPlan.Id));
        string json = CreateSubscriptionEvent("evt_same_second_older_update", 1782155023, "customer.subscription.updated", "sub_current", "past_due");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Equal("sub_current", StripeBillingClient.LastGetSubscriptionId);
    }

    [Fact]
    public async Task PostAsync_WithUnsupportedSubscriptionStatus_DoesNotMutateBillingState()
    {
        // Arrange
        var eventWatermarkUtc = DateTimeOffset.FromUnixTimeSeconds(1782155003).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = eventWatermarkUtc;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_subscription_future", 1782155023, "customer.subscription.updated", "sub_current", "future_status");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Equal(eventWatermarkUtc, organization.StripeSubscriptionEventDate);
        Assert.Equal("sub_current", organization.StripeSubscriptionId);
    }

    [Theory]
    [InlineData("incomplete", BillingStatus.Unpaid, false)]
    [InlineData("paused", BillingStatus.Unpaid, false)]
    [InlineData("incomplete_expired", BillingStatus.Canceled, true)]
    public async Task PostAsync_WithNonpayingSubscriptionStatus_AppliesBillingState(string stripeStatus, BillingStatus billingStatus, bool clearsSubscriptionId)
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = DateTimeOffset.FromUnixTimeSeconds(1782155003).UtcDateTime;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_subscription_nonpaying", 1782155023, "customer.subscription.updated", "sub_current", stripeStatus);

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(billingStatus, organization.BillingStatus);
        Assert.True(organization.IsSuspended);
        Assert.Equal(clearsSubscriptionId ? null : "sub_current", organization.StripeSubscriptionId);
    }

    [Fact]
    public async Task PostAsync_WithSameSecondDeletionForObsoleteSubscription_DoesNotSuspendCurrentSubscription()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = eventCreatedUtc;
        organization.PlanId = _plans.SmallPlan.Id;
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_obsolete_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_obsolete", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Equal("sub_current", organization.StripeSubscriptionId);
        Assert.Equal(eventCreatedUtc, organization.StripeSubscriptionEventDate);
    }

    [Fact]
    public async Task PostAsync_WithDeletionForFreePlan_DoesNotSuspendOrganization()
    {
        // Arrange
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.StripeSubscriptionId = null;
        organization.BillingStatus = BillingStatus.Trialing;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_free_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_old", "canceled");

        // Act
        await PostStripeEventAsync(json);

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(BillingStatus.Trialing, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Null(organization.StripeSubscriptionId);
    }

    [Fact]
    public async Task PostAsync_ConcurrentPlanChange_RefetchesOrganizationAfterBillingLock()
    {
        // Arrange
        var eventCreatedUtc = DateTimeOffset.FromUnixTimeSeconds(1782155023).UtcDateTime;
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        organization.StripeCustomerId = "cus_existing";
        organization.PlanId = _plans.SmallPlan.Id;
        organization.StripeSubscriptionId = "sub_current";
        organization.StripeSubscriptionEventDate = eventCreatedUtc.AddSeconds(-1);
        organization.BillingStatus = BillingStatus.Active;
        organization.RemoveSuspension();
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        string json = CreateSubscriptionEvent("evt_subscription_deleted", 1782155023, "customer.subscription.deleted", "sub_current", "canceled");
        var billingManager = GetService<BillingManager>();
        var billingLock = await billingManager.TryAcquireOrganizationLockAsync(organization.Id);
        Assert.NotNull(billingLock);

        Task webhookTask;
        await using (billingLock)
        {
            webhookTask = PostStripeEventAsync(json);
            await Task.Delay(100, TestContext.Current.CancellationToken);
            Assert.False(webhookTask.IsCompleted);

            organization.PlanId = _plans.FreePlan.Id;
            organization.StripeSubscriptionId = null;
            organization.BillingStatus = BillingStatus.Trialing;
            organization.RemoveSuspension();
            await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
        }

        // Act
        await webhookTask;

        // Assert
        organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID, o => o.Cache(false));
        Assert.NotNull(organization);
        Assert.Equal(_plans.FreePlan.Id, organization.PlanId);
        Assert.Equal(BillingStatus.Trialing, organization.BillingStatus);
        Assert.False(organization.IsSuspended);
        Assert.Null(organization.StripeSubscriptionId);
    }

    private async Task PostStripeEventAsync(string json)
    {
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
    }

    private static string CreateSubscriptionEvent(string eventId, long created, string eventType, string subscriptionId, string status)
        => $$"""
            {
              "id": "{{eventId}}",
              "object": "event",
              "created": {{created}},
              "data": {
                "object": {
                  "id": "{{subscriptionId}}",
                  "object": "subscription",
                  "customer": "cus_existing",
                  "status": "{{status}}"
                }
              },
              "livemode": false,
              "pending_webhooks": 1,
              "type": "{{eventType}}"
            }
            """;

    private static Subscription CreateStripeSubscription(string id, string status, string priceId)
        => new()
        {
            Id = id,
            CustomerId = "cus_existing",
            Status = status,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Id = $"si_{id}", Price = new Price { Id = priceId } }]
            }
        };
}
