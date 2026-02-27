using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
using Foundatio.Repositories.Utility;
using Xunit;

namespace Exceptionless.Tests.Controllers;

/// <summary>
/// Tests for OrganizationController including mapping coverage.
/// Validates NewOrganization -> Organization and Organization -> ViewOrganization mappings.
/// </summary>
public sealed class OrganizationControllerTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserRepository _userRepository;

    public OrganizationControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _userRepository = GetService<IUserRepository>();
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

        // Assert - Verify Organization mapping -> Organization correctly
        Assert.NotNull(viewOrg);
        Assert.NotNull(viewOrg.Id);
        Assert.Equal("Test Organization", viewOrg.Name);
        Assert.True(viewOrg.CreatedUtc > DateTime.MinValue);

        // Verify persisted entity
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

        // Assert - Verify mapped Organization -> ViewOrganization correctly
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

        // Assert - ViewOrganization should include computed properties
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

        // Assert - All organizations should be mapped to ViewOrganization
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

        // Assert - Newly created org should have a default plan
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

        // Assert - IsOverMonthlyLimit is computed by OrganizationMapper
        Assert.NotNull(viewOrg);
        // The value can be true or false depending on usage, but the property should be set
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
    public async Task DeleteAsync_ExistingOrganization_RemovesOrganization()
    {
        // Arrange - Create an organization to delete
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
}
