using Exceptionless.Core;
using Exceptionless.Core.Billing;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Microsoft.Extensions.DependencyInjection;
using Stripe;
using Xunit;
using WebInvoice = Exceptionless.Web.Models.Invoice;

namespace Exceptionless.Tests.Controllers;

public sealed class OrganizationControllerTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;
    private FakeStripeBillingClient StripeBillingClient => (FakeStripeBillingClient)GetService<IStripeBillingClient>();

    public OrganizationControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _userRepository = GetService<IUserRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    protected override void RegisterServices(IServiceCollection services)
    {
        base.RegisterServices(services);
        services.ReplaceSingleton<IStripeBillingClient, FakeStripeBillingClient>();
    }

    private async Task WithBillingEnabledAsync(Func<Task> action)
    {
        var options = GetService<AppOptions>();
        string? originalStripeApiKey = options.StripeOptions.StripeApiKey;

        options.StripeOptions.StripeApiKey = "sk_test_local";
        try
        {
            await action();
        }
        finally
        {
            options.StripeOptions.StripeApiKey = originalStripeApiKey;
        }
    }

    private async Task<T> WithBillingEnabledAsync<T>(Func<Task<T>> action)
    {
        var options = GetService<AppOptions>();
        string? originalStripeApiKey = options.StripeOptions.StripeApiKey;

        options.StripeOptions.StripeApiKey = "sk_test_local";
        try
        {
            return await action();
        }
        finally
        {
            options.StripeOptions.StripeApiKey = originalStripeApiKey;
        }
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        StripeBillingClient.Reset();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    private async Task SetStripeCustomerIdAsync(string organizationId, string stripeCustomerId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        Assert.NotNull(organization);

        organization.StripeCustomerId = stripeCustomerId;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());
    }

    private async Task SetPlanAndStripeCustomerIdAsync(string organizationId, string planId, string stripeCustomerId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);
        Assert.NotNull(organization);

        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(user);

        var plan = _billingManager.GetBillingPlan(planId);
        Assert.NotNull(plan);

        _billingManager.ApplyBillingPlan(organization, plan, user);
        organization.StripeCustomerId = stripeCustomerId;
        organization.CardLast4 = "4242";
        organization.SubscribeDate = TimeProvider.GetUtcNow().UtcDateTime;
        organization.BillingStatus = BillingStatus.Active;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache().Originals());
    }

    private Stripe.Invoice CreateStripeInvoice(string id, string customerId, DateTime createdUtc)
    {
        var periodEndUtc = createdUtc.AddMonths(1);
        return new Stripe.Invoice
        {
            Id = id,
            CustomerId = customerId,
            Created = createdUtc,
            Status = "paid",
            Total = 1500,
            Subtotal = 1500,
            PeriodStart = createdUtc,
            PeriodEnd = periodEndUtc,
            Lines = new StripeList<Stripe.InvoiceLineItem>
            {
                Data =
                [
                    new Stripe.InvoiceLineItem
                    {
                        Amount = 1500,
                        Description = "Small plan",
                        Period = new InvoiceLineItemPeriod { Start = createdUtc, End = periodEndUtc },
                        Pricing = new InvoiceLineItemPricing
                        {
                            PriceDetails = new InvoiceLineItemPricingPriceDetails { PriceId = _plans.SmallPlan.Id }
                        }
                    }
                ]
            }
        };
    }

    private static Subscription CreateStripeSubscription(string id, string itemId, DateTime? canceledAtUtc = null)
        => new()
        {
            Id = id,
            CanceledAt = canceledAtUtc,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Id = itemId }]
            }
        };

    [Fact]
    public async Task PostAsync_NewOrganization_MapsToOrganizationAndCreates()
    {
        // Arrange
        var newOrg = new NewOrganization
        {
            Name = "Test Organization"
        };

        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("organizations")
            .Content(newOrg)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(viewOrg);
        Assert.NotNull(viewOrg.Id);
        Assert.Equal("Test Organization", viewOrg.Name);
        Assert.True(viewOrg.CreatedUtc > DateTime.MinValue);

        var organization = await _organizationRepository.GetByIdAsync(viewOrg.Id);
        Assert.NotNull(organization);
        Assert.Equal("Test Organization", organization.Name);
    }

    [Fact]
    public async Task GetAsync_ExistingOrganization_MapsToViewOrganization()
    {
        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewOrg);
        Assert.Equal(SampleDataService.TEST_ORG_ID, viewOrg.Id);
        Assert.False(String.IsNullOrEmpty(viewOrg.Name));
        Assert.NotNull(viewOrg.PlanId);
        Assert.NotNull(viewOrg.PlanName);
    }

    [Fact]
    public async Task GetAsync_WithStatsMode_ReturnsPopulatedViewOrganization()
    {
        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewOrg);
        Assert.Equal(SampleDataService.TEST_ORG_ID, viewOrg.Id);
        Assert.NotNull(viewOrg.Usage);
        Assert.NotNull(viewOrg.UsageHours);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsViewOrganizationCollection()
    {
        // Act
        var viewOrgs = await SendRequestAsAsync<List<ViewOrganization>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("organizations")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewOrgs);
        Assert.True(viewOrgs.Count > 0);
        Assert.All(viewOrgs, vo =>
        {
            Assert.NotNull(vo.Id);
            Assert.NotNull(vo.Name);
            Assert.NotNull(vo.PlanId);
        });
    }

    [Fact]
    public async Task GetAllAsync_WithFilter_ReturnsMatchingViewOrganizations()
    {
        // Act
        var viewOrgs = await SendRequestAsAsync<List<ViewOrganization>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("organizations")
            .QueryString("filter", "Acme")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewOrgs);
        var viewOrg = Assert.Single(viewOrgs);
        Assert.Equal("Acme", viewOrg.Name);
    }

    [Fact]
    public async Task GetAllAsync_WithFilter_ReturnsOnlyAssociatedOrganizations()
    {
        // Act
        var viewOrgs = await SendRequestAsAsync<List<ViewOrganization>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("organizations")
            .QueryString("filter", "Free")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewOrgs);
        Assert.Empty(viewOrgs);
    }

    [Fact]
    public async Task PostAsync_NewOrganization_AssignsDefaultPlan()
    {
        // Arrange
        var newOrg = new NewOrganization
        {
            Name = "Organization With Default Plan"
        };

        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("organizations")
            .Content(newOrg)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(viewOrg);
        Assert.NotNull(viewOrg.PlanId);
        Assert.NotNull(viewOrg.PlanName);
    }

    [Fact]
    public async Task GetAsync_ViewOrganization_IncludesIsOverMonthlyLimit()
    {
        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(viewOrg);
        Assert.False(viewOrg.IsOverMonthlyLimit);
    }

    [Fact]
    public async Task PostAsync_NewOrganization_SetsCreatedAndUpdatedDates()
    {
        // Arrange
        var newOrg = new NewOrganization
        {
            Name = "Organization With Dates"
        };

        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("organizations")
            .Content(newOrg)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(viewOrg);
        Assert.True(viewOrg.CreatedUtc > DateTime.MinValue);
        Assert.True(viewOrg.UpdatedUtc > DateTime.MinValue);
    }

    [Fact]
    public Task PostAsync_EmptyName_ReturnsValidationError()
    {
        // Arrange
        var newOrg = new NewOrganization
        {
            Name = String.Empty
        };

        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("organizations")
            .Content(newOrg)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task GetAsync_NonExistentOrganization_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", "nonexistent-org-id")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task SetFeatureAsync_AsGlobalAdmin_EnablesFeature()
    {
        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "feature-saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert - feature is stored on the organization
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        Assert.Contains("feature-saved-views", organization.Features);
    }

    [Fact]
    public Task SetFeatureAsync_AsRegularUser_ReturnsForbidden()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "feature-saved-views")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public Task SetFeatureAsync_NonExistentOrganization_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", "000000000000000000000001", "features", "feature-saved-views")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task RemoveFeatureAsync_AsGlobalAdmin_DisablesFeature()
    {
        // Arrange - enable the feature first
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "feature-saved-views")
            .StatusCodeShouldBeOk()
        );

        var afterEnable = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(afterEnable);
        Assert.Contains("feature-saved-views", afterEnable.Features);

        // Act - disable the feature
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "feature-saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert - feature is removed
        var afterRemove = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(afterRemove);
        Assert.DoesNotContain("feature-saved-views", afterRemove.Features);
    }

    [Fact]
    public Task RemoveFeatureAsync_AsRegularUser_ReturnsForbidden()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "feature-saved-views")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public async Task SetFeatureAsync_IsCaseInsensitive()
    {
        // Act - enable with different casing (controller normalizes to lowercase)
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "Feature-Saved-Views")
            .StatusCodeShouldBeOk()
        );

        // Assert - stored normalized to lowercase
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        Assert.Contains("feature-saved-views", organization.Features);
        Assert.DoesNotContain("Feature-Saved-Views", organization.Features);
    }

    [Fact]
    public async Task GetAsync_ViewOrganization_IncludesFeaturesCollection()
    {
        // Arrange - enable a feature
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "feature-saved-views")
            .StatusCodeShouldBeOk()
        );

        // Act
        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .StatusCodeShouldBeOk()
        );

        // Assert - Features is included in the ViewOrganization DTO
        Assert.NotNull(viewOrg);
        Assert.NotNull(viewOrg.Features);
        Assert.Contains("feature-saved-views", viewOrg.Features);
    }

    [Fact]
    public Task SetFeatureAsync_WhitespaceOnly_ReturnsBadRequest()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "  ")
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public Task RemoveFeatureAsync_WhitespaceOnly_ReturnsBadRequest()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "features", "  ")
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task DeleteAsync_ExistingOrganization_RemovesOrganization()
    {
        // Arrange
        var newOrg = new NewOrganization
        {
            Name = "Organization To Delete"
        };

        var viewOrg = await SendRequestAsAsync<ViewOrganization>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("organizations")
            .Content(newOrg)
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(viewOrg);

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("organizations", viewOrg.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Assert
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", viewOrg.Id)
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task AddUserAsync_NewEmail_AddsInvite()
    {
        // Arrange
        const string emailAddress = "New.Member+Invite@localhost";

        // Act
        var user = await SendRequestAsAsync<User>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", emailAddress)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(emailAddress, user.EmailAddress);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);

        var invite = Assert.Single(organization.Invites, i => String.Equals(i.EmailAddress, emailAddress.ToLowerInvariant(), StringComparison.Ordinal));
        Assert.False(String.IsNullOrEmpty(invite.Token));
        Assert.True(invite.DateAdded > DateTime.MinValue);
    }

    [Fact]
    public async Task AddUserAsync_ExistingInvite_DoesNotDuplicateInvite()
    {
        // Arrange
        const string emailAddress = "pending.member@localhost";
        const string token = "existing-invite-token";
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Invites.Add(new Invite
        {
            Token = token,
            EmailAddress = emailAddress,
            DateAdded = TimeProvider.GetUtcNow().UtcDateTime
        });
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        // Act
        var user = await SendRequestAsAsync<User>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", emailAddress.ToUpperInvariant())
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(emailAddress.ToUpperInvariant(), user.EmailAddress);

        organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);

        var invite = Assert.Single(organization.Invites, i => String.Equals(i.EmailAddress, emailAddress, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(token, invite.Token);
    }

    [Fact]
    public async Task AddUserAsync_ExistingUserWithoutMembership_AddsOrganizationMembership()
    {
        // Arrange
        var existingUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(existingUser);
        Assert.DoesNotContain(SampleDataService.TEST_ORG_ID, existingUser.OrganizationIds);

        // Act
        var user = await SendRequestAsAsync<User>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", SampleDataService.FREE_USER_EMAIL)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(SampleDataService.FREE_USER_EMAIL, user.EmailAddress);

        existingUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(existingUser);
        Assert.Contains(SampleDataService.TEST_ORG_ID, existingUser.OrganizationIds);
        Assert.Contains(SampleDataService.FREE_ORG_ID, existingUser.OrganizationIds);
    }

    [Fact]
    public async Task AddUserAsync_ExistingUserWithMembership_DoesNotDuplicateOrganizationMembership()
    {
        // Act
        var user = await SendRequestAsAsync<User>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", SampleDataService.TEST_ORG_USER_EMAIL)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(user);
        Assert.Equal(SampleDataService.TEST_ORG_USER_EMAIL, user.EmailAddress);

        var existingUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(existingUser);
        Assert.Single(existingUser.OrganizationIds, id => String.Equals(id, SampleDataService.TEST_ORG_ID, StringComparison.Ordinal));
    }

    [Fact]
    public Task AddUserAsync_UnauthorizedOrganization_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", "new.member@localhost")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task AddUserAsync_NonExistentOrganization_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Post()
            .AppendPaths("organizations", "000000000000000000000000", "users", "new.member@localhost")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task AddUserAsync_PlanLimitReached_ReturnsUpgradeRequired()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "users", "new.member@localhost")
            .StatusCodeShouldBeUpgradeRequired()
        );
    }

    [Fact]
    public async Task RemoveUserAsync_UserWithNotificationSettings_CleansUpNotificationSettings()
    {
        // Arrange
        var organizationAdminUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(organizationAdminUser);
        Assert.Contains(SampleDataService.TEST_ORG_ID, organizationAdminUser.OrganizationIds);

        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.NotificationSettings[organizationAdminUser.Id] = new NotificationSettings
        {
            SendDailySummary = true,
            ReportNewErrors = true,
            ReportCriticalErrors = true
        };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.True(project.NotificationSettings.ContainsKey(organizationAdminUser.Id));

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", SampleDataService.TEST_ORG_USER_EMAIL)
            .StatusCodeShouldBeOk()
        );

        await RefreshDataAsync();

        // Assert
        project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.False(project.NotificationSettings.ContainsKey(organizationAdminUser.Id));

        organizationAdminUser = await _userRepository.GetByIdAsync(organizationAdminUser.Id);
        Assert.NotNull(organizationAdminUser);
        Assert.DoesNotContain(SampleDataService.TEST_ORG_ID, organizationAdminUser.OrganizationIds);
    }

    [Fact]
    public async Task RemoveUserAsync_WithExistingOrphanedNotificationSettings_CleansTargetAndHistoricalOrphans()
    {
        // Arrange
        var organizationAdminUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(organizationAdminUser);

        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        string orphanedUserId = ObjectId.GenerateNewId().ToString();
        project.NotificationSettings[organizationAdminUser.Id] = new NotificationSettings
        {
            SendDailySummary = true,
            ReportNewErrors = true,
            ReportCriticalErrors = true
        };
        project.NotificationSettings[orphanedUserId] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        Assert.Null(await _userRepository.GetByIdAsync(orphanedUserId));

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", SampleDataService.TEST_ORG_USER_EMAIL)
            .StatusCodeShouldBeOk()
        );

        await RefreshDataAsync();

        project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);

        // Assert
        Assert.DoesNotContain(organizationAdminUser.Id, project.NotificationSettings.Keys);
        Assert.DoesNotContain(orphanedUserId, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task RemoveUserAsync_UserWithNotificationSettings_PreservesOtherUsersAndIntegrations()
    {
        // Arrange
        var organizationAdminUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(organizationAdminUser);

        var globalAdmin = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(globalAdmin);

        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.NotificationSettings[organizationAdminUser.Id] = new NotificationSettings { ReportNewErrors = true };
        project.NotificationSettings[globalAdmin.Id] = new NotificationSettings { ReportCriticalErrors = true };
        project.NotificationSettings[Project.NotificationIntegrations.Slack] = new NotificationSettings { SendDailySummary = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Delete()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", SampleDataService.TEST_ORG_USER_EMAIL)
            .StatusCodeShouldBeOk()
        );

        await RefreshDataAsync();

        // Assert
        project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.DoesNotContain(organizationAdminUser.Id, project.NotificationSettings.Keys);
        Assert.Contains(globalAdmin.Id, project.NotificationSettings.Keys);
        Assert.Contains(Project.NotificationIntegrations.Slack, project.NotificationSettings.Keys);
    }

    [Fact]
    public async Task GetPlansAsync_UnlimitedPlanOrg_ReturnsPlansWithCurrentPlanOverlay()
    {
        // Act
        var plans = await SendRequestAsAsync<List<BillingPlan>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "plans")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(plans);
        Assert.True(plans.Count > 0);
        var unlimitedPlan = plans.SingleOrDefault(p => String.Equals(p.Id, _plans.UnlimitedPlan.Id, StringComparison.Ordinal));
        Assert.NotNull(unlimitedPlan);
        Assert.False(unlimitedPlan.IsHidden);
    }

    [Fact]
    public async Task GetPlansAsync_FreePlanOrg_ExcludesHiddenPlans()
    {
        // Act
        var plans = await SendRequestAsAsync<List<BillingPlan>>(r => r
            .AsFreeOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "plans")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(plans);
        Assert.True(plans.Count > 0);

        Assert.DoesNotContain(plans, p => p.IsHidden);
        var freePlan = plans.SingleOrDefault(p => String.Equals(p.Id, _plans.FreePlan.Id, StringComparison.Ordinal));
        Assert.NotNull(freePlan);
        Assert.Equal(_plans.FreePlan.Name, freePlan.Name);
    }

    [Fact]
    public async Task GetPlansAsync_AdminUser_ReturnsAllPlansIncludingHidden()
    {
        // Act
        var plans = await SendRequestAsAsync<List<BillingPlan>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "plans")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(plans);
        Assert.Equal(_plans.Plans.Count, plans.Count);
    }

    [Fact]
    public async Task GetPlansAsync_CurrentPlanOverlay_ReflectsOrgValues()
    {
        // Arrange
        var org = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(org);

        // Act
        var plans = await SendRequestAsAsync<List<BillingPlan>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "plans")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(plans);
        var currentPlan = plans.SingleOrDefault(p => String.Equals(p.Id, org.PlanId, StringComparison.Ordinal));
        Assert.NotNull(currentPlan);
        Assert.Equal(org.PlanName, currentPlan.Name);
        Assert.Equal(org.BillingPrice, currentPlan.Price);
        Assert.Equal(org.MaxProjects, currentPlan.MaxProjects);
        Assert.Equal(org.MaxUsers, currentPlan.MaxUsers);
        Assert.Equal(org.RetentionDays, currentPlan.RetentionDays);
        Assert.Equal(org.MaxEventsPerMonth, currentPlan.MaxEventsPerMonth);
        Assert.Equal(org.HasPremiumFeatures, currentPlan.HasPremiumFeatures);
    }

    [Fact]
    public Task GetPlansAsync_NonExistentOrg_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", "000000000000000000000000", "plans")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task ChangePlanAsync_BillingDisabled_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "change-plan")
            .Content(new ChangePlanRequest { PlanId = _plans.FreePlan.Id })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task ChangePlanAsync_LegacyQueryParams_BillingDisabled_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "change-plan")
            .QueryString("planId", _plans.FreePlan.Id)
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task ChangePlanAsync_NonExistentOrg_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", "000000000000000000000000", "change-plan")
            .Content(new ChangePlanRequest { PlanId = _plans.FreePlan.Id })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task ChangePlanAsync_UnauthorizedOrg_ReturnsNotFound()
    {
        // Act & Assert — free user should not be able to change plan for the test org they don't belong to
        return SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "change-plan")
            .Content(new ChangePlanRequest { PlanId = _plans.FreePlan.Id })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task ChangePlanAsync_EmptyBody_BillingDisabled_ReturnsNotFound()
    {
        // Act & Assert — empty body falls back to query params; billing disabled returns 404
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "change-plan")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task ChangePlanAsync_InvalidPlan_ReturnsValidationError()
    {
        return WithBillingEnabledAsync(() =>
            SendRequestAsync(r => r
                .AsTestOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest { PlanId = "missing-plan" })
                .StatusCodeShouldBeUnprocessableEntity()
            ));
    }

    [Fact]
    public async Task ChangePlanAsync_SameFreePlan_ReturnsSuccess()
    {
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest { PlanId = _plans.FreePlan.Id })
                .StatusCodeShouldBeOk()
            ));

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Contains("already on the free plan", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangePlanAsync_LegacyQueryParamsSameFreePlan_ReturnsSuccess()
    {
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .QueryString("planId", _plans.FreePlan.Id)
                .StatusCodeShouldBeOk()
            ));

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Contains("already on the free plan", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangePlanAsync_PaidPlanWithoutBillingInformation_ReturnsFailure()
    {
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest { PlanId = _plans.SmallPlan.Id })
                .StatusCodeShouldBeOk()
            ));

        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("Billing information was not set.", result.Message);
    }

    [Fact]
    public async Task ChangePlanAsync_NewCustomerCreatesStripeCustomerAndSubscription()
    {
        // Arrange
        StripeBillingClient.CustomerToReturn = new Customer { Id = "cus_created" };

        // Act
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest
                {
                    PlanId = _plans.SmallPlan.Id,
                    StripeToken = "tok_visa",
                    Last4 = "4242",
                    CouponId = "coupon_10"
                })
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Message);
        Assert.NotNull(StripeBillingClient.LastCustomerCreateOptions);
        Assert.Equal("tok_visa", StripeBillingClient.LastCustomerCreateOptions.Source);
        Assert.Equal(SampleDataService.FREE_USER_EMAIL, StripeBillingClient.LastCustomerCreateOptions.Email);

        var subscription = Assert.Single(StripeBillingClient.CreatedSubscriptionOptions);
        Assert.Equal("cus_created", subscription.Customer);
        var item = Assert.Single(subscription.Items);
        Assert.Equal(_plans.SmallPlan.Id, item.Price);
        Assert.Equal("coupon_10", Assert.Single(subscription.Discounts).Coupon);
        Assert.Equal("create_prorations", subscription.ProrationBehavior);
        Assert.NotNull(subscription.BillingCycleAnchorConfig);
        Assert.Equal(1, subscription.BillingCycleAnchorConfig.DayOfMonth);
        Assert.Equal(0, subscription.BillingCycleAnchorConfig.Hour);
        Assert.Equal(0, subscription.BillingCycleAnchorConfig.Minute);
        Assert.Equal(0, subscription.BillingCycleAnchorConfig.Second);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        Assert.Equal("cus_created", organization.StripeCustomerId);
        Assert.Equal("4242", organization.CardLast4);
        Assert.Equal(_plans.SmallPlan.Id, organization.PlanId);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
    }

    [Fact]
    public async Task ChangePlanAsync_ExistingCustomerUpdatesPaymentMethodAndSubscription()
    {
        // Arrange
        var subscribeDate = new DateTime(2026, 5, 22, 14, 30, 0, DateTimeKind.Utc);
        TimeProvider.SetUtcNow(subscribeDate);
        await SetStripeCustomerIdAsync(SampleDataService.FREE_ORG_ID, "cus_existing");
        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_active", "si_active"));

        // Act
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest
                {
                    PlanId = _plans.SmallPlan.Id,
                    StripeToken = "pm_card_visa",
                    Last4 = "4242",
                    CouponId = "coupon_10"
                })
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, result.Message);

        var attachedPaymentMethod = Assert.Single(StripeBillingClient.AttachedPaymentMethods);
        Assert.Equal("pm_card_visa", attachedPaymentMethod.PaymentMethodId);
        Assert.Equal("cus_existing", attachedPaymentMethod.Options.Customer);

        var updatedCustomer = Assert.Single(StripeBillingClient.UpdatedCustomers);
        Assert.Equal("cus_existing", updatedCustomer.CustomerId);
        Assert.Equal(SampleDataService.FREE_USER_EMAIL, updatedCustomer.Options.Email);
        Assert.Equal("pm_card_visa", updatedCustomer.Options.InvoiceSettings?.DefaultPaymentMethod);

        var updatedSubscription = Assert.Single(StripeBillingClient.UpdatedSubscriptions);
        Assert.Equal("sub_active", updatedSubscription.SubscriptionId);
        var item = Assert.Single(updatedSubscription.Options.Items);
        Assert.Equal("si_active", item.Id);
        Assert.Equal(_plans.SmallPlan.Id, item.Price);
        Assert.Equal("coupon_10", Assert.Single(updatedSubscription.Options.Discounts).Coupon);
        Assert.Equal("create_prorations", updatedSubscription.Options.ProrationBehavior);
        Assert.Empty(StripeBillingClient.CreatedSubscriptionOptions);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        Assert.Equal("4242", organization.CardLast4);
        Assert.Equal(subscribeDate, organization.SubscribeDate);
        Assert.Equal(_plans.SmallPlan.Id, organization.PlanId);
        Assert.Equal(BillingStatus.Active, organization.BillingStatus);
    }

    [Fact]
    public async Task ChangePlanAsync_FreePlanCancelsActiveStripeSubscriptions()
    {
        // Arrange
        var canceledAtUtc = TimeProvider.GetUtcNow().UtcDateTime;
        await SetPlanAndStripeCustomerIdAsync(SampleDataService.FREE_ORG_ID, _plans.SmallPlan.Id, "cus_existing");
        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_active", "si_active"));
        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_canceled", "si_canceled", canceledAtUtc: canceledAtUtc));

        // Act
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest { PlanId = _plans.FreePlan.Id })
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("cus_existing", StripeBillingClient.LastSubscriptionListOptions?.Customer);
        Assert.Equal("sub_active", Assert.Single(StripeBillingClient.CanceledSubscriptions).SubscriptionId);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        Assert.Equal(_plans.FreePlan.Id, organization.PlanId);
        Assert.Equal(BillingStatus.Trialing, organization.BillingStatus);
    }

    [Fact]
    public async Task ChangePlanAsync_StripeBillingClientThrows_ReturnsFailure()
    {
        // Arrange
        StripeBillingClient.CreateCustomerException = new InvalidOperationException("Stripe unavailable");

        // Act
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest
                {
                    PlanId = _plans.SmallPlan.Id,
                    StripeToken = "tok_visa",
                    Last4 = "4242"
                })
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("An error occurred while changing plans. Please try again.", result.Message);
        Assert.Empty(StripeBillingClient.CreatedSubscriptionOptions);
    }

    [Fact]
    public async Task ChangePlanAsync_NewCustomerSubscriptionFails_PreservesStripeCustomerForRetry()
    {
        // Arrange
        StripeBillingClient.CustomerToReturn = new Customer { Id = "cus_created" };
        StripeBillingClient.CreateSubscriptionException = new InvalidOperationException("Stripe unavailable");

        // Act
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest
                {
                    PlanId = _plans.SmallPlan.Id,
                    StripeToken = "tok_visa",
                    Last4 = "4242"
                })
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("An error occurred while changing plans. Please try again.", result.Message);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        Assert.Equal("cus_created", organization.StripeCustomerId);
        Assert.Equal("4242", organization.CardLast4);
        Assert.Equal(_plans.FreePlan.Id, organization.PlanId);
        Assert.Equal(BillingStatus.Trialing, organization.BillingStatus);
    }

    [Fact]
    public async Task ChangePlanAsync_ExistingCustomerSubscriptionFails_DoesNotPersistPlanOrCardChange()
    {
        // Arrange
        await SetStripeCustomerIdAsync(SampleDataService.FREE_ORG_ID, "cus_existing");
        StripeBillingClient.Subscriptions.Add(CreateStripeSubscription("sub_active", "si_active"));
        StripeBillingClient.UpdateSubscriptionException = new InvalidOperationException("Stripe unavailable");

        // Act
        var result = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<ChangePlanResult>(r => r
                .AsFreeOrganizationUser()
                .Post()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "change-plan")
                .Content(new ChangePlanRequest
                {
                    PlanId = _plans.SmallPlan.Id,
                    StripeToken = "pm_card_visa",
                    Last4 = "4242"
                })
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Equal("An error occurred while changing plans. Please try again.", result.Message);
        Assert.NotEmpty(StripeBillingClient.UpdatedSubscriptions);

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(organization);
        Assert.Equal("cus_existing", organization.StripeCustomerId);
        Assert.Null(organization.CardLast4);
        Assert.Equal(_plans.FreePlan.Id, organization.PlanId);
        Assert.Equal(BillingStatus.Trialing, organization.BillingStatus);
    }

    [Fact]
    public async Task CanDownGradeAsync_TooManyUsers_ReturnsFailure()
    {
        // Arrange — test org has 2 users (global admin + org user); free plan allows max 1
        var org = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(org);

        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(user);

        // Act
        var result = await _billingManager.CanDownGradeAsync(org, _plans.FreePlan, user);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("remove", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanDownGradeAsync_TooManyProjects_ReturnsFailure()
    {
        // Arrange — free org has 1 user and 1 project; add a second project so project check fails
        var org = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(org);

        var extraProject = new Project
        {
            Name = "Extra Project",
            OrganizationId = org.Id,
            NextSummaryEndOfDayTicks = TimeProvider.GetUtcNow().UtcDateTime.Date.AddDays(1).AddHours(1).Ticks
        };
        await _projectRepository.AddAsync(extraProject, o => o.ImmediateConsistency());

        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(user);

        // Act
        var result = await _billingManager.CanDownGradeAsync(org, _plans.FreePlan, user);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("remove", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("project", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanDownGradeAsync_AlreadyHasFreePlan_ReturnsFailure()
    {
        // Arrange — create a second org for the free user, so they already have 1 free org
        var freeUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(freeUser);

        var secondOrg = new Organization { Name = "Second Org" };
        _billingManager.ApplyBillingPlan(secondOrg, _plans.Plans.First(p => p.Id == "EX_SMALL"), freeUser);
        secondOrg.StripeCustomerId = "cus_test";
        secondOrg.CardLast4 = "4242";
        secondOrg.SubscribeDate = TimeProvider.GetUtcNow().UtcDateTime;
        secondOrg = await _organizationRepository.AddAsync(secondOrg, o => o.ImmediateConsistency());

        freeUser.OrganizationIds.Add(secondOrg.Id);
        await _userRepository.SaveAsync(freeUser, o => o.ImmediateConsistency());

        // Act — try to downgrade second org to free plan (user already has FREE_ORG on free plan)
        var result = await _billingManager.CanDownGradeAsync(secondOrg, _plans.FreePlan, freeUser);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("free account", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanDownGradeAsync_ValidDowngrade_ReturnsSuccess()
    {
        // Arrange — the free org (1 user, 1 project) should be able to "downgrade" to small plan
        var org = await _organizationRepository.GetByIdAsync(SampleDataService.FREE_ORG_ID);
        Assert.NotNull(org);

        var smallPlan = _plans.Plans.FirstOrDefault(p => p.Id == "EX_SMALL");
        Assert.NotNull(smallPlan);

        var user = await _userRepository.GetByEmailAddressAsync(SampleDataService.FREE_USER_EMAIL);
        Assert.NotNull(user);

        // Act — "upgrading" from free to small, downgrade check should succeed
        var result = await _billingManager.CanDownGradeAsync(org, smallPlan, user);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public Task GetInvoiceAsync_BillingDisabled_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", "invoice", "in_test_invoice_id")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetInvoiceAsync_StripeInvoice_ReturnsMappedInvoice()
    {
        // Arrange
        await SetStripeCustomerIdAsync(SampleDataService.TEST_ORG_ID, "cus_test");
        var createdUtc = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        StripeBillingClient.Invoice = CreateStripeInvoice("in_abc1234567", "cus_test", createdUtc);

        // Act
        var invoice = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<WebInvoice>(r => r
                .AsTestOrganizationUser()
                .AppendPaths("organizations", "invoice", "abc1234567")
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(invoice);
        Assert.Equal("in_abc1234567", StripeBillingClient.LastGetInvoiceId);
        Assert.Equal("abc1234567", invoice.Id);
        Assert.Equal(SampleDataService.TEST_ORG_ID, invoice.OrganizationId);
        Assert.Equal(createdUtc, invoice.Date);
        Assert.True(invoice.Paid);
        Assert.Equal(15.00m, invoice.Total);
        var item = Assert.Single(invoice.Items);
        Assert.Equal(15.00m, item.Amount);
        Assert.Contains("Small Plan", item.Description, StringComparison.Ordinal);
    }

    [Fact]
    public Task GetInvoiceAsync_StripeBillingClientThrows_ReturnsNotFound()
    {
        // Arrange
        StripeBillingClient.GetInvoiceException = new InvalidOperationException("Stripe unavailable");

        // Act & Assert
        return WithBillingEnabledAsync(() =>
            SendRequestAsync(r => r
                .AsTestOrganizationUser()
                .AppendPaths("organizations", "invoice", "abc1234567")
                .StatusCodeShouldBeNotFound()
            ));
    }

    [Fact]
    public Task GetInvoicesAsync_BillingDisabled_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "invoices")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetInvoicesAsync_StripeCustomer_ReturnsMappedInvoicesAndPagination()
    {
        // Arrange
        await SetStripeCustomerIdAsync(SampleDataService.TEST_ORG_ID, "cus_test");
        StripeBillingClient.Invoices.AddRange(
        [
            CreateStripeInvoice("in_first", "cus_test", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateStripeInvoice("in_second", "cus_test", new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc)),
            CreateStripeInvoice("in_third", "cus_test", new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc))
        ]);

        // Act
        var invoices = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<List<InvoiceGridModel>>(r => r
                .AsTestOrganizationUser()
                .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "invoices")
                .QueryString("before", "before_cursor")
                .QueryString("after", "in_after_cursor")
                .QueryString("limit", 2)
                .StatusCodeShouldBeOk()
            ));

        // Assert
        Assert.NotNull(invoices);
        Assert.Equal(2, invoices.Count);
        Assert.Equal("first", invoices[0].Id);
        Assert.Equal("second", invoices[1].Id);
        Assert.NotNull(StripeBillingClient.LastInvoiceListOptions);
        Assert.Equal("cus_test", StripeBillingClient.LastInvoiceListOptions.Customer);
        Assert.Equal(3, StripeBillingClient.LastInvoiceListOptions.Limit);
        Assert.Equal("in_before_cursor", StripeBillingClient.LastInvoiceListOptions.EndingBefore);
        Assert.Equal("in_after_cursor", StripeBillingClient.LastInvoiceListOptions.StartingAfter);
    }

    [Fact]
    public async Task GetInvoicesAsync_BillingEnabledOrganizationWithoutStripeCustomer_ReturnsEmptyCollection()
    {
        var invoices = await WithBillingEnabledAsync(() =>
            SendRequestAsAsync<List<InvoiceGridModel>>(r => r
                .AsFreeOrganizationUser()
                .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "invoices")
                .StatusCodeShouldBeOk()
            ));

        Assert.NotNull(invoices);
        Assert.Empty(invoices);
    }

    [Fact]
    public Task GetInvoicesAsync_BillingEnabledNonExistentOrganization_ReturnsNotFound()
    {
        return WithBillingEnabledAsync(() =>
            SendRequestAsync(r => r
                .AsGlobalAdminUser()
                .AppendPaths("organizations", "000000000000000000000000", "invoices")
                .StatusCodeShouldBeNotFound()
            ));
    }

    [Fact]
    public Task PatchAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Patch()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .Content(new NewOrganization { Name = "Unauthorized Update" })
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task PatchAsync_EmptyName_ReturnsValidationError()
    {
        // Arrange
        var originalOrg = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(originalOrg);
        string originalName = originalOrg.Name;

        // Act
        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .Content(new NewOrganization { Name = "" })
            .StatusCodeShouldBeBadRequest()
        );

        // Assert - verify data unchanged
        var unchangedOrg = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(unchangedOrg);
        Assert.Equal(originalName, unchangedOrg.Name);
    }

    [Fact]
    public Task PatchAsync_NonExistentOrganization_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", "000000000000000000000000")
            .Content(new NewOrganization { Name = "Nope" })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PatchAsync_UpdateName_ReturnsUpdatedOrganization()
    {
        // Arrange
        var originalOrg = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(originalOrg);

        // Act
        var updated = await SendRequestAsAsync<ViewOrganization>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .Content(new NewOrganization { Name = "Updated Acme" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(SampleDataService.TEST_ORG_ID, updated.Id);
        Assert.Equal("Updated Acme", updated.Name);
        Assert.True(updated.UpdatedUtc >= originalOrg.UpdatedUtc);

        var persisted = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(persisted);
        Assert.Equal("Updated Acme", persisted.Name);
    }

    [Fact]
    public async Task PatchAsync_EmptyJsonBody_ReturnsOriginalOrganizationUnchanged()
    {
        // Arrange
        var originalOrganization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(originalOrganization);

        // Act
        var updated = await SendRequestAsAsync<ViewOrganization>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID)
            .Content("{}", "application/json")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(originalOrganization.Name, updated.Name);
        Assert.Equal(originalOrganization.UpdatedUtc, updated.UpdatedUtc);
    }
}
