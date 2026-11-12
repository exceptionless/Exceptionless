using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Tests.Utility;
using Foundatio.Repositories;
using Foundatio.Repositories.Models;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Stripe;
using Xunit;

namespace Exceptionless.Tests.Services;

public sealed class OrganizationServiceTests : TestWithServices
{
    public OrganizationServiceTests(ITestOutputHelper output) : base(output) { }

    protected override void RegisterServices(IServiceCollection services, AppOptions options)
    {
        base.RegisterServices(services, options);
        services.ReplaceSingleton<IStripeBillingClient, FakeStripeBillingClient>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SoftDeleteOrganization_Failure_PersistsDeletionAfterMembersBeforeResourceCleanup(bool deletionSaveFails)
    {
        // Arrange
        var organization = new Organization { Id = TestConstants.OrganizationId, Name = "Deleting organization" };
        var persistenceFailure = new IOException("Deletion marker failed");
        var cleanupFailure = new IOException("Token cleanup failed");
        bool persistedDeleted = false;
        bool membersProcessed = false;
        bool membersProcessedBeforeSave = false;
        var organizations = new Mock<IOrganizationRepository>(MockBehavior.Strict);
        organizations.Setup(repository => repository.SaveAsync(organization, It.IsAny<CommandOptionsDescriptor<Organization>>()))
            .Returns(() =>
            {
                membersProcessedBeforeSave = membersProcessed;
                if (deletionSaveFails)
                    return Task.FromException<Organization>(persistenceFailure);

                persistedDeleted = organization.IsDeleted;
                return Task.FromResult(organization);
            });
        var users = new Mock<IUserRepository>(MockBehavior.Strict);
        users.Setup(repository => repository.GetByOrganizationIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<User>>()))
            .Returns(() =>
            {
                membersProcessed = true;
                return Task.FromResult(FindResults<User>.Empty);
            });
        var tokens = new Mock<ITokenRepository>(MockBehavior.Strict);
        tokens.Setup(repository => repository.RemoveAllByOrganizationIdAsync(organization.Id)).ThrowsAsync(cleanupFailure);
        var service = new OrganizationService(organizations.Object,
            new Mock<IProjectRepository>(MockBehavior.Strict).Object,
            new Mock<ISavedViewRepository>(MockBehavior.Strict).Object,
            tokens.Object, users.Object,
            new Mock<IWebHookRepository>(MockBehavior.Strict).Object,
            new Mock<IStripeBillingClient>(MockBehavior.Strict).Object,
            GetService<UsageService>(), GetService<Foundatio.Lock.ILockProvider>(), Log);

        // Act
        var exception = await Record.ExceptionAsync(() => service.SoftDeleteOrganizationAsync(organization, TestConstants.UserId));

        // Assert
        Assert.Same(deletionSaveFails ? persistenceFailure : cleanupFailure, exception);
        Assert.Equal(!deletionSaveFails, persistedDeleted);
        Assert.True(membersProcessedBeforeSave);
        organizations.Verify(repository => repository.SaveAsync(organization, It.IsAny<CommandOptionsDescriptor<Organization>>()), Times.Once());
        users.Verify(repository => repository.GetByOrganizationIdAsync(organization.Id, It.IsAny<CommandOptionsDescriptor<User>>()),
            Times.Once());
        tokens.Verify(repository => repository.RemoveAllByOrganizationIdAsync(organization.Id),
            deletionSaveFails ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task CancelSubscriptionsAsync_WithoutStripeCustomer_DoesNotCallStripe()
    {
        // Arrange
        var stripeClient = Assert.IsType<FakeStripeBillingClient>(GetService<IStripeBillingClient>());
        var service = GetService<OrganizationService>();

        // Act
        await service.CancelSubscriptionsAsync(new Organization { Id = "000000000000000000000001", Name = "Test" });

        // Assert
        Assert.Null(stripeClient.LastSubscriptionListOptions);
        Assert.Empty(stripeClient.CanceledSubscriptions);
    }

    [Fact]
    public async Task CancelSubscriptionsAsync_ActiveSubscriptions_CancelsOnlyActiveSubscriptions()
    {
        // Arrange
        var stripeClient = Assert.IsType<FakeStripeBillingClient>(GetService<IStripeBillingClient>());
        stripeClient.Subscriptions.Add(new Subscription { Id = "sub_active" });
        stripeClient.Subscriptions.Add(new Subscription { Id = "sub_canceled", CanceledAt = TimeProvider.GetUtcNow().UtcDateTime });
        var service = GetService<OrganizationService>();

        // Act
        await service.CancelSubscriptionsAsync(new Organization
        {
            Id = "000000000000000000000001",
            Name = "Test",
            StripeCustomerId = "cus_test"
        });

        // Assert
        Assert.Equal("cus_test", stripeClient.LastSubscriptionListOptions?.Customer);
        Assert.Equal("sub_active", Assert.Single(stripeClient.CanceledSubscriptions).SubscriptionId);
    }
}
