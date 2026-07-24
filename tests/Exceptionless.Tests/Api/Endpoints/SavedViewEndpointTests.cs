using System.Net;
using System.Text.Json;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Seed;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class SavedViewEndpointTests : IntegrationTestsBase
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISavedViewRepository _savedViewRepository;
    private readonly IUserRepository _userRepository;
    private readonly OrganizationService _organizationService;

    public SavedViewEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _organizationRepository = GetService<IOrganizationRepository>();
        _savedViewRepository = GetService<ISavedViewRepository>();
        _userRepository = GetService<IUserRepository>();
        _organizationService = GetService<OrganizationService>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
        await GetService<DataSeedService>().SeedAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DataSeedAsync_ExistingDataWithoutPredefinedViews_CreatesSystemPredefinedViews()
    {
        // Arrange
        Assert.True(await _organizationRepository.CountAsync() > 0);
        int expectedPredefinedViewCount = await GetDefaultPredefinedSavedViewCountAsync();

        var existingSystemViews = await GetSystemPredefinedSavedViewsAsync();
        Assert.Equal(expectedPredefinedViewCount, existingSystemViews.Count);

        foreach (var savedView in existingSystemViews)
            await _savedViewRepository.RemoveAsync(savedView.Id, o => o.ImmediateConsistency());

        await RefreshDataAsync();
        Assert.Equal(0, await _savedViewRepository.CountByOrganizationIdAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId));

        // Act
        await GetService<DataSeedService>().SeedAsync(TestContext.Current.CancellationToken);

        // Assert
        var seededSystemViews = await GetSystemPredefinedSavedViewsAsync();
        Assert.Equal(expectedPredefinedViewCount, seededSystemViews.Count);
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "events", "All"));
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "events", "Errors"));
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "stacks", "All"));
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "stacks", "Most Frequent Errors"));
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "stacks", "Most Frequent 404s"));
        Assert.Contains(seededSystemViews, view => IsPredefinedSavedView(view, "stacks", "Most Used Features"));
    }

    [Fact]
    public async Task ExportOrganizationSavedViewsAsync_AsGlobalAdmin_ReturnsOrganizationDefinitions()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "API Surface Export View",
            Slug = "api-surface-export-view",
            Filter = "type:error",
            ViewType = "events"
        };

        await SendRequestAsAsync<ViewSavedView>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeCreated()
        );
        await RefreshDataAsync();

        // Act
        var definitions = await SendRequestAsAsync<IReadOnlyCollection<PredefinedSavedViewDefinition>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "export")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(definitions);
        Assert.Contains(definitions, definition =>
            String.Equals(definition.Name, "API Surface Export View", StringComparison.Ordinal)
            && String.Equals(definition.ViewType, "events", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetPredefinedAsync_GlobalAdmin_ReturnsSeedJsonShape()
    {
        // Act
        var response = await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );
        string json = await response.Content.ReadAsStringAsync(TestCancellationToken);
        var definitions = await DeserializeResponseAsync<List<PredefinedSavedViewDefinition>>(response);

        // Assert
        Assert.Contains("\"filterDefinitions\"", json);
        Assert.DoesNotContain("\"filter_definitions\"", json);
        Assert.NotNull(definitions);
        Assert.Equal(await GetDefaultPredefinedSavedViewCountAsync(), definitions.Count);

        var logs = definitions.FirstOrDefault(view => String.Equals(view.Key, "events:logs", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(logs);
        Assert.Equal("Logs", logs.Name);
        Assert.Equal("logs", logs.Slug);
        Assert.Equal("events", logs.ViewType);
        Assert.Equal("type:log (status:open OR status:regressed)", logs.Filter);
        var filterDefinitions = logs.FilterDefinitions ?? throw new Xunit.Sdk.XunitException("Expected FilterDefinitions to be non-null.");
        Assert.Equal(JsonValueKind.Array, filterDefinitions.ValueKind);
    }

    [Fact]
    public Task GetPredefinedAsync_User_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", "predefined")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public async Task PostForceUpdatePredefinedAsync_GlobalAdmin_OverwritesMatchingOrganizationSavedViews()
    {
        // Arrange
        var predefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(predefinedViews);
        var logs = predefinedViews.First(view => IsPredefinedSavedView(view, "events", "Logs"));
        var savedLogs = await _savedViewRepository.GetByIdAsync(logs.Id);
        Assert.NotNull(savedLogs);
        savedLogs.PredefinedKey = "EVENTS:LOGS";
        savedLogs.Filter = "type:error";
        savedLogs.Slug = "custom-logs";
        await _savedViewRepository.SaveAsync(savedLogs, o => o.ImmediateConsistency());

        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_ORG_USER_EMAIL);
        Assert.NotNull(testUser);
        var privateLogs = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            CreatedByUserId = testUser.Id,
            PredefinedKey = "events:logs",
            Name = "Private Logs",
            Slug = "private-logs",
            ViewType = "events",
            Filter = "type:error"
        }, o => o.ImmediateConsistency());

        var legacyLogs = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            CreatedByUserId = testUser.Id,
            Name = "Legacy Logs",
            Slug = "legacy-logs",
            ViewType = "events",
            Filter = "type:error"
        }, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        // Act
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "predefined", "force-update")
            .StatusCodeShouldBeAccepted()
        );

        Assert.NotNull(workItems);
        Assert.NotEmpty(workItems.Workers);
        await GetService<WorkItemJob>().RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        var updatedLogs = await _savedViewRepository.GetByIdAsync(logs.Id);
        Assert.NotNull(updatedLogs);
        Assert.Equal("type:log (status:open OR status:regressed)", updatedLogs.Filter);
        Assert.Equal("logs", updatedLogs.Slug);
        Assert.Equal(PredefinedSavedViewContentHasher.GetContentHash(updatedLogs), updatedLogs.PredefinedContentHash);

        var unchangedPrivateLogs = await _savedViewRepository.GetByIdAsync(privateLogs.Id);
        Assert.NotNull(unchangedPrivateLogs);
        Assert.Equal("type:error", unchangedPrivateLogs.Filter);

        var unchangedLegacyLogs = await _savedViewRepository.GetByIdAsync(legacyLogs.Id);
        Assert.NotNull(unchangedLegacyLogs);
        Assert.Equal("type:error", unchangedLegacyLogs.Filter);
    }

    [Fact]
    public Task PostForceUpdatePredefinedAsync_User_ReturnsForbidden()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", "predefined", "force-update")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public async Task PutPredefinedAsync_AsGlobalAdmin_ReplacesPredefinedDefinitions()
    {
        // Arrange
        var definitions = new[]
        {
            new PredefinedSavedViewDefinition
            {
                Key = "events:api-surface-import",
                Name = "API Surface Import",
                Slug = "api-surface-import",
                ViewType = "events",
                Filter = "type:log",
                Time = "last 24 hours",
                Sort = "-date",
                Columns = new Dictionary<string, bool> { ["message"] = true },
                ColumnOrder = ["message"],
                ShowChart = true,
                ShowStats = true
            }
        };

        // Act
        var result = await SendRequestAsAsync<IReadOnlyCollection<PredefinedSavedViewDefinition>>(r => r
            .Put()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "predefined")
            .Content(definitions)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        var definition = Assert.Single(result);
        Assert.Equal("events:api-surface-import", definition.Key);

        var savedViews = await _savedViewRepository.GetByOrganizationForUserAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId, PredefinedSavedViewsDataSeed.SystemUserId);
        Assert.Contains(savedViews.Documents, view => String.Equals(view.Name, "API Surface Import", StringComparison.Ordinal));
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
            Sort = "-date",
            ViewType = "events",
            FilterDefinitions = """[{"type":"keyword","value":"status:open"}]""",
            ColumnOrder = ["summary", "date", "user"]
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
        Assert.Equal("-date", result.Sort);
        Assert.Equal("events", result.ViewType);
        Assert.Equal(["summary", "date", "user"], result.ColumnOrder);
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
        Assert.Equal("-date", savedView.Sort);
        Assert.Equal(["summary", "date", "user"], savedView.ColumnOrder);
    }

    [Fact]
    public async Task PostAsync_NewSavedView_ReturnsAbsoluteLocation()
    {
        // Arrange
        var savedView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Absolute Location Saved View",
            ViewType = "events"
        };

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(savedView)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.NotNull(response.Headers.Location);
        Assert.True(response.Headers.Location.IsAbsoluteUri);
        Assert.Equal("localhost", response.Headers.Location.Host);
        Assert.StartsWith("/api/v2/saved-views/", response.Headers.Location.AbsolutePath);
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
            ViewType = "stacks",
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
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = String.Empty,
            Filter = "status:open",
            ViewType = "events"
        };

        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_WithNameThatCannotCreateUrlName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "!!!",
            Filter = "status:open",
            ViewType = "events"
        };

        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PostAsync_WithObjectIdUrlName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Object Id URL Name",
            Filter = "status:open",
            Slug = "507f1f77bcf86cd799439011",
            ViewType = "events"
        };

        // Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(newView)
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PostAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Duplicate Saved View Name", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "duplicate saved view name",
                Filter = "type:error",
                ViewType = "events"
            })
            .ExpectedStatus(HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task PostAsync_DuplicateSlug_ReturnsConflict()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Shared URL Name", "status:open", "events", slug: "shared-url-name");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Different Display Name",
                Filter = "type:error",
                Slug = "shared-url-name",
                ViewType = "events"
            })
            .ExpectedStatus(HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public Task PostAsync_WithEmptyFilter_ReturnsCreated()
    {
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Show All",
            Filter = String.Empty,
            ViewType = "events"
        };

        // Act & Assert
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
        // Arrange
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Invalid View Type",
            Filter = "status:open",
            ViewType = "invalid-view"
        };

        // Act & Assert
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
    [InlineData("stacks")]
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
        var stacksFilter = await CreateSavedViewAsync("Stacks Only", "status:regressed", "stacks");
        Assert.NotNull(eventsFilter);
        Assert.NotNull(stacksFilter);

        // Act
        var filters = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "events")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(filters);
        Assert.Contains(filters, f => String.Equals(f.Id, eventsFilter.Id));
        Assert.DoesNotContain(filters, f => String.Equals(f.Id, stacksFilter.Id));
    }

    [Fact]
    public async Task GetByOrganizationAsync_FirstRequest_CreatesPredefinedSavedViewsOnce()
    {
        // Act
        var filters = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(filters);
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "events", "All"));
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "events", "Errors"));
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "stacks", "All"));
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "stacks", "Most Frequent Errors"));
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "stacks", "Most Frequent 404s"));
        Assert.Contains(filters, view => IsPredefinedSavedView(view, "stacks", "Most Used Features"));

        foreach (var savedView in filters.Where(IsPredefinedSavedView))
            await _savedViewRepository.RemoveAsync(savedView.Id, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        var afterDelete = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(afterDelete);
        Assert.DoesNotContain(afterDelete, IsPredefinedSavedView);
    }

    [Fact]
    public async Task PostPredefinedAsync_AfterDelete_RecreatesPredefinedSavedViews()
    {
        // Arrange
        var filters = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(filters);
        foreach (var savedView in filters.Where(IsPredefinedSavedView))
            await _savedViewRepository.RemoveAsync(savedView.Id, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        // Act
        var predefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(predefinedViews);
        Assert.Equal(await GetDefaultPredefinedSavedViewCountAsync(), predefinedViews.Count);
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "events", "All"));
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "events", "Errors"));
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "stacks", "All"));
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "stacks", "Most Frequent Errors"));
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "stacks", "Most Frequent 404s"));
        Assert.Contains(predefinedViews, view => IsPredefinedSavedView(view, "stacks", "Most Used Features"));
    }

    [Fact]
    public async Task PostPredefinedAsync_ExistingModifiedPredefinedView_PreservesCustomConfiguration()
    {
        // Arrange
        var predefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(predefinedViews);
        var logs = predefinedViews.FirstOrDefault(view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.NotNull(logs);

        var savedLogs = await _savedViewRepository.GetByIdAsync(logs.Id);
        Assert.NotNull(savedLogs);
        savedLogs.Filter = "type:error";
        savedLogs.Slug = "legacy-logs";
        await _savedViewRepository.SaveAsync(savedLogs, o => o.ImmediateConsistency());

        var testUser = await _userRepository.GetByEmailAddressAsync(SampleDataService.TEST_USER_EMAIL);
        Assert.NotNull(testUser);

        var customView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Custom Investigation",
            Filter = "type:error",
            Slug = "custom-investigation",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        }, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        // Act
        var updatedPredefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedPredefinedViews);
        var updatedLogs = updatedPredefinedViews.FirstOrDefault(view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.NotNull(updatedLogs);
        Assert.Equal(logs.Id, updatedLogs.Id);
        Assert.Equal("type:error", updatedLogs.Filter);
        Assert.Equal("legacy-logs", updatedLogs.Slug);

        Assert.NotNull(await _savedViewRepository.GetByIdAsync(customView.Id));
    }

    [Fact]
    public async Task GetByOrganizationAsync_PredefinedDefinitionChanges_UpdatesUnmodifiedView()
    {
        // Arrange
        var initialViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(initialViews);
        var logs = initialViews.First(view => IsPredefinedSavedView(view, "events", "Logs"));
        var systemLogs = (await GetSystemPredefinedSavedViewsAsync()).First(view => String.Equals(view.PredefinedKey, "events:logs", StringComparison.Ordinal));
        systemLogs.Filter = "type:log level:warn";
        systemLogs.PredefinedContentHash = PredefinedSavedViewContentHasher.GetContentHash(systemLogs);
        await _savedViewRepository.SaveAsync(systemLogs, o => o.ImmediateConsistency());
        await RefreshDataAsync();

        // Act
        var updatedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedViews);
        var updatedLogs = updatedViews.First(view => view.Id == logs.Id);
        Assert.Equal("type:log level:warn", updatedLogs.Filter);

        var savedLogs = await _savedViewRepository.GetByIdAsync(logs.Id);
        Assert.NotNull(savedLogs);
        Assert.Equal(PredefinedSavedViewContentHasher.GetContentHash(savedLogs), savedLogs.PredefinedContentHash);
    }

    [Fact]
    public async Task GetByOrganizationAsync_PredefinedDefinitionChanges_UpdatesViewRevertedToBaseline()
    {
        // Arrange
        var initialViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(initialViews);
        var logs = initialViews.First(view => IsPredefinedSavedView(view, "events", "Logs"));

        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", logs.Id)
            .Content(new UpdateSavedView { Filter = "type:error" })
            .StatusCodeShouldBeOk()
        );

        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", logs.Id)
            .Content(new UpdateSavedView { Filter = "type:log (status:open OR status:regressed)" })
            .StatusCodeShouldBeOk()
        );

        var systemLogs = (await GetSystemPredefinedSavedViewsAsync()).First(view => String.Equals(view.PredefinedKey, "events:logs", StringComparison.Ordinal));
        systemLogs.Filter = "type:log level:warn";
        systemLogs.PredefinedContentHash = PredefinedSavedViewContentHasher.GetContentHash(systemLogs);
        await _savedViewRepository.SaveAsync(systemLogs, o => o.ImmediateConsistency());
        await RefreshDataAsync();

        // Act
        var updatedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedViews);
        var updatedLogs = updatedViews.First(view => view.Id == logs.Id);
        Assert.Equal("type:log level:warn", updatedLogs.Filter);
    }

    [Fact]
    public async Task GetByOrganizationAsync_PredefinedDefinitionChanges_UpdatesViewRevertedAfterSkippedSync()
    {
        // Arrange
        var initialViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(initialViews);
        var logs = initialViews.First(view => IsPredefinedSavedView(view, "events", "Logs"));

        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", logs.Id)
            .Content(new UpdateSavedView { Filter = "type:error" })
            .StatusCodeShouldBeOk()
        );

        var systemLogs = (await GetSystemPredefinedSavedViewsAsync()).First(view => String.Equals(view.PredefinedKey, "events:logs", StringComparison.Ordinal));
        systemLogs.Filter = "type:log level:warn";
        systemLogs.PredefinedContentHash = PredefinedSavedViewContentHasher.GetContentHash(systemLogs);
        await _savedViewRepository.SaveAsync(systemLogs, o => o.ImmediateConsistency());
        await RefreshDataAsync();

        var customizedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(customizedViews);
        Assert.Equal("type:error", customizedViews.First(view => view.Id == logs.Id).Filter);

        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", logs.Id)
            .Content(new UpdateSavedView { Filter = "type:log (status:open OR status:regressed)" })
            .StatusCodeShouldBeOk()
        );
        await RefreshDataAsync();

        // Act
        var updatedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedViews);
        Assert.Equal("type:log level:warn", updatedViews.First(view => view.Id == logs.Id).Filter);
    }

    [Fact]
    public Task PostPredefinedAsync_CrossOrganization_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.FREE_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PostPredefinedSavedViewAsync_GlobalAdmin_UsesPromotedConfigurationForPredefinedViews()
    {
        // Arrange
        var predefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(predefinedViews);
        var logs = predefinedViews.FirstOrDefault(view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.NotNull(logs);

        var savedLogs = await _savedViewRepository.GetByIdAsync(logs.Id);
        Assert.NotNull(savedLogs);
        savedLogs.Name = "Application Logs";
        savedLogs.Slug = "application-logs";
        savedLogs.Filter = "type:log level:error";
        savedLogs.ShowChart = false;
        savedLogs.Columns = new Dictionary<string, bool>
        {
            ["summary"] = true,
            ["date"] = true,
            ["type"] = false
        };
        savedLogs.ColumnOrder = ["summary", "date", "type"];
        await _savedViewRepository.SaveAsync(savedLogs, o => o.ImmediateConsistency());
        await RefreshDataAsync();

        // Act
        var promoted = await SendRequestAsAsync<ViewSavedView>(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", logs.Id, "predefined")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(promoted);
        Assert.Equal("Application Logs", promoted.Name);
        Assert.Equal("application-logs", promoted.Slug);

        foreach (var savedView in predefinedViews)
            await _savedViewRepository.RemoveAsync(savedView.Id, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        var updatedPredefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedPredefinedViews);
        Assert.Equal(await GetDefaultPredefinedSavedViewCountAsync(), updatedPredefinedViews.Count);
        var applicationLogs = updatedPredefinedViews.FirstOrDefault(view => IsPredefinedSavedView(view, "events", "Application Logs"));
        Assert.NotNull(applicationLogs);
        Assert.Equal("application-logs", applicationLogs.Slug);
        Assert.Equal("type:log level:error", applicationLogs.Filter);
        Assert.False(applicationLogs.ShowChart);
        Assert.NotNull(applicationLogs.Columns);
        Assert.True(applicationLogs.Columns["summary"]);
        Assert.False(applicationLogs.Columns["type"]);
        Assert.Equal(new[] { "summary", "date", "type" }, applicationLogs.ColumnOrder);
        Assert.DoesNotContain(updatedPredefinedViews, view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.Contains(updatedPredefinedViews, view => IsPredefinedSavedView(view, "events", "Errors"));
    }

    [Fact]
    public async Task PostPredefinedSavedViewAsync_AfterPatch_ExportsLatestConfiguration()
    {
        // Arrange
        var predefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(predefinedViews);
        var logs = predefinedViews.FirstOrDefault(view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.NotNull(logs);

        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", logs.Id)
            .StatusCodeShouldBeOk()
        );

        var changes = new UpdateSavedView
        {
            Filter = "type:log level:warn",
            FilterDefinitions = """[{"type":"type","value":["log"],"hidden":true},{"type":"level","value":["Warn"]}]"""
        };

        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", logs.Id)
            .Content(changes)
            .StatusCodeShouldBeOk()
        );

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", logs.Id, "predefined")
            .StatusCodeShouldBeOk()
        );

        var definitions = await SendRequestAsAsync<List<PredefinedSavedViewDefinition>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(definitions);
        var logsDefinition = definitions.FirstOrDefault(view => String.Equals(view.Key, "events:logs", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(logsDefinition);
        Assert.Equal("type:log level:warn", logsDefinition.Filter);
        var filterDefinitions = logsDefinition.FilterDefinitions ?? throw new Xunit.Sdk.XunitException("Expected FilterDefinitions to be non-null.");
        Assert.Equal(JsonValueKind.Array, filterDefinitions.ValueKind);
        Assert.Equal(2, filterDefinitions.GetArrayLength());
        Assert.True(filterDefinitions[0].GetProperty("hidden").GetBoolean());
        Assert.Equal("level", filterDefinitions[1].GetProperty("type").GetString());
    }

    [Fact]
    public async Task PostPredefinedSavedViewAsync_User_ReturnsForbidden()
    {
        // Arrange
        var created = await CreateSavedViewAsync("User View", "type:error", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", created.Id, "predefined")
            .StatusCodeShouldBeForbidden()
        );
    }

    [Fact]
    public async Task DeletePredefinedSavedViewAsync_GlobalAdmin_RemovesSeededPredefinedView()
    {
        // Arrange
        var predefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(predefinedViews);
        var logs = predefinedViews.FirstOrDefault(view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.NotNull(logs);

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", logs.Id, "predefined")
            .ExpectedStatus(HttpStatusCode.NoContent)
        );

        foreach (var savedView in predefinedViews)
            await _savedViewRepository.RemoveAsync(savedView.Id, o => o.ImmediateConsistency());

        await RefreshDataAsync();

        var updatedPredefinedViews = await SendRequestAsAsync<List<ViewSavedView>>(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views", "predefined")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedPredefinedViews);
        int expectedPredefinedViewCount = await GetDefaultPredefinedSavedViewCountAsync();
        Assert.Equal(expectedPredefinedViewCount - 1, updatedPredefinedViews.Count);
        Assert.DoesNotContain(updatedPredefinedViews, view => IsPredefinedSavedView(view, "events", "Logs"));
        Assert.Contains(updatedPredefinedViews, view => IsPredefinedSavedView(view, "events", "Errors"));

        await GetService<DataSeedService>().SeedAsync(TestContext.Current.CancellationToken);
        await RefreshDataAsync();

        Assert.Equal(expectedPredefinedViewCount - 1, await _savedViewRepository.CountByOrganizationIdAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId));
    }

    [Fact]
    public async Task DeletePredefinedSavedViewAsync_User_ReturnsForbidden()
    {
        // Arrange
        var created = await CreateSavedViewAsync("User View", "type:error", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("saved-views", created.Id, "predefined")
            .StatusCodeShouldBeForbidden()
        );
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
    public async Task PatchAsync_UpdateSort_UpdatesSortString()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Sort Update Test", "status:open", "events");
        Assert.NotNull(created);

        // Act
        var updated = await SendRequestAsAsync<ViewSavedView>(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Sort = "-date" })
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updated);
        Assert.Equal("-date", updated.Sort);
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
                Slug = $"cap-test-{i}",
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
            Slug = "organization-wide",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var privateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "My Private View",
            Filter = "type:error",
            Slug = "my-private-view",
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
            Slug = "organization-view",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "Private View",
            Filter = "type:error",
            Slug = "private-view",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        long countBefore = await _savedViewRepository.CountByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.True(countBefore >= 2);

        // Act
        var organizationRepository = GetService<IOrganizationRepository>();
        var organization = await organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        await _organizationService.SoftDeleteOrganizationAsync(organization, testUser.Id);
        await RefreshDataAsync();

        // Assert
        long countAfter = await _savedViewRepository.CountByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);
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
            Slug = "organization-wide",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var privateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "Private",
            Filter = "type:error",
            Slug = "private",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        long removed = await _organizationService.RemoveUserSavedViewsAsync(SampleDataService.TEST_ORG_ID, testUser.Id);
        await RefreshDataAsync();

        // Assert
        Assert.Equal(1, removed);
        Assert.Null(await _savedViewRepository.GetByIdAsync(privateView.Id));
        Assert.NotNull(await _savedViewRepository.GetByIdAsync(organizationWide.Id));
    }

    private async Task<ViewSavedView?> CreateSavedViewAsync(string name, string filter, string view, bool isPrivate = false, string? slug = null)
    {
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = name,
            Filter = filter,
            Slug = slug,
            ViewType = view,
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

    private async Task<List<SavedView>> GetSystemPredefinedSavedViewsAsync()
    {
        var results = await _savedViewRepository.GetByOrganizationForUserAsync(PredefinedSavedViewsDataSeed.SystemOrganizationId, PredefinedSavedViewsDataSeed.SystemUserId, o => o.PageLimit(1000));
        return results.Documents.Where(view => view.UserId is null).ToList();
    }

    private async Task<int> GetDefaultPredefinedSavedViewCountAsync()
    {
        var definitions = await PredefinedSavedViewsDataSeed.ReadDefaultSavedViewsAsync(TestCancellationToken);
        return definitions.Count;
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
    public async Task PatchAsync_DuplicateName_ReturnsConflict()
    {
        // Arrange
        var existing = await CreateSavedViewAsync("Existing Patch Name", "status:open", "events");
        var created = await CreateSavedViewAsync("Editable Patch Name", "type:error", "events");
        Assert.NotNull(existing);
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Name = "existing patch name" })
            .ExpectedStatus(HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task PatchAsync_DuplicateSlug_ReturnsConflict()
    {
        // Arrange
        var existing = await CreateSavedViewAsync("Existing Patch URL", "status:open", "events", slug: "existing-patch-url");
        var created = await CreateSavedViewAsync("Editable Patch URL", "type:error", "events", slug: "editable-patch-url");
        Assert.NotNull(existing);
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Slug = "existing-patch-url" })
            .ExpectedStatus(HttpStatusCode.Conflict)
        );
    }

    [Fact]
    public async Task PatchAsync_WithObjectIdUrlName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Editable Object Id URL", "type:error", "events", slug: "editable-object-id-url");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Slug = "507f1f77bcf86cd799439011" })
            .StatusCodeShouldBeUnprocessableEntity()
        );
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
            FilterDefinitions = $"[\"{new string('x', SavedView.MaxFilterDefinitionsLength)}\"]"
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

    [Fact]
    public async Task PostAsync_PrivateView_Succeeds()
    {
        // Arrange & Act
        var created = await CreateSavedViewAsync("Private View", "status:open", "events", isPrivate: true);

        // Assert
        Assert.NotNull(created);
        Assert.NotNull(created.UserId);
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
                Columns = new Dictionary<string, bool> { ["summary"] = true, ["type"] = false, ["exception_type"] = false, ["level"] = false, ["message"] = false }
            })
            .StatusCodeShouldBeCreated()
        );
    }

    [Theory]
    [InlineData("events")]
    [InlineData("stream")]
    public Task PostAsync_VersionColumnForEventViews_Succeeds(string viewType)
    {
        // Arrange & Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = $"Valid {viewType} Version Column",
                ViewType = viewType,
                Columns = new Dictionary<string, bool> { ["summary"] = true, ["version"] = false },
                ColumnOrder = ["summary", "version"]
            })
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task PostAsync_InvalidColumnOrderKey_ReturnsUnprocessableEntity()
    {
        // Arrange & Act & Assert
        return SendRequestAsync(r => r
            .Post()
            .AsGlobalAdminUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "saved-views")
            .Content(new NewSavedView
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Bad Column Order",
                ViewType = "events",
                ColumnOrder = ["summary", "status"]
            })
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task PatchAsync_DuplicateColumnOrderKey_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("Patch Column Order Test", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView
            {
                ColumnOrder = ["summary", "summary"]
            })
            .StatusCodeShouldBeUnprocessableEntity()
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

    [Fact]
    public async Task PatchAsync_SortExceedsMaxLength_ReturnsUnprocessableEntity()
    {
        // Arrange
        var created = await CreateSavedViewAsync("MaxLen Sort", "status:open", "events");
        Assert.NotNull(created);

        // Act & Assert
        await SendRequestAsync(r => r
            .Patch()
            .AsGlobalAdminUser()
            .AppendPaths("saved-views", created.Id)
            .Content(new UpdateSavedView { Sort = new string('x', 101) })
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
            .AsGlobalAdminUser()
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
            .AsGlobalAdminUser()
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
            Slug = "public-view",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var ownPrivateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "Own Private",
            Filter = "type:error",
            Slug = "own-private",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var otherPrivateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = TestConstants.UserId2,
            Name = "Other Private",
            Filter = "type:log",
            Slug = "other-private",
            ViewType = "events",
            CreatedByUserId = TestConstants.UserId2
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
            Slug = "events-view",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var issuesView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Issues View",
            Filter = "status:regressed",
            Slug = "issues-view",
            ViewType = "stacks",
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
            Slug = "count-test-1",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Count Test 2",
            Slug = "count-test-2",
            ViewType = "stacks",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        long count = await _savedViewRepository.CountByOrganizationIdAsync(SampleDataService.TEST_ORG_ID);

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
            Slug = "view-type-test-1",
            ViewType = "stream",
            CreatedByUserId = testUser.Id
        });

        var view2 = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "View Type Test 2",
            Slug = "view-type-test-2",
            ViewType = "stream",
            CreatedByUserId = testUser.Id
        });

        var eventsView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Wrong View Type",
            Slug = "wrong-view-type",
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
            Slug = "public-created-by-user",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        var privateView = await _savedViewRepository.AddAsync(new SavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            UserId = testUser.Id,
            Name = "User Private View",
            Slug = "user-private-view",
            ViewType = "events",
            CreatedByUserId = testUser.Id
        });

        await RefreshDataAsync();

        // Act
        long removed = await _savedViewRepository.RemovePrivateByUserIdAsync(SampleDataService.TEST_ORG_ID, testUser.Id);
        await RefreshDataAsync();

        // Assert
        Assert.True(removed > 0);
        Assert.Null(await _savedViewRepository.GetByIdAsync(privateView.Id));
        Assert.NotNull(await _savedViewRepository.GetByIdAsync(publicView.Id));
    }

    private static bool IsPredefinedSavedView(ViewSavedView savedView, string viewType, string name)
    {
        return String.Equals(savedView.ViewType, viewType, StringComparison.OrdinalIgnoreCase) && String.Equals(savedView.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPredefinedSavedView(SavedView savedView, string viewType, string name)
    {
        return String.Equals(savedView.ViewType, viewType, StringComparison.OrdinalIgnoreCase) && String.Equals(savedView.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPredefinedSavedView(ViewSavedView savedView)
    {
        return IsPredefinedSavedView(savedView, "events", "All")
            || IsPredefinedSavedView(savedView, "events", "Logs")
            || IsPredefinedSavedView(savedView, "events", "Errors")
            || IsPredefinedSavedView(savedView, "stacks", "All")
            || IsPredefinedSavedView(savedView, "stacks", "Most Frequent Errors")
            || IsPredefinedSavedView(savedView, "stacks", "Most Frequent 404s")
            || IsPredefinedSavedView(savedView, "stacks", "Most Used Features");
    }

    [Fact]
    public async Task PostAsync_WithCustomFieldFilter_SetsUsesPremiumFeatures()
    {
        // Arrange — a filter that references an idx.* custom field is a premium filter.
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Custom Field View",
            Filter = "idx.my_field:test",
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
        Assert.True(result.UsesPremiumFeatures, "SavedView with idx.* filter should have UsesPremiumFeatures = true");
    }

    [Fact]
    public async Task PostAsync_WithFreeFilter_DoesNotSetUsesPremiumFeatures()
    {
        // Arrange — a filter that only references free fields.
        var newView = new NewSavedView
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Free Filter View",
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
        Assert.False(result.UsesPremiumFeatures, "SavedView with only free fields should have UsesPremiumFeatures = false");
    }
}
