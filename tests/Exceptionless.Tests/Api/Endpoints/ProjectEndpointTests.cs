using System.Net;
using System.Text.Json;
using Exceptionless.Core.Extensions;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Api.Results;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Foundatio.Serializer;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed class ProjectEndpointTests : IntegrationTestsBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IStackRepository _stackRepository;

    public ProjectEndpointTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _eventRepository = GetService<IEventRepository>();
        _organizationRepository = GetService<IOrganizationRepository>();
        _projectRepository = GetService<IProjectRepository>();
        _stackRepository = GetService<IStackRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
    }

    [Fact]
    public async Task AddSlackAsync_WithExistingSlackToken_ReturnsNotModified()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.Data ??= new DataDictionary();
        project.Data[Project.KnownDataKeys.SlackToken] = "already-configured";
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "slack")
            .QueryString("code", "valid-code")
            .Content(new { })
            .ExpectedStatus(HttpStatusCode.NotModified)
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task AddSlackAsync_WithNonJsonBody_ReturnsUnsupportedMediaType()
    {
        // Arrange
        const string code = "valid-slack-code";

        // Act
        using var response = await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "slack")
            .QueryString("code", code)
            .Content("ignored", "text/plain")
            .ExpectedStatus(HttpStatusCode.UnsupportedMediaType)
        );

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.False(project.NotificationSettings.ContainsKey(Project.NotificationIntegrations.Slack));
    }

    [Fact]
    public async Task AddSlackAsync_WithValidCode_PersistsSlackToken()
    {
        // Arrange
        const string code = "valid-slack-code";

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "slack")
            .QueryString("code", code)
            .Content(new { })
            .StatusCodeShouldBeOk()
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.True(project.NotificationSettings.ContainsKey(Project.NotificationIntegrations.Slack));
        var token = project.GetSlackToken(GetService<ITextSerializer>());
        Assert.NotNull(token);
        Assert.Equal("xoxp-valid-slack-code", token.AccessToken);
        Assert.Equal("team-valid-slack-code", token.TeamId);
        Assert.NotNull(token.IncomingWebhook);
        Assert.Equal("https://hooks.slack.test/valid-slack-code", token.IncomingWebhook.Url);
    }

    [Fact]
    public async Task DeleteAsync_ExistingProject_RemovesProject()
    {
        // Arrange
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Project To Delete",
                DeleteBotDataEnabled = false
            })
            .StatusCodeShouldBeCreated()
        );
        Assert.NotNull(project);

        // Act
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", project.Id)
            .StatusCodeShouldBeAccepted()
        );

        Assert.NotNull(workItems);

        var workItemJob = GetService<WorkItemJob>();
        await workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        var deleted = await _projectRepository.GetByIdAsync(project.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public Task DeleteAsync_NonExistentProject_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", "000000000000000000000000")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteDataAsync_ExistingKey_RemovesDataKey()
    {
        // Arrange - add a data key first
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "data")
            .QueryString("key", "MyDataKey")
            .Content(new ValueFromBody<string>("MyDataValue"))
            .StatusCodeShouldBeOk()
        );

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "data")
            .QueryString("key", "MyDataKey")
            .StatusCodeShouldBeOk()
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.False(project.Data?.ContainsKey("MyDataKey") ?? false);
    }

    [Fact]
    public Task DeleteDataAsync_InvalidKey_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "data")
            .QueryString("key", "-invalid")
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public Task DeleteDataAsync_NonExistentProject_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", "000000000000000000000000", "data")
            .QueryString("key", "SomeKey")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task DeleteDataAsync_OmittedKey_ReturnsBadRequest()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", projectId, "data")
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DemoteTabAsync_WithExistingPromotedTab_RemovesPromotedTab()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.PromotedTabs = ["regressions", "timeline"];
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "promotedtabs")
            .QueryString("name", "regressions")
            .StatusCodeShouldBeOk()
        );

        // Assert
        var updatedProject = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(updatedProject);
        Assert.DoesNotContain("regressions", updatedProject.PromotedTabs ?? []);
        Assert.Contains("timeline", updatedProject.PromotedTabs ?? []);
    }

    [Fact]
    public async Task DemoteTabAsync_WithOmittedName_ReturnsBadRequest()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", projectId, "promotedtabs")
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateSampleDataAsync_ValidProject_QueuesSampleEvents()
    {
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "sample-data")
            .StatusCodeShouldBeAccepted()
        );

        Assert.NotNull(workItems);
        Assert.Single(workItems.Workers);

        var workItemJob = GetService<WorkItemJob>();
        await workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        long projectEventCount = await _eventRepository.CountAsync(q => q.Project(SampleDataService.TEST_PROJECT_ID));
        long otherProjectEventCount = await _eventRepository.CountAsync(q => q.Project(SampleDataService.TEST_ROCKET_SHIP_PROJECT_ID));
        Assert.True(projectEventCount >= 100, $"Expected at least 100 generated events but found {projectEventCount}.");
        Assert.Equal(0, otherProjectEventCount);
    }

    [Fact]
    public async Task GetAllAsync_AsTestUser_ReturnsUserProjects()
    {
        // Act
        var projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(projects);
        Assert.True(projects.Count >= 2);
        Assert.All(projects, p => Assert.Equal(SampleDataService.TEST_ORG_ID, p.OrganizationId));
    }

    [Fact]
    public async Task GetAllAsync_WithLimitParameter_RespectsLimit()
    {
        // Act
        var projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("limit", "1")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(projects);
        Assert.Single(projects);
    }

    [Fact]
    public async Task GetAsync_ExistingProject_MapsToViewProjectWithSlackIntegration()
    {
        // Act - Test Mapping: Project -> ViewProject (with AfterMap for HasSlackIntegration)
        var viewProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .StatusCodeShouldBeOk()
        );

        // Assert - seeded project has no Slack token, so the computed mapping must be false.
        Assert.NotNull(viewProject);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, viewProject.Id);
        Assert.False(viewProject.HasSlackIntegration);
    }

    [Fact]
    public async Task GetByOrganizationAsync_ValidOrganization_ReturnsProjectsForOrg()
    {
        // Act
        var projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "projects")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(projects);
        Assert.True(projects.Count >= 2);
        Assert.All(projects, p => Assert.Equal(SampleDataService.TEST_ORG_ID, p.OrganizationId));
    }

    [Fact]
    public Task GetByOrganizationAsync_InvalidOrganization_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", "000000000000000000000000", "projects")
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task GetConfigAsync_ByProjectId_ReturnsConfig()
    {
        // Act
        var config = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(config);
        Assert.True(config.Version >= 0);
        Assert.NotNull(config.Settings);
    }

    [Fact]
    public async Task GetConfigAsync_WithClientAuth_ReturnsConfigurationWithSettings()
    {
        // Act
        var response = await SendRequestAsync(r => r
           .AsFreeOrganizationClientUser()
           .AppendPath("projects/config")
           .StatusCodeShouldBeOk()
        );

        // Assert - response headers
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.True(response.Content.Headers.ContentLength.HasValue);
        Assert.True(response.Content.Headers.ContentLength > 0);

        // Assert - deserialized model
        var config = await response.DeserializeAsync<ClientConfiguration>();
        Assert.NotNull(config);
        Assert.True(config.Settings.GetBoolean("IncludeConditionalData"));
        Assert.Equal(0, config.Version);

        // Assert - raw JSON uses snake_case and correct structure
        string json = await response.Content.ReadAsStringAsync(TestCancellationToken);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("version", out var versionProp), "Expected snake_case property 'version' in JSON");
        Assert.Equal(0, versionProp.GetInt32());
        Assert.True(root.TryGetProperty("settings", out var settingsProp), "Expected snake_case property 'settings' in JSON");
        Assert.Equal(JsonValueKind.Object, settingsProp.ValueKind);
        Assert.True(settingsProp.TryGetProperty("IncludeConditionalData", out var settingValue), "Expected 'IncludeConditionalData' key in settings");
        Assert.Equal("true", settingValue.GetString());
    }

    [Fact]
    public async Task GetConfigAsync_WithCurrentVersion_ReturnsNotModified()
    {
        // Arrange - get the current config version
        var config = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsFreeOrganizationClientUser()
            .AppendPath("projects/config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(config);

        // Act - request with the current version
        var response = await SendRequestAsync(r => r
            .AsFreeOrganizationClientUser()
            .AppendPath("projects/config")
            .QueryString("v", config.Version.ToString())
            .ExpectedStatus(HttpStatusCode.NotModified)
        );

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task GetConfigAsync_WithStaleVersion_ReturnsUpdatedConfig()
    {
        // Arrange - get initial config
        var config = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsFreeOrganizationClientUser()
            .AppendPath("projects/config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(config);
        int initialVersion = config.Version;

        // Increment the version by setting a new config value
        await SendRequestAsync(r => r
            .AsFreeOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.FREE_PROJECT_ID, "config")
            .QueryString("key", "StaleVersionTest")
            .Content(new ValueFromBody<string>("StaleValue"))
            .StatusCodeShouldBeOk()
        );

        // Act - request with the old (stale) version
        var updatedConfig = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsFreeOrganizationClientUser()
            .AppendPath("projects/config")
            .QueryString("v", initialVersion.ToString())
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedConfig);
        Assert.True(updatedConfig.Version > initialVersion);
        Assert.Equal("StaleValue", updatedConfig.Settings.GetString("StaleVersionTest"));
    }

    [Fact]
    public async Task SetConfigAsync_WithValidKeyAndValue_PersistsAndIncrementsVersion()
    {
        // Arrange - get initial config
        var initialConfig = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(initialConfig);

        // Act - set a new config value
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", "MyNewSetting")
            .Content(new ValueFromBody<string>("MyNewValue"))
            .StatusCodeShouldBeOk()
        );

        // Assert - verify the setting was persisted
        var updatedConfig = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updatedConfig);
        Assert.Equal("MyNewValue", updatedConfig.Settings.GetString("MyNewSetting"));
        Assert.Equal(initialConfig.Version + 1, updatedConfig.Version);
    }

    [Fact]
    public async Task SetConfigAsync_WithEmptyKey_ReturnsBadRequest()
    {
        // Arrange - get initial config version
        var configBefore = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(configBefore);

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", String.Empty)
            .Content(new ValueFromBody<string>("SomeValue"))
            .StatusCodeShouldBeBadRequest()
        );

        // Assert - version should not change
        var configAfter = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(configAfter);
        Assert.Equal(configBefore.Version, configAfter.Version);
    }

    [Fact]
    public async Task SetConfigAsync_WithEmptyValue_ReturnsBadRequest()
    {
        // Arrange - get initial config version
        var configBefore = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(configBefore);

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", "TestKey")
            .Content(new ValueFromBody<string>(String.Empty))
            .StatusCodeShouldBeBadRequest()
        );

        // Assert - version should not change
        var configAfter = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(configAfter);
        Assert.Equal(configBefore.Version, configAfter.Version);
    }

    [Fact]
    public async Task SetConfigAsync_WithOmittedKey_ReturnsBadRequest()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", projectId, "config")
            .Content(new ValueFromBody<string>("SomeValue"))
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetConfigAsync_RoundTrip_JsonSerializesCorrectly()
    {
        // Arrange - set a config value
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", "SerializationTest")
            .Content(new ValueFromBody<string>("TestValue123"))
            .StatusCodeShouldBeOk()
        );

        // Act - get raw JSON from the API
        var response = await SendRequestAsync(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );

        string json = await response.Content.ReadAsStringAsync(TestCancellationToken);

        // Assert - validate JSON structure matches client expectations
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Should have snake_case property names
        Assert.True(root.TryGetProperty("version", out _), "Expected 'version' property (snake_case)");
        Assert.False(root.TryGetProperty("Version", out _), "Should not have PascalCase 'Version' property");
        Assert.True(root.TryGetProperty("settings", out var settings), "Expected 'settings' property (snake_case)");
        Assert.False(root.TryGetProperty("Settings", out _), "Should not have PascalCase 'Settings' property");

        // Settings should be a flat dictionary, not a wrapped object
        Assert.Equal(JsonValueKind.Object, settings.ValueKind);
        Assert.True(settings.TryGetProperty("SerializationTest", out var testValue));
        Assert.Equal("TestValue123", testValue.GetString());

        // Settings keys should preserve original casing (not be snake_cased)
        Assert.True(settings.TryGetProperty("IncludeConditionalData", out _),
            "Settings dictionary keys should preserve original casing");
    }

    [Fact]
    public async Task DeleteConfigAsync_WithExistingKey_RemovesSettingAndIncrementsVersion()
    {
        // Arrange - add a config setting first
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", "ToBeDeleted")
            .Content(new ValueFromBody<string>("DeleteMe"))
            .StatusCodeShouldBeOk()
        );

        var configBeforeDelete = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(configBeforeDelete);
        Assert.Equal("DeleteMe", configBeforeDelete.Settings.GetString("ToBeDeleted"));

        // Act - delete the config setting
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", "ToBeDeleted")
            .StatusCodeShouldBeOk()
        );

        // Assert - verify the setting was removed and version incremented
        var configAfterDelete = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(configAfterDelete);
        Assert.Null(configAfterDelete.Settings.GetString("ToBeDeleted", null));
        Assert.Equal(configBeforeDelete.Version + 1, configAfterDelete.Version);
    }

    [Fact]
    public async Task DeleteConfigAsync_WithNonExistentKey_ReturnsOkWithoutVersionChange()
    {
        // Arrange - get current config version
        var configBefore = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );
        Assert.NotNull(configBefore);

        // Act - delete a key that doesn't exist
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .QueryString("key", "NonExistentKey12345")
            .StatusCodeShouldBeOk()
        );

        // Assert - version should not change
        var configAfter = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsTestOrganizationClientUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "config")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(configAfter);
        Assert.Equal(configBefore.Version, configAfter.Version);
    }

    [Fact]
    public async Task DeleteConfigAsync_WithOmittedKey_ReturnsBadRequest()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", projectId, "config")
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetIntegrationNotificationSettingsAsync_WithSlackIntegration_ReturnsSettings()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.NotificationSettings[Project.NotificationIntegrations.Slack] = new NotificationSettings { ReportNewErrors = true };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        var settings = await SendRequestAsAsync<NotificationSettings>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, Project.NotificationIntegrations.Slack, "notifications")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.ReportNewErrors);
    }

    [Fact]
    public async Task GetNotificationSettingsAsync_AsGlobalAdmin_ReturnsAllSettings()
    {
        // Arrange - set notification settings for the user first
        var settings = new NotificationSettings
        {
            SendDailySummary = true,
            ReportNewErrors = true,
            ReportCriticalErrors = false,
            ReportEventRegressions = true,
            ReportNewEvents = false,
            ReportCriticalEvents = true
        };

        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Post()
            .AppendPaths("users", TestConstants.UserId, "projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .Content(settings)
            .StatusCodeShouldBeOk()
        );

        // Act
        var allSettings = await SendRequestAsAsync<Dictionary<string, NotificationSettings>>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(allSettings);
        Assert.True(allSettings.Count > 0);
    }

    [Fact]
    public async Task GetNotificationSettingsAsync_AsUser_ReturnsUserSettings()
    {
        // Arrange
        var settings = new NotificationSettings
        {
            SendDailySummary = true,
            ReportNewErrors = true
        };

        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Post()
            .AppendPaths("users", TestConstants.UserId, "projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .Content(settings)
            .StatusCodeShouldBeOk()
        );

        // Act
        var userSettings = await SendRequestAsAsync<NotificationSettings>(r => r
            .AsGlobalAdminUser()
            .AppendPaths("users", TestConstants.UserId, "projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(userSettings);
        Assert.True(userSettings.SendDailySummary);
        Assert.True(userSettings.ReportNewErrors);
    }

    [Fact]
    public async Task GetV1ConfigAsync_WithClientAuth_ReturnsConfig()
    {
        // Act
        var config = await SendRequestAsAsync<ClientConfiguration>(r => r
            .BaseUri(_server.BaseAddress)
            .AsFreeOrganizationClientUser()
            .AppendPaths("api", "v1", "project", "config")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(config);
        Assert.True(config.Version >= 0);
        Assert.NotNull(config.Settings);
    }

    [Fact]
    public async Task GetV2ConfigAsync_WithClientAuth_ReturnsConfig()
    {
        // Act
        var config = await SendRequestAsAsync<ClientConfiguration>(r => r
            .AsFreeOrganizationClientUser()
            .AppendPath("projects/config")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(config);
        Assert.True(config.Version >= 0);
        Assert.NotNull(config.Settings);
    }

    [Fact]
    public async Task IsNameAvailableAsync_ExistingName_ReturnsCreated()
    {
        // Arrange - the TEST_PROJECT_ID project should have a name
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);

        // Act - 201 Created means name is already taken (NOT available)
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects/check-name")
            .QueryString("name", project.Name)
            .StatusCodeShouldBeCreated()
        );
    }

    [Fact]
    public Task IsNameAvailableAsync_NewName_ReturnsNoContent()
    {
        // Act - 204 NoContent means name IS available
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects/check-name")
            .QueryString("name", "UniqueProjectName_" + Guid.NewGuid().ToString("N"))
            .StatusCodeShouldBeNoContent()
        );
    }

    [Fact]
    public async Task IsNameAvailableAsync_OmittedName_ReturnsCreated()
    {
        // Arrange
        string path = "projects/check-name";

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPath(path)
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task IsNameAvailableAsync_OmittedNameWithOrganizationScope_ReturnsCreated()
    {
        // Arrange
        string organizationId = SampleDataService.TEST_ORG_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", organizationId, "projects", "check-name")
            .StatusCodeShouldBeCreated()
        );

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public Task IsNameAvailableAsync_ScopedToOrganization_ReturnsNoContent()
    {
        // Act - 204 NoContent means name IS available in this org scope
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .AppendPaths("organizations", SampleDataService.TEST_ORG_ID, "projects", "check-name")
            .QueryString("name", "UniqueOrgScoped_" + Guid.NewGuid().ToString("N"))
            .StatusCodeShouldBeNoContent()
        );
    }

    [Fact]
    public async Task PatchAsync_WithNameOnlySnakeCasePayload_UpdatesNameAndPreservesDeleteBotSetting()
    {
        // Arrange
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Original Name",
                DeleteBotDataEnabled = true
            })
            .StatusCodeShouldBeCreated()
        );
        Assert.NotNull(project);

        /* language=json */
        const string json = """
                            {
                                "name": "Updated Name"
                            }
                            """;

        // Act
        var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(json, "application/json")
            .StatusCodeShouldBeOk()
        );
        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        var updatedProject = await DeserializeResponseAsync<ViewProject>(response);

        // Assert
        Assert.NotNull(updatedProject);
        Assert.Equal("Updated Name", updatedProject.Name);
        Assert.True(updatedProject.DeleteBotDataEnabled);
        Assert.Equal(project.OrganizationId, updatedProject.OrganizationId);

        var persisted = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Updated Name", persisted.Name);
        Assert.True(persisted.DeleteBotDataEnabled);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("delete_bot_data_enabled", out var deleteBotDataEnabled), "Expected lower_case_underscore response property 'delete_bot_data_enabled'.");
        Assert.True(deleteBotDataEnabled.GetBoolean());
        Assert.False(root.TryGetProperty("DeleteBotDataEnabled", out _), "Response must not drift back to PascalCase 'DeleteBotDataEnabled'.");
    }

    [Fact]
    public async Task PatchAsync_WithNullAndWhitespacePromotedTabs_RemovesInvalidTabs()
    {
        // Arrange
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Nullable Promoted Tabs Project",
                DeleteBotDataEnabled = false
            })
            .StatusCodeShouldBeCreated()
        );
        Assert.NotNull(project);

        /* language=json */
        const string json = """
                            {
                                "promoted_tabs": [null, "", "   ", "  timeline  "]
                            }
                            """;

        // Act
        var updatedProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(json, "application/json")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedProject);
        Assert.Equal(["timeline"], updatedProject.PromotedTabs);

        var persisted = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["timeline"], persisted.PromotedTabs);
    }

    [Fact]
    public async Task PatchAsync_WithPromotedTabs_PreservesOrderAndRemovesDuplicates()
    {
        // Arrange
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Promoted Tabs Project",
                DeleteBotDataEnabled = false
            })
            .StatusCodeShouldBeCreated()
        );
        Assert.NotNull(project);

        /* language=json */
        const string json = """
                            {
                                "promoted_tabs": ["gamma", "alpha", "gamma", "  beta  ", ""]
                            }
                            """;

        // Act
        var updatedProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(json, "application/json")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(updatedProject);
        Assert.Equal(["gamma", "alpha", "beta"], updatedProject.PromotedTabs);

        var persisted = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(persisted);
        Assert.Equal(["gamma", "alpha", "beta"], persisted.PromotedTabs);
    }

    [Fact]
    public async Task PatchAsync_NonExistentProject_ReturnsNotFound()
    {
        // Arrange - record a known project's state to verify it wasn't changed
        var beforeProject = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(beforeProject);

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", "000000000000000000000000")
            .Content(new UpdateProject
            {
                Name = "Should Not Exist",
                DeleteBotDataEnabled = false
            })
            .StatusCodeShouldBeNotFound()
        );

        // Assert - existing project was not affected
        var afterProject = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(afterProject);
        Assert.Equal(beforeProject.Name, afterProject.Name);
    }

    [Fact]
    public async Task PatchAsync_WithExtraPayloadProperties_IgnoresReadOnlyFieldsAndUpdatesKnownFields()
    {
        // Arrange
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Extra Props Project",
                DeleteBotDataEnabled = true
            })
            .StatusCodeShouldBeCreated()
        );
        Assert.NotNull(project);

        var persistedBefore = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(persistedBefore);

        /* language=json */
        string json = $$"""
                        {
                            "id": "000000000000000000000000",
                            "organization_id": "{{SampleDataService.FREE_ORG_ID}}",
                            "organization_name": "Hijacked Org",
                            "created_utc": "2000-01-01T00:00:00Z",
                            "name": "Patched With Extras",
                            "delete_bot_data_enabled": false,
                            "has_premium_features": true,
                            "stack_count": 9999
                        }
                        """;

        // Act
        var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(json, "application/json")
            .StatusCodeShouldBeOk()
        );
        string responseJson = await response.Content.ReadAsStringAsync(TestCancellationToken);
        var updatedProject = await DeserializeResponseAsync<ViewProject>(response);

        // Assert
        Assert.NotNull(updatedProject);
        Assert.Equal("Patched With Extras", updatedProject.Name);
        Assert.False(updatedProject.DeleteBotDataEnabled);
        Assert.Equal(SampleDataService.TEST_ORG_ID, updatedProject.OrganizationId);
        Assert.Equal(persistedBefore.CreatedUtc, updatedProject.CreatedUtc);

        var persistedAfter = await _projectRepository.GetByIdAsync(project.Id);
        Assert.NotNull(persistedAfter);
        Assert.Equal("Patched With Extras", persistedAfter.Name);
        Assert.False(persistedAfter.DeleteBotDataEnabled);
        Assert.Equal(SampleDataService.TEST_ORG_ID, persistedAfter.OrganizationId);
        Assert.Equal(persistedBefore.CreatedUtc, persistedAfter.CreatedUtc);

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("organization_id", out var organizationId), "Expected lower_case_underscore response property 'organization_id'.");
        Assert.Equal(SampleDataService.TEST_ORG_ID, organizationId.GetString());
        Assert.True(root.TryGetProperty("delete_bot_data_enabled", out var deleteBotDataEnabled), "Expected lower_case_underscore response property 'delete_bot_data_enabled'.");
        Assert.False(deleteBotDataEnabled.GetBoolean());
        Assert.False(root.TryGetProperty("OrganizationId", out _), "Response must not drift back to PascalCase 'OrganizationId'.");
        Assert.False(root.TryGetProperty("DeleteBotDataEnabled", out _), "Response must not drift back to PascalCase 'DeleteBotDataEnabled'.");
    }

    [Fact]
    public async Task PostAsync_NewProject_MapsAllPropertiesToProject()
    {
        // Arrange - Test Mapping: NewProject -> Project
        var newProject = new NewProject
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Mapped Test Project",
            DeleteBotDataEnabled = true
        };

        // Act
        var viewProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(newProject)
            .StatusCodeShouldBeCreated()
        );

        // Assert - Verify mapping worked correctly
        Assert.NotNull(viewProject);
        Assert.NotNull(viewProject.Id);
        Assert.Equal("Mapped Test Project", viewProject.Name);
        Assert.Equal(SampleDataService.TEST_ORG_ID, viewProject.OrganizationId);
        Assert.True(viewProject.DeleteBotDataEnabled);
        Assert.True(viewProject.CreatedUtc > DateTime.MinValue);

        // Verify persisted entity
        var project = await _projectRepository.GetByIdAsync(viewProject.Id);
        Assert.NotNull(project);
        Assert.Equal("Mapped Test Project", project.Name);
        Assert.True(project.DeleteBotDataEnabled);
    }

    [Fact]
    public async Task PostAsync_NewProject_ReturnsAbsoluteLocation()
    {
        // Arrange
        var project = new NewProject
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Absolute Location Project",
            DeleteBotDataEnabled = false
        };

        // Act
        var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(project)
            .StatusCodeShouldBeCreated());

        // Assert
        Assert.NotNull(response.Headers.Location);
        Assert.True(response.Headers.Location.IsAbsoluteUri);
        Assert.Equal("localhost", response.Headers.Location.Host);
    }

    [Fact]
    public async Task PostAsync_WithPromotedTabs_NormalizesTabs()
    {
        // Arrange
        var project = new NewProject
        {
            OrganizationId = SampleDataService.TEST_ORG_ID,
            Name = "Normalized Tabs Project",
            DeleteBotDataEnabled = false,
            PromotedTabs = ["gamma", "alpha", "gamma", "  beta  ", ""]
        };

        // Act
        var createdProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(project)
            .StatusCodeShouldBeCreated());

        // Assert
        Assert.NotNull(createdProject);
        Assert.Equal(["gamma", "alpha", "beta"], createdProject.PromotedTabs);

        var persistedProject = await _projectRepository.GetByIdAsync(createdProject.Id);
        Assert.NotNull(persistedProject);
        Assert.Equal(["gamma", "alpha", "beta"], persistedProject.PromotedTabs);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    public async Task PromoteTabAsync_WithNonJsonBody_ReturnsUnsupportedMediaType(string method)
    {
        // Arrange
        var httpMethod = new HttpMethod(method);

        // Act
        using var response = await SendRequestAsync(r => r
            .Method(httpMethod)
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "promotedtabs")
            .QueryString("name", "regressions")
            .Content("ignored", "text/plain")
            .ExpectedStatus(HttpStatusCode.UnsupportedMediaType));

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact]
    public async Task PromoteTabAsync_WithOmittedName_ReturnsBadRequest()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", projectId, "promotedtabs")
            .Content(new { })
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PromoteTabAsync_WithValidName_AddsPromotedTab()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.PromotedTabs = [];
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency());

        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "promotedtabs")
            .QueryString("name", "regressions")
            .Content(new { })
            .StatusCodeShouldBeOk()
        );

        // Assert
        var updatedProject = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(updatedProject);
        Assert.Contains("regressions", updatedProject.PromotedTabs ?? []);
    }

    [Fact]
    public async Task PostDataAsync_ValidKeyAndValue_PersistsData()
    {
        // Act
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "data")
            .QueryString("key", "TestDataKey")
            .Content(new ValueFromBody<string>("TestDataValue"))
            .StatusCodeShouldBeOk()
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.NotNull(project.Data);
        Assert.True(project.Data.TryGetValue("TestDataKey", out object? dataValue));
        Assert.Equal("TestDataValue", dataValue);
    }

    [Fact]
    public Task PostDataAsync_EmptyKey_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "data")
            .QueryString("key", String.Empty)
            .Content(new ValueFromBody<string>("SomeValue"))
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public Task PostDataAsync_KeyStartsWithDash_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "data")
            .QueryString("key", "-invalid")
            .Content(new ValueFromBody<string>("SomeValue"))
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public Task PostDataAsync_NonExistentProject_ReturnsNotFound()
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", "000000000000000000000000", "data")
            .QueryString("key", "SomeKey")
            .Content(new ValueFromBody<string>("SomeValue"))
            .StatusCodeShouldBeNotFound()
        );
    }

    [Fact]
    public async Task PostDataAsync_OmittedKey_ReturnsBadRequest()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", projectId, "data")
            .Content(new ValueFromBody<string>("SomeValue"))
            .StatusCodeShouldBeBadRequest()
        );

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RemoveSlackAsync_WithoutSlackToken_ReturnsOk()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act
        await SendRequestAsync(r => r
            .Delete()
            .AsTestOrganizationUser()
            .AppendPaths("projects", projectId, "slack")
            .StatusCodeShouldBeOk()
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(projectId);
        Assert.NotNull(project);
        Assert.False(project.Data?.ContainsKey(Project.KnownDataKeys.SlackToken) ?? false);
    }

    [Fact]
    public async Task ResetDataAsync_ValidProject_ClearsStacksAndEvents()
    {
        // Arrange
        await CreateDataAsync(d =>
        {
            d.Event().Message("test for reset");
        });

        long stacksBefore = await _stackRepository.CountAsync(q => q.Project(SampleDataService.TEST_PROJECT_ID));
        long eventsBefore = await _eventRepository.CountAsync(q => q.Project(SampleDataService.TEST_PROJECT_ID));
        Assert.True(stacksBefore > 0);
        Assert.True(eventsBefore > 0);

        // Act
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, "reset-data")
            .StatusCodeShouldBeAccepted()
        );

        Assert.NotNull(workItems);
        Assert.Single(workItems.Workers);

        var workItemJob = GetService<WorkItemJob>();
        await workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        // Assert
        long stacksAfter = await _stackRepository.CountAsync(q => q.Project(SampleDataService.TEST_PROJECT_ID));
        long eventsAfter = await _eventRepository.CountAsync(q => q.Project(SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(0, stacksAfter);
        Assert.Equal(0, eventsAfter);
    }

    [Fact]
    public async Task SetNotificationSettingsAsync_ValidSettings_PersistsSettings()
    {
        // Arrange
        var settings = new NotificationSettings
        {
            SendDailySummary = true,
            ReportNewErrors = true,
            ReportCriticalErrors = true,
            ReportEventRegressions = false,
            ReportNewEvents = false,
            ReportCriticalEvents = true
        };

        // Act
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Post()
            .AppendPaths("users", TestConstants.UserId, "projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .Content(settings)
            .StatusCodeShouldBeOk()
        );

        // Assert - read back from repository
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.True(project.NotificationSettings.TryGetValue(TestConstants.UserId, out var saved));
        Assert.NotNull(saved);
        Assert.True(saved.SendDailySummary);
        Assert.True(saved.ReportNewErrors);
        Assert.True(saved.ReportCriticalErrors);
        Assert.False(saved.ReportEventRegressions);
        Assert.False(saved.ReportNewEvents);
        Assert.True(saved.ReportCriticalEvents);
    }

    [Fact]
    public async Task SetNotificationSettingsAsync_NullSettings_RemovesSettings()
    {
        // Arrange - set some settings first
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Post()
            .AppendPaths("users", TestConstants.UserId, "projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .Content(new NotificationSettings { ReportNewErrors = true })
            .StatusCodeShouldBeOk()
        );

        // Verify they exist
        var projectBefore = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(projectBefore);
        Assert.True(projectBefore.NotificationSettings.ContainsKey(TestConstants.UserId));

        // Act - send null to remove
        await SendRequestAsync(r => r
            .AsGlobalAdminUser()
            .Delete()
            .AppendPaths("users", TestConstants.UserId, "projects", SampleDataService.TEST_PROJECT_ID, "notifications")
            .StatusCodeShouldBeOk()
        );

        // Assert
        var projectAfter = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(projectAfter);
        Assert.False(projectAfter.NotificationSettings.ContainsKey(TestConstants.UserId));
    }

    [Fact]
    public async Task SetIntegrationNotificationSettingsAsync_WithSlackIntegration_PersistsSettings()
    {
        // Arrange
        var settings = new NotificationSettings
        {
            ReportNewErrors = true,
            ReportCriticalErrors = true
        };

        // Act
        await SendRequestAsync(r => r
            .Post()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, Project.NotificationIntegrations.Slack, "notifications")
            .Content(settings)
            .StatusCodeShouldBeOk()
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.True(project.NotificationSettings.TryGetValue(Project.NotificationIntegrations.Slack, out var savedSettings));
        Assert.True(savedSettings.ReportNewErrors);
        Assert.True(savedSettings.ReportCriticalErrors);
    }

    [Fact]
    public async Task CanGetProjectListStats()
    {
        var projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(projects);
        Assert.Equal(2, projects.Count);
        var project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(0, project.StackCount);
        Assert.Equal(0, project.EventCount);

        var (stacks, events) = await CreateDataAsync(d =>
        {
            d.Event().Message("test");
        });

        projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(projects);
        project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(stacks.Count, project.StackCount);
        Assert.Equal(events.Count, project.EventCount);

        // Reset Project data and ensure counts are removed immediately after the work item runs.
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", project.Id, "reset-data")
            .StatusCodeShouldBeAccepted()
        );

        Assert.NotNull(workItems);
        Assert.Single(workItems.Workers);
        var workItemJob = GetService<WorkItemJob>();
        await workItemJob.RunUntilEmptyAsync(TestCancellationToken);
        await RefreshDataAsync();

        projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(projects);
        project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(0, project.StackCount);
        Assert.Equal(0, project.EventCount);

        long stackCount = await _stackRepository.CountAsync(q => q.Project(project.Id));
        long eventCount = await _eventRepository.CountAsync(q => q.Project(project.Id));
        Assert.Equal(0, stackCount);
        Assert.Equal(0, eventCount);

        var legacyGetWorkItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", project.Id, "reset-data")
            .StatusCodeShouldBeAccepted()
        );

        Assert.NotNull(legacyGetWorkItems);
        Assert.Single(legacyGetWorkItems.Workers);

        var (newStacks, newEvents) = await CreateDataAsync(d =>
        {
            d.Event().Message("test after reset");
        });
        await RefreshDataAsync();

        Assert.NotEmpty(newStacks);
        Assert.NotEmpty(newEvents);

        projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(projects);
        project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(newStacks.Count, project.StackCount);
        Assert.Equal(newEvents.Count, project.EventCount);
    }

    [Fact]
    public async Task PatchAsync_WithFixedIngestLimit_SavesCorrectly()
    {
        var updated = await SendRequestAsAsync<ViewProject>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content("""{"ingest_limit":{"type":0,"fixed_limit":500}}""", "application/json")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.NotNull(updated.IngestLimit);
        Assert.Equal(ProjectIngestLimitType.Fixed, updated.IngestLimit.Type);
        Assert.Equal(500, updated.IngestLimit.FixedLimit);

        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.NotNull(project.IngestLimit);
        Assert.Equal(ProjectIngestLimitType.Fixed, project.IngestLimit.Type);
        Assert.Equal(500, project.IngestLimit.FixedLimit);
    }

    [Fact]
    public async Task PatchAsync_WithPercentageIngestLimit_SavesCorrectly()
    {
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.MaxEventsPerMonth = 1000;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        var updated = await SendRequestAsAsync<ViewProject>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content("""{"ingest_limit":{"type":1,"percent_of_organization_limit":75.0}}""", "application/json")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.NotNull(updated.IngestLimit);
        Assert.Equal(ProjectIngestLimitType.PercentOfOrganizationLimit, updated.IngestLimit.Type);
        Assert.Equal(75.0m, updated.IngestLimit.PercentOfOrganizationLimit);

        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        Assert.NotNull(project.IngestLimit);
        Assert.Equal(75.0m, project.IngestLimit.PercentOfOrganizationLimit);
    }

    [Fact]
    public async Task PatchAsync_RemoveIngestLimit_SavesNull()
    {
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(project);
        project.IngestLimit = new ProjectIngestLimit { Type = ProjectIngestLimitType.Fixed, FixedLimit = 100 };
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency().Cache());

        var updated = await SendRequestAsAsync<ViewProject>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content("""{"ingest_limit":null}""", "application/json")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.Null(updated.IngestLimit);

        var saved = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(saved);
        Assert.Null(saved.IngestLimit);
    }

    [Fact]
    public async Task PatchAsync_OnlyIngestLimit_NoNameValidationError()
    {
        var original = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.NotNull(original);

        var updated = await SendRequestAsAsync<ViewProject>(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content("""{"ingest_limit":{"type":0,"fixed_limit":200}}""", "application/json")
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updated);
        Assert.Equal(original.Name, updated.Name);
        Assert.NotNull(updated.IngestLimit);
        Assert.Equal(200, updated.IngestLimit.FixedLimit);
    }

    [Theory]
    [InlineData("{\"ingest_limit\":{\"type\":0,\"fixed_limit\":0}}")]
    [InlineData("{\"ingest_limit\":{\"type\":1,\"percent_of_organization_limit\":0}}")]
    [InlineData("{\"ingest_limit\":{\"type\":1,\"percent_of_organization_limit\":100.1}}")]
    public async Task PatchAsync_InvalidIngestLimit_ReturnsValidationError(string content)
    {
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.MaxEventsPerMonth = 1000;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content(content, "application/json")
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public Task PatchAsync_FractionalFixedIngestLimit_ReturnsBadRequest()
    {
        return SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content("""{"ingest_limit":{"type":0,"fixed_limit":1.5}}""", "application/json")
            .StatusCodeShouldBeBadRequest()
        );
    }

    [Fact]
    public async Task PatchAsync_PercentageIngestLimitForUnlimitedOrganization_ReturnsValidationError()
    {
        var organization = await _organizationRepository.GetByIdAsync(SampleDataService.TEST_ORG_ID);
        Assert.NotNull(organization);
        organization.MaxEventsPerMonth = -1;
        await _organizationRepository.SaveAsync(organization, o => o.ImmediateConsistency().Cache());

        await SendRequestAsync(r => r
            .Patch()
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .Content("""{"ingest_limit":{"type":1,"percent_of_organization_limit":50}}""", "application/json")
            .StatusCodeShouldBeBadRequest()
        );
    }
}
