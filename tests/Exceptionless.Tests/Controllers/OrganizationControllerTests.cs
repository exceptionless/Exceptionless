using Exceptionless.Core.Billing;
using Exceptionless.Core.Models;
using Exceptionless.Core.Models.Billing;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class OrganizationControllerTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;
    private readonly BillingManager _billingManager;
    private readonly BillingPlans _plans;

    public OrganizationControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _userRepository = GetService<IUserRepository>();
        _billingManager = GetService<BillingManager>();
        _plans = GetService<BillingPlans>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

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
        Assert.IsType<bool>(viewOrg.IsOverMonthlyLimit);
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
            NextSummaryEndOfDayTicks = DateTime.UtcNow.Date.AddDays(1).AddHours(1).Ticks
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
        secondOrg.SubscribeDate = DateTime.UtcNow;
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
    public Task GetInvoicesAsync_BillingDisabled_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "invoices")
            .StatusCodeShouldBeNotFound()
        );
    }
}
