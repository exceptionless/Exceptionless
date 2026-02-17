using System.Net;
using System.Text.Json;
using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Repositories;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Jobs;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Controllers;

public sealed class ProjectControllerTests : IntegrationTestsBase
{
    private readonly IProjectRepository _projectRepository;

    public ProjectControllerTests(ITestOutputHelper output, AppWebHostFactory factory) : base(output, factory)
    {
        _projectRepository = GetService<IProjectRepository>();
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
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
    public async Task GetAsync_ExistingProject_MapsToViewProjectWithSlackIntegration()
    {
        // Act - Test Mapping: Project -> ViewProject (with AfterMap for HasSlackIntegration)
        var viewProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .StatusCodeShouldBeOk()
        );

        // Assert - ViewProject should include computed HasSlackIntegration property
        Assert.NotNull(viewProject);
        Assert.Equal(SampleDataService.TEST_PROJECT_ID, viewProject.Id);
        Assert.IsType<bool>(viewProject.HasSlackIntegration);
    }

    [Fact]
    public async Task CanUpdateProject()
    {
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Test Project",
                DeleteBotDataEnabled = true
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(project);

        var updatedProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(new UpdateProject
            {
                Name = "Test Project 2",
                DeleteBotDataEnabled = true
            })
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updatedProject);
        Assert.NotEqual(project.Name, updatedProject.Name);
    }


    [Fact]
    public async Task CanUpdateProjectWithExtraPayloadProperties()
    {
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPath("projects")
            .Content(new NewProject
            {
                OrganizationId = SampleDataService.TEST_ORG_ID,
                Name = "Test Project",
                DeleteBotDataEnabled = true
            })
            .StatusCodeShouldBeCreated()
        );

        Assert.NotNull(project);
        project.Name = "Updated";
        var updatedProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(project)
            .StatusCodeShouldBeOk()
        );

        Assert.NotNull(updatedProject);
        Assert.Equal("Updated", updatedProject.Name);
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
            .QueryString("key", "")
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
            .Content(new ValueFromBody<string>(""))
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

        // Reset Project data and ensure soft deleted counts don't show up
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
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
        // Stacks and event counts include soft deleted (performance reasons)
        Assert.Equal(stacks.Count, project.StackCount);
        Assert.Equal(events.Count, project.EventCount);

        var cleanupJob = GetService<CleanupDataJob>();
        await cleanupJob.RunAsync(TestCancellationToken);

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
    }
}
