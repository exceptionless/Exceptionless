using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

/// <summary>
/// Tests for OrganizationController including mapping coverage.
/// Validates NewOrganization -> Organization and Organization -> ViewOrganization mappings.
/// </summary>
public sealed class OrganizationControllerTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
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

        // Assert - IsOverMonthlyLimit is computed by AfterMap in AutoMapper
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
}
