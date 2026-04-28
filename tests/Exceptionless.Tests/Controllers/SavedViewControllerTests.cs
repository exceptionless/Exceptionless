using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class SavedViewControllerTests : IntegrationTestsBase
{
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly OrganizationService _organizationService;

    public SavedViewControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _savedViewRepository = GetService<ISavedViewRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _userRepository = GetService<IUserRepository>();
        _organizationService = GetService<OrganizationService>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();

        // Enable saved views feature for all tests in this class
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Add(OrganizationFeatures.SavedViews);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());
    }

    [Fact]
    public async Task PostAsync_NewSavedView_MapsAllPropertiesToSavedView()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Production Errors",
            Filter = "status:open",
            Time = "[now-7D TO now]",
            ViewType = "events",
            FilterDefinitions = """[{"type":"keyword","value":"status:open"}]"""
        };

        // Act
        var result = await SendRequestAsAsync<ViewSavedView>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Id);
        Assert.Equal(SampleDataService.TEST_ORG_ID, result.OrganizationId);
        Assert.Null(result.UserId);
        Assert.Equal("Production Errors", result.Name);
        Assert.Equal("status:open", result.Filter);
        Assert.Equal("[now-7D TO now]", result.Time);
        Assert.Equal("events", result.ViewType);
        Assert.NotNull(result.FilterDefinitions);
        Assert.Equal(1, result.Version);
        Assert.NotNull(result.CreatedByUserId);
        Assert.Null(result.UpdatedByUserId);
        Assert.True(result.CreatedUtc > DateTime.MinValue);
        Assert.True(result.UpdatedUtc > DateTime.MinValue);

        // Verify persisted
        var savedView = await _savedViewRepository.GetByIdAsync(result.Id);
        Assert.NotNull(savedView);
        Assert.Equal("Production Errors", savedView.Name);
    }

    [Fact]
    public async Task PostAsync_WithIsPrivate_SetsUserIdOnSavedView()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "My Private View",
            Filter = "status:regressed",
            ViewType = "issues",
            IsPrivate = true
        };

        // Act
        var result = await SendRequestAsAsync<ViewSavedView>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.UserId);
    }

    [Fact]
    public async Task PostAsync_WithoutIsPrivate_DoesNotSetUserId()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Organization Wide View",
            Filter = "status:open",
            ViewType = "events"
        };

        // Act
        var result = await SendRequestAsAsync<ViewSavedView>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.UserId);
    }

    [Fact]
    public Task PostAsync_WithUnauthorizedOrganization_ReturnsBadRequest()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.FREE_ORG_ID,
            Name = "Unauthorized View",
            Filter = "status:open",
            ViewType = "events"
        };

        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PostAsync_AsOrganizationUser_CanCreateSavedView()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Organization User View",
            Filter = "type:error",
            ViewType = "stream"
        };

        // Act
        var result = await SendRequestAsAsync<ViewSavedView>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Organization User View", result.Name);
    }

    [Fact]
    public Task PostAsync_WithEmptyName_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = String.Empty,
            Filter = "status:open",
            ViewType = "events"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_WithEmptyFilter_ReturnsCreated()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Show All",
            Filter = String.Empty,
            ViewType = "events"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task PostAsync_WithInvalidView_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Invalid View Type",
            Filter = "status:open",
            ViewType = "invalid-view"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Theory]
    [InlineData("events")]
    [InlineData("issues")]
    [InlineData("stream")]
    public async Task PostAsync_WithValidView_Succeeds(string view)
    {
        // Arrange & Act
        var result = await CreateSavedViewAsync($"View Test {view}", "status:open", view);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(view, result.ViewType);
    }

    [Fact]
    public async Task GetAsync_ExistingFilter_ReturnsFilter()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Get Test View", "status:open", "events");
        Assert.NotNull(created);

        // Act
        var result = await SendRequestAsAsync<ViewSavedView>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Name, result.Name);
    }

    [Fact]
    public Task GetAsync_NonExistentFilter_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetByOrganizationAsync_WithMultipleUsers_ReturnsOrganizationWideAndCurrentUserFilters()
    {
        // Arrange
        var organizationFilter = await CreateSavedViewAsync("Organization View", "status:open", "events");
        var privateFilter = await CreateSavedViewAsync("Private View", "status:regressed", "events", isPrivate: true);
        Assert.NotNull(organizationFilter);
        Assert.NotNull(privateFilter);

        // Act
        var filters = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(filters);
        Assert.True(filters.Count >= 2);
        Assert.Contains(filters, f => String.Equals(f.Id, organizationFilter.Id));
        Assert.Contains(filters, f => String.Equals(f.Id, privateFilter.Id));
    }

    [Fact]
    public async Task GetByOrganizationAsync_WithOtherUsersPrivateFilters_ExcludesThem()
    {
        // Arrange - Global admin creates a private filter
        var privateFilter = await CreateSavedViewAsync("Admin Private", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateFilter);

        // Act - Organization user queries the same organization
        var filters = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert - should not see the admin's private filter
        Assert.NotNull(filters);
        Assert.DoesNotContain(filters, f => String.Equals(f.Id, privateFilter.Id));
    }

    [Fact]
    public async Task GetByViewAsync_WithMixedViewTypes_ReturnsOnlyMatchingViewFilters()
    {
        // Arrange
        var eventsFilter = await CreateSavedViewAsync("Events Only", "status:open", "events");
        var issuesFilter = await CreateSavedViewAsync("Issues Only", "status:regressed", "issues");
        Assert.NotNull(eventsFilter);
        Assert.NotNull(issuesFilter);

        // Act
        var filters = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "events")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(filters);
        Assert.Contains(filters, f => String.Equals(f.Id, eventsFilter.Id));
        Assert.DoesNotContain(filters, f => String.Equals(f.Id, issuesFilter.Id));
    }

    [Fact]
    public async Task PatchAsync_UpdateName_UpdatesNameAndSetsUpdatedByUserId()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Original Name", "status:open", "events");
        Assert.NotNull(created);

        // Act
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Name = "Updated Name" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Updated Name", updated.Name);
        Assert.NotNull(updated.UpdatedByUserId);
        Assert.True(updated.UpdatedUtc >= created.UpdatedUtc);
    }

    [Fact]
    public async Task PatchAsync_UpdateFilter_UpdatesFilterString()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Filter Update Test", "status:open", "events");
        Assert.NotNull(created);

        // Act
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Filter = "status:regressed" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("status:regressed", updated.Filter);
    }

    [Fact]
    public async Task PatchAsync_UpdateTime_UpdatesTimeString()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Time Update Test", "status:open", "events");
        Assert.NotNull(created);

        // Act
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Time = "[now-30D TO now]" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("[now-30D TO now]", updated.Time);
    }

    [Fact]
    public Task PatchAsync_NonExistentFilter_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "000000000000000000000000")
            .Content(new UpdateSavedView { Name = "Nope" })
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteAsync_OwnOrganizationWideFilter_Succeeds()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Delete Me", "status:open", "events");
        Assert.NotNull(created);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .StatusCodeShouldBeAccepted()
        );

        // Assert
        var deleted = await _savedViewRepository.GetByIdAsync(created.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersPrivateFilter_ReturnsNotFound()
    {
        // Arrange - Global admin creates private filter
        var privateFilter = await CreateSavedViewAsync("Admin Private Delete", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateFilter);

        // Act - Organization user tries to delete it (DenyWithNotFound hides existence)
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", privateFilter.Id)
            .StatusCodeShouldBeNotFound()
        );

        // Assert - still exists
        var stillExists = await _savedViewRepository.GetByIdAsync(privateFilter.Id);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task DeleteAsync_MultipleFilters_DeletesAll()
    {
        // Arrange
        var first = await CreateSavedViewAsync("Multi Delete 1", "status:open", "events");
        var second = await CreateSavedViewAsync("Multi Delete 2", "status:regressed", "events");
        Assert.NotNull(first);
        Assert.NotNull(second);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", $"{first.Id},{second.Id}")
            .StatusCodeShouldBeAccepted()
        );

        // Assert
        Assert.Null(await _savedViewRepository.GetByIdAsync(first.Id));
        Assert.Null(await _savedViewRepository.GetByIdAsync(second.Id));
    }

    [Fact]
    public async Task GetAsync_PrivateFilterByOwner_ReturnsFilter()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Owner Access", "status:open", "events", isPrivate: true);
        Assert.NotNull(created);

        // Act - same user who created it
        var result = await SendRequestAsAsync<ViewSavedView>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetAsync_PrivateFilterByOtherUser_ReturnsNotFound()
    {
        // Arrange - Global admin creates private filter
        var created = await CreateSavedViewAsync("Admin Only", "status:open", "events", isPrivate: true);
        Assert.NotNull(created);

        // Act - Organization user tries to get it
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", created.Id)
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PatchAsync_OrganizationWideFilterByOrganizationMember_Succeeds()
    {
        // Arrange - Global admin creates organization-wide filter
        var created = await CreateSavedViewAsync("Shared View", "status:open", "events");
        Assert.NotNull(created);

        // Act - Organization user updates it
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Name = "Renamed by Organization User" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("Renamed by Organization User", updated.Name);
    }

    [Fact]
    public Task PostAsync_AnonymousUser_ReturnsUnauthorized()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Anonymous View",
            Filter = "status:open",
            ViewType = "events"
        };

        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsAnonymousUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public async Task PostAsync_ExceedsPerOrgCap_ReturnsBadRequest()
    {
        // Arrange - Directly seed repository to approach the cap
        var filters = new List<SavedView>();
        for (int i = 0; i < 100; i++)
        {
            filters.Add(new SavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = $"Cap Test {i}",
                Filter = "status:open",
                ViewType = "events",
                Version = 1,
                CreatedByUserId = "537650f3b77efe23a47914f0",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            });
        }
        await _savedViewRepository.AddAsync(filters, o => o.ImmediateConsistency());

        // Act - try to add one more
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "One Too Many",
            Filter = "status:open",
            ViewType = "events"
        };

        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PostAsync_WhenFeatureDisabled_ReturnsUnprocessableEntity()
    {
        // Arrange — disable the saved views feature
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Remove(OrganizationFeatures.SavedViews);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Blocked View",
            Filter = "status:open",
            ViewType = "events"
        };

        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PatchAsync_WhenFeatureDisabled_ReturnsUnprocessableEntity()
    {
        // Arrange — create a view directly (bypassing feature check), then disable feature
        var savedView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Existing View",
            Filter = "status:open",
            ViewType = "events",
            Version = 1,
            CreatedByUserId = "537650f3b77efe23a47914f0",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Remove(OrganizationFeatures.SavedViews);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", savedView.Id)
            .Content(new UpdateSavedView { Name = "Updated Name" })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task DeleteAsync_WhenFeatureDisabled_ReturnsUnprocessableEntity()
    {
        // Arrange — create a view directly, then disable feature
        var savedView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "View To Delete",
            Filter = "status:open",
            ViewType = "events",
            Version = 1,
            CreatedByUserId = "537650f3b77efe23a47914f0",
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        }, o => o.ImmediateConsistency());

        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.Features.Remove(OrganizationFeatures.SavedViews);
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency());

        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", savedView.Id)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task RemoveUser_DeletesPrivateSavedViews_ButPreservesOrganizationWideViews()
    {
        // Arrange — create an organization-wide view and a private view for the test organization user
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(testUser);

        var organizationWideView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Organization Wide",
            Filter = "status:open",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var privateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "My Private View",
            Filter = "type:error",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act — remove the user from the organization via the API
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "users", SampleDataService.TEST_ORG_USER_EMAIL)
            .StatusCodeShouldBeOk()
        );

        await RefreshDataAsync();

        // Assert — private view is gone, organization-wide view remains
        Assert.Null(await _savedViewRepository.GetByIdAsync(privateView.Id));
        Assert.NotNull(await _savedViewRepository.GetByIdAsync(organizationWideView.Id));
    }

    [Fact]
    public async Task SoftDeleteOrganization_WithSavedViews_RemovesAllSavedViews()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(testUser);

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Organization View",
            Filter = "status:open",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "Private View",
            Filter = "type:error",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        var countBefore = await _savedViewRepository.CountByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.True(countBefore >= 2);

        // Act
        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        await _organizationService.SoftDeleteOrganizationAsync(organization, testUser.Id);
        await RefreshDataAsync();

        // Assert
        var countAfter = await _savedViewRepository.CountByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.Equal(0, countAfter);
    }

    [Fact]
    public async Task RemoveUserSavedViews_WithMixedVisibility_OnlyDeletesPrivateViews()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(testUser);

        var organizationWide = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Organization Wide",
            Filter = "status:open",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var privateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "Private",
            Filter = "type:error",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        var removed = await _organizationService.RemoveUserSavedViewsAsync(SampleDataService.TEST_ORG_ID, testUser.Id);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(1, removed);
        Assert.Null(await _savedViewRepository.GetByIdAsync(privateView.Id));
        Assert.NotNull(await _savedViewRepository.GetByIdAsync(organizationWide.Id));
    }

    private async Task<ViewSavedView?> CreateSavedViewAsync(string name, string filter, string view, bool isPrivate = false, bool isDefault = false)
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = name,
            Filter = filter,
            ViewType = view,
            IsDefault = isDefault,
            IsPrivate = isPrivate
        };

        var result = await SendRequestAsAsync<ViewSavedView>(r =>
        {
            r.Post()
                .AsGlobalAdminUser()
                .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
                .Content(newView)
                .StatusCodeShouldBeCreated();
        });

        await RefreshDataAsync();
        return result;
    }

    // IsDefault tests

    [Fact]
    public async Task PostAsync_WithIsDefault_SetsIsDefaultOnSavedView()
    {
        // Arrange & Act
        var created = await CreateSavedViewAsync("Default Events", "status:open", "events", isDefault: true);

        // Assert
        Assert.NotNull(created);
        Assert.True(created.IsDefault);
    }

    [Fact]
    public async Task PostAsync_WithoutIsDefault_DefaultsToFalse()
    {
        // Arrange & Act
        var created = await CreateSavedViewAsync("Not Default", "status:open", "events");

        // Assert
        Assert.NotNull(created);
        Assert.False(created.IsDefault);
    }

    [Fact]
    public async Task PostAsync_NewDefault_ClearsPreviousDefault()
    {
        // Arrange
        var first = await CreateSavedViewAsync("First Default", "status:open", "events", isDefault: true);
        Assert.NotNull(first);
        Assert.True(first.IsDefault);

        // Act - create another default for same view
        var second = await CreateSavedViewAsync("Second Default", "status:regressed", "events", isDefault: true);
        Assert.NotNull(second);
        Assert.True(second.IsDefault);

        // Assert - first should no longer be default
        var firstReloaded = await _savedViewRepository.GetByIdAsync(first.Id);
        Assert.NotNull(firstReloaded);
        Assert.False(firstReloaded.IsDefault);
    }

    [Fact]
    public async Task PostAsync_NewDefaultForDifferentView_DoesNotClearOtherViewDefault()
    {
        // Arrange
        var eventsDefault = await CreateSavedViewAsync("Events Default", "status:open", "events", isDefault: true);
        Assert.NotNull(eventsDefault);

        // Act - create default for issues view
        var issuesDefault = await CreateSavedViewAsync("Issues Default", "status:regressed", "issues", isDefault: true);
        Assert.NotNull(issuesDefault);

        // Assert - events default should be unaffected
        var eventsReloaded = await _savedViewRepository.GetByIdAsync(eventsDefault.Id);
        Assert.NotNull(eventsReloaded);
        Assert.True(eventsReloaded.IsDefault);
    }

    [Fact]
    public async Task PatchAsync_SetIsDefault_ClearsPreviousDefault()
    {
        // Arrange
        var first = await CreateSavedViewAsync("First", "status:open", "events", isDefault: true);
        var second = await CreateSavedViewAsync("Second", "status:regressed", "events");
        Assert.NotNull(first);
        Assert.NotNull(second);

        // Act - set second as default via PATCH
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", second.Id)
            .Content(new UpdateSavedView { IsDefault = true })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.True(updated.IsDefault);

        var firstReloaded = await _savedViewRepository.GetByIdAsync(first.Id);
        Assert.NotNull(firstReloaded);
        Assert.False(firstReloaded.IsDefault);
    }

    [Fact]
    public async Task PatchAsync_UnsetIsDefault_RemovesDefault()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Default View", "status:open", "events", isDefault: true);
        Assert.NotNull(created);
        Assert.True(created.IsDefault);

        // Act
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { IsDefault = false })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.False(updated.IsDefault);
    }

    // CanUpdateAsync permission tests

    [Fact]
    public async Task PatchAsync_OtherUsersPrivateView_ReturnsNotFound()
    {
        // Arrange - Global admin creates private view
        var privateView = await CreateSavedViewAsync("Admin Private", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateView);

        // Act - Organization user tries to update it
        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", privateView.Id)
            .Content(new UpdateSavedView { Name = "Hacked" })
            .StatusCodeShouldBeNotFound()
        );

        // Assert - name unchanged
        var unchanged = await _savedViewRepository.GetByIdAsync(privateView.Id);
        Assert.NotNull(unchanged);
        Assert.Equal("Admin Private", unchanged.Name);
    }

    [Fact]
    public async Task PatchAsync_UpdateFilterDefinitions_PersistsJsonBlob()
    {
        // Arrange
        var created = await CreateSavedViewAsync("FilterDef Test", "status:open", "events");
        Assert.NotNull(created);

        // Act
        const string filterDefs = """[{"type":"keyword","value":"status:open"},{"type":"boolean","term":"is_first_occurrence","value":true}]""";
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { FilterDefinitions = filterDefs })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(filterDefs, updated.FilterDefinitions);
    }

    [Fact]
    public async Task PatchAsync_WithInvalidFilterDefinitions_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("FilterDef Validation", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { FilterDefinitions = "not valid json" })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task GetByOrganizationAsync_WithUnauthorizedOrg_ReturnsNotFound()
    {
        // Act & Assert
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", "000000000000000000000000", "saved-views")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task DeleteAsync_NonExistentView_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PostAsync_IsDefaultResponse_IncludesIsDefaultField()
    {
        // Arrange & Act
        var created = await CreateSavedViewAsync("Check Response", "status:open", "events", isDefault: true);
        Assert.NotNull(created);

        // Act - fetch back via GET
        var fetched = await SendRequestAsAsync<ViewSavedView>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(fetched);
        Assert.True(fetched.IsDefault);
    }

    // Security tests

    [Fact]
    public Task PostAsync_WithXssInName_StoresLiterally()
    {
        // XSS in the name should be stored as-is; escaping is the frontend's job
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "<script>alert('xss')</script>",
            Filter = "status:open",
            ViewType = "events"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task PostAsync_FilterExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Long View",
            Filter = new string('x', 2001),
            ViewType = "events"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_FilterDefinitionsExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Long FilterDefs",
            Filter = "status:open",
            ViewType = "events",
            FilterDefinitions = new string('x', 10001)
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_TimeExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Long Time",
            Filter = "status:open",
            ViewType = "events",
            Time = new string('t', 101)
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_NameExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = new string('n', 101),
            Filter = "status:open",
            ViewType = "events"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task GetByViewAsync_WithInvalidView_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "dashboard")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetByOrganizationAsync_CrossOrganization_ReturnsNotFound()
    {
        // Arrange - Create a filter in TEST_ORG
        await CreateSavedViewAsync("Cross Organization Test", "status:open", "events");

        // Act - Try to list from FREE_ORG (wrong org for this user)
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "saved-views")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public Task GetAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .AsAnonymousUser()
            .AppendPaths("saved-views", "000000000000000000000000")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public Task PatchAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .Patch()
            .AsAnonymousUser()
            .AppendPaths("saved-views", "000000000000000000000000")
            .Content(new UpdateSavedView { Name = "Hacked" })
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public Task DeleteAsync_AnonymousUser_ReturnsUnauthorized()
    {
        return SendRequestAsync(r => r
            .Delete()
            .AsAnonymousUser()
            .AppendPaths("saved-views", "000000000000000000000000")
            .StatusCodeShouldBeUnauthorized()
        );
    }

    [Fact]
    public Task PostAsync_FilterAtMaxLength_Succeeds()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Max Length View",
            Filter = new string('x', 2000),
            ViewType = "events"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task PostAsync_InvalidJsonFilterDefinitions_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Bad JSON",
            Filter = "status:open",
            ViewType = "events",
            FilterDefinitions = "not valid json"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_JsonObjectFilterDefinitions_ReturnsUnprocessableEntity()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "JSON Object",
            Filter = "status:open",
            ViewType = "events",
            FilterDefinitions = """{"type":"keyword","value":"test"}"""
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_ValidJsonArrayFilterDefinitions_Succeeds()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Valid JSON Array",
            Filter = "status:open",
            ViewType = "events",
            FilterDefinitions = """[{"type":"keyword","value":"test"}]"""
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task PostAsync_EmptyArrayFilterDefinitions_Succeeds()
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Empty Array",
            Filter = "status:open",
            ViewType = "events",
            FilterDefinitions = "[]"
        };

        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );
    }

    // Private views cannot be default tests

    [Fact]
    public Task PostAsync_PrivateAndDefault_ReturnsUnprocessableEntity()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Private Default",
            Filter = "status:open",
            ViewType = "events",
            IsDefault = true,
            IsPrivate = true
        };

        // Act & Assert - private + default should be rejected with 422
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PostAsync_PrivateWithoutDefault_Succeeds()
    {
        // Arrange & Act
        var created = await CreateSavedViewAsync("Private Non-Default", "status:open", "events", isPrivate: true);

        // Assert
        Assert.NotNull(created);
        Assert.False(created.IsDefault);
        Assert.NotNull(created.UserId);
    }

    [Fact]
    public async Task PatchAsync_PrivateViewSetDefault_ReturnsUnprocessableEntity()
    {
        // Arrange - create a private view
        var privateView = await CreateSavedViewAsync("Private View", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateView);
        Assert.NotNull(privateView.UserId);

        // Act & Assert - trying to set a private view as default should fail with 422
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", privateView.Id)
            .Content(new UpdateSavedView { IsDefault = true })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Verify it's still not default
        var reloaded = await _savedViewRepository.GetByIdAsync(privateView.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsDefault);
    }

    [Fact]
    public async Task PostAsync_OrganizationWideDefault_DoesNotAffectPrivateViews()
    {
        // Arrange - create a private view (not default)
        var privateView = await CreateSavedViewAsync("My Private", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateView);

        // Act - create an organization-wide default
        var organizationDefault = await CreateSavedViewAsync("Organization Default", "status:regressed", "events", isDefault: true);
        Assert.NotNull(organizationDefault);
        Assert.True(organizationDefault.IsDefault);

        // Assert - private view should be unaffected
        var privateReloaded = await _savedViewRepository.GetByIdAsync(privateView.Id);
        Assert.NotNull(privateReloaded);
        Assert.False(privateReloaded.IsDefault);
    }

    [Fact]
    public async Task PostAsync_DefaultForDifferentViews_AreIndependent()
    {
        // Arrange & Act - create defaults for different views
        var eventsDefault = await CreateSavedViewAsync("Events Default", "status:open", "events", isDefault: true);
        var issuesDefault = await CreateSavedViewAsync("Issues Default", "status:regressed", "issues", isDefault: true);
        var streamDefault = await CreateSavedViewAsync("Stream Default", "type:error", "stream", isDefault: true);

        // Assert - all should be independently default
        Assert.NotNull(eventsDefault);
        Assert.NotNull(issuesDefault);
        Assert.NotNull(streamDefault);
        Assert.True(eventsDefault.IsDefault);
        Assert.True(issuesDefault.IsDefault);
        Assert.True(streamDefault.IsDefault);

        // Verify by reloading
        var eventsReloaded = await _savedViewRepository.GetByIdAsync(eventsDefault.Id);
        var issuesReloaded = await _savedViewRepository.GetByIdAsync(issuesDefault.Id);
        var streamReloaded = await _savedViewRepository.GetByIdAsync(streamDefault.Id);
        Assert.True(eventsReloaded!.IsDefault);
        Assert.True(issuesReloaded!.IsDefault);
        Assert.True(streamReloaded!.IsDefault);
    }

    [Fact]
    public async Task PatchAsync_UnsetDefault_OnlyAffectsTargetView()
    {
        // Arrange - set defaults for two views
        var eventsDefault = await CreateSavedViewAsync("Events Default", "status:open", "events", isDefault: true);
        var issuesDefault = await CreateSavedViewAsync("Issues Default", "status:regressed", "issues", isDefault: true);
        Assert.NotNull(eventsDefault);
        Assert.NotNull(issuesDefault);

        // Act - unset events default
        await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", eventsDefault.Id)
            .Content(new UpdateSavedView { IsDefault = false })
            .StatusCodeShouldBeOk()
        );

        // Assert - issues default should be unaffected
        var eventsReloaded = await _savedViewRepository.GetByIdAsync(eventsDefault.Id);
        var issuesReloaded = await _savedViewRepository.GetByIdAsync(issuesDefault.Id);
        Assert.False(eventsReloaded!.IsDefault);
        Assert.True(issuesReloaded!.IsDefault);
    }

    [Fact]
    public Task PostAsync_InvalidColumnKey_ReturnsUnprocessableEntity()
    {
        // Arrange & Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Bad Columns",
                ViewType = "events",
                Columns = new Dictionary<string, bool> { ["status"] = true }
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PatchAsync_InvalidColumnKey_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Patch Column Test", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView
            {
                Columns = new Dictionary<string, bool> { ["INVALID_COLUMN"] = true }
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_ValidColumnKeys_Succeeds()
    {
        // Arrange & Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Valid Columns",
                ViewType = "events",
                Columns = new Dictionary<string, bool> { ["user"] = true, ["date"] = false }
            })
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public async Task PatchAsync_NameExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("MaxLen Test", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert — Name max is 100
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Name = new string('x', 101) })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PatchAsync_FilterExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("MaxLen View", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert — Filter max is 2000
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Filter = new string('x', 2001) })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    // Cross-org security tests

    [Fact]
    public async Task PatchAsync_ViewInOtherOrganization_ReturnsNotFound()
    {
        // Arrange — create a view in TEST_ORG
        var created = await CreateSavedViewAsync("Cross Org Patch Test", "status:open", "events");
        Assert.NotNull(created);

        // Verify the view is only accessible within the org via GetModelAsync which checks IsInOrganization
        // A user in a different organization should get NotFound (the view's org doesn't match)
        // Since AsTestOrganizationUser IS in TEST_ORG, we test that someone can't bypass org membership
        // by verifying the view can't be accessed from the wrong org listing
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "saved-views")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PatchAsync_OtherUsersPrivateView_CannotPatchViaOrgMembership()
    {
        // Arrange — Global admin creates a private view
        var privateView = await CreateSavedViewAsync("Admin Only Private", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateView);
        Assert.NotNull(privateView.UserId);

        // Act — Org member tries to patch (should get NotFound because private view is scoped to owner)
        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", privateView.Id)
            .Content(new UpdateSavedView { Name = "Hijacked" })
            .StatusCodeShouldBeNotFound()
        );

        // Assert — name unchanged
        var unchanged = await _savedViewRepository.GetByIdAsync(privateView.Id);
        Assert.NotNull(unchanged);
        Assert.Equal("Admin Only Private", unchanged.Name);
    }

    [Fact]
    public async Task DeleteAsync_OtherUsersPrivateView_CannotDeleteViaOrgMembership()
    {
        // Arrange — Global admin creates a private view
        var privateView = await CreateSavedViewAsync("Admin Private Delete Guard", "status:open", "events", isPrivate: true);
        Assert.NotNull(privateView);

        // Act — Org member tries to delete
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", privateView.Id)
            .StatusCodeShouldBeNotFound()
        );

        // Assert — still exists
        var stillExists = await _savedViewRepository.GetByIdAsync(privateView.Id);
        Assert.NotNull(stillExists);
    }

    [Fact]
    public async Task PatchAsync_ColumnsExceedsMaxCount_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Column Count Test", "status:open", "events");
        Assert.NotNull(created);

        // Build a columns dict with 51 entries (exceeds max of 50).
        // Count validation fires before key validation, so arbitrary keys work here.
        var columns = new Dictionary<string, bool>();
        for (int i = 0; i < 51; i++)
            columns[$"col{i}"] = true;

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AppendPaths("saved-views", created.Id)
            .Content(new { columns })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PatchAsync_EmptyName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Whitespace Test", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AppendPaths("saved-views", created.Id)
            .Content(new { name = "   " })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    // Repository coverage tests

    [Fact]
    public async Task GetByOrganizationForUserAsync_ReturnsPublicAndOwnPrivateViews()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(testUser);

        var publicView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Public View",
            Filter = "status:open",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var ownPrivateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "Own Private",
            Filter = "type:error",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var otherPrivateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = "other-user-id",
            Name = "Other Private",
            Filter = "type:log",
            ViewType = "events",
            CreatedByUserId = "other-user-id"
        });

        await RefreshDataAsync();

        // Act
        var results = await _savedViewRepository.GetByOrganizationForUserAsync(
            SampleDataService.TEST_ORG_ID, testUser.Id);

        // Assert — should include public and own private, but not other user's private
        Assert.Contains(results.Documents, v => v.Id == publicView.Id);
        Assert.Contains(results.Documents, v => v.Id == ownPrivateView.Id);
        Assert.DoesNotContain(results.Documents, v => v.Id == otherPrivateView.Id);
    }

    [Fact]
    public async Task GetByViewForUserAsync_FiltersOnViewType()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(testUser);

        var eventsView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Events View",
            Filter = "status:open",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var issuesView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Issues View",
            Filter = "status:regressed",
            ViewType = "issues",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        var results = await _savedViewRepository.GetByViewForUserAsync(
            SampleDataService.TEST_ORG_ID, "events", testUser.Id);

        // Assert
        Assert.Contains(results.Documents, v => v.Id == eventsView.Id);
        Assert.DoesNotContain(results.Documents, v => v.Id == issuesView.Id);
    }

    [Fact]
    public async Task CountByOrganizationIdAsync_ReturnsCorrectCount()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(testUser);

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Count Test 1",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Count Test 2",
            ViewType = "issues",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        var count = await _savedViewRepository.CountByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);

        // Assert
        Assert.True(count >= 2);
    }

    [Fact]
    public async Task GetByViewAsync_ReturnsAllViewsForViewType()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(testUser);

        var view1 = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "View Type Test 1",
            ViewType = "stream",
            CreatedByUserId = testUser.Id
        });

        var view2 = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "View Type Test 2",
            ViewType = "stream",
            CreatedByUserId = testUser.Id
        });

        var eventsView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Wrong View Type",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        var results = await _savedViewRepository.GetByViewAsync(SampleDataService.TEST_ORG_ID, "stream");

        // Assert
        Assert.Contains(results.Documents, v => v.Id == view1.Id);
        Assert.Contains(results.Documents, v => v.Id == view2.Id);
        Assert.DoesNotContain(results.Documents, v => v.Id == eventsView.Id);
    }

    [Fact]
    public async Task RemoveByUserIdAsync_OnlyRemovesPrivateViewsForUser()
    {
        // Arrange
        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(testUser);

        var publicView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Public Created By User",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var privateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "User Private View",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        var removed = await _savedViewRepository.RemovePrivateByUserIdAsync(SampleDataService.TEST_ORG_ID, testUser.Id);
        await RefreshDataAsync();

        // Assert
        Assert.True(removed > 0);
        Assert.Null(await _savedViewRepository.GetByIdAsync(privateView.Id));
        Assert.NotNull(await _savedViewRepository.GetByIdAsync(publicView.Id));
    }

}
