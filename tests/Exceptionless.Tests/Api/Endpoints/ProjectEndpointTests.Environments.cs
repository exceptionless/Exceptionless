using System.Net;
using Exceptionless.Core.Models;
using Exceptionless.Core.Services;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Tests.Utility;
using Exceptionless.Web.Models;
using Foundatio.Repositories;
using Xunit;

namespace Exceptionless.Tests.Api.Endpoints;

public sealed partial class ProjectEndpointTests
{
    private const string EnvironmentsPath = "environments";

    [Fact]
    public async Task AddEnvironmentAsync_WithPaddedName_ListsTrimmedNameAndExposesItOnTheProject()
    {
        // Arrange
        var body = new NewProjectEnvironment { Name = "  Production  ", Notify = false };

        // Act
        var settings = await SendRequestAsAsync<ProjectEnvironmentSettings>(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .Content(body)
            .StatusCodeShouldBeOk()
        );
        var project = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID)
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(settings);
        var listed = Assert.Single(settings.Environments);
        Assert.Equal("Production", listed.Name);
        Assert.False(listed.Notify);
        Assert.NotEqual(default, listed.CreatedUtc);
        Assert.Equal("Production", Assert.Single(project!.Environments).Name);
        Assert.True(project.NotifyOtherEnvironments);
        Assert.False(project.DiscardUnlistedEnvironments);
        Assert.False(project.DiscardEventsWithoutEnvironment);
    }

    [Fact]
    public async Task AddEnvironmentAsync_WithNameDifferingOnlyByCase_ReturnsConflictAndKeepsTheListedCasing()
    {
        // Arrange
        await AddEnvironmentAsync("Production");

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .Content(new NewProjectEnvironment { Name = "PRODUCTION" })
            .ExpectedStatus(HttpStatusCode.Conflict)
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.Equal(["Production"], project!.Environments.Select(e => e.Name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("prod\tuction")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123456789")]
    public async Task AddEnvironmentAsync_WithInvalidName_ReturnsUnprocessableEntity(string name)
    {
        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .Content(new NewProjectEnvironment { Name = name })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.Empty(project!.Environments);
    }

    [Fact]
    public async Task AddEnvironmentAsync_WhenTheListIsFull_ReturnsUnprocessableEntity()
    {
        // Arrange
        var project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        project!.Environments = Enumerable.Range(0, ProjectEnvironment.MaximumCount).Select(i => new ProjectEnvironment { Name = $"env-{i}" }).ToList();
        await _projectRepository.SaveAsync(project, o => o.ImmediateConsistency().Cache());

        // Act
        using var response = await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .Content(new NewProjectEnvironment { Name = "one-too-many" })
            .StatusCodeShouldBeUnprocessableEntity()
        );

        // Assert
        project = await _projectRepository.GetByIdAsync(SampleDataService.TEST_PROJECT_ID);
        Assert.Equal(ProjectEnvironment.MaximumCount, project!.Environments.Count);
    }

    [Fact]
    public async Task AddEnvironmentAsync_ForDiscardedName_StopsReportingItAsDiscardedAndLeavesEventsAlone()
    {
        // Arrange
        var discarded = GetService<DiscardedEnvironmentService>();
        await discarded.RecordAsync(SampleDataService.TEST_PROJECT_ID, ["Prodution", "dev"]);
        var ev = await AddEventAsync("prodution");

        // Act
        await AddEnvironmentAsync("PRODUTION");
        var result = await SendRequestAsAsync<ProjectDiscardedEnvironments>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath, "discarded")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(DiscardedEnvironmentService.RetentionDays, result.RetentionDays);
        Assert.Equal(["dev"], result.Environments.Select(e => e.Key));
        Assert.Equal("prodution", (await _eventRepository.GetByIdAsync(ev.Id))!.Environment);
    }

    [Fact]
    public async Task UpdateEnvironmentAsync_WithDifferentCasing_TogglesNotifyOnTheListedName()
    {
        // Arrange
        await AddEnvironmentAsync("Staging");

        // Act
        var settings = await SendRequestAsAsync<ProjectEnvironmentSettings>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .QueryString("name", "STAGING")
            .Content(new UpdateProjectEnvironment { Notify = false })
            .StatusCodeShouldBeOk()
        );

        // Assert
        var listed = Assert.Single(settings!.Environments);
        Assert.Equal("Staging", listed.Name);
        Assert.False(listed.Notify);
    }

    [Fact]
    public async Task UpdateEnvironmentAsync_WithUnlistedNameOrMissingNotify_ReturnsNotFoundOrUnprocessableEntity()
    {
        // Arrange
        await AddEnvironmentAsync("Staging");

        // Act & Assert
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .QueryString("name", "unlisted")
            .Content(new UpdateProjectEnvironment { Notify = false })
            .StatusCodeShouldBeNotFound()
        );
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .QueryString("name", "staging")
            .Content(new UpdateProjectEnvironment())
            .StatusCodeShouldBeUnprocessableEntity()
        );
    }

    [Fact]
    public async Task RemoveEnvironmentAsync_WithDifferentCasing_RemovesTheNameButNeverDeletesEvents()
    {
        // Arrange
        await AddEnvironmentAsync("Production");
        await AddEnvironmentAsync("Staging");
        var ev = await AddEventAsync("production");

        // Act
        var settings = await SendRequestAsAsync<ProjectEnvironmentSettings>(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .QueryString("name", "PRODUCTION")
            .StatusCodeShouldBeOk()
        );
        var again = await SendRequestAsAsync<ProjectEnvironmentSettings>(r => r
            .AsTestOrganizationUser()
            .Delete()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .QueryString("name", "PRODUCTION")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.Equal(["Staging"], settings!.Environments.Select(e => e.Name));
        Assert.Equal(["Staging"], again!.Environments.Select(e => e.Name));
        Assert.Equal("production", (await _eventRepository.GetByIdAsync(ev.Id))!.Environment);
    }

    [Fact]
    public async Task UpdateEnvironmentSettingsAsync_WithPartialBody_ChangesOnlyTheProvidedSettings()
    {
        // Arrange
        var discarded = GetService<DiscardedEnvironmentService>();
        await discarded.RecordAsync(SampleDataService.TEST_PROJECT_ID, [null, "dev"]);
        await SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Put()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath, "settings")
            .Content(new UpdateProjectEnvironmentSettings { DiscardUnlistedEnvironments = true, DiscardEventsWithoutEnvironment = true })
            .StatusCodeShouldBeOk()
        );

        // Act
        var settings = await SendRequestAsAsync<ProjectEnvironmentSettings>(r => r
            .AsTestOrganizationUser()
            .Put()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath, "settings")
            .Content(new UpdateProjectEnvironmentSettings { DiscardEventsWithoutEnvironment = false, NotifyOtherEnvironments = false })
            .StatusCodeShouldBeOk()
        );
        var result = await SendRequestAsAsync<ProjectDiscardedEnvironments>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath, "discarded")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(settings);
        Assert.True(settings.DiscardUnlistedEnvironments);
        Assert.False(settings.DiscardEventsWithoutEnvironment);
        Assert.False(settings.NotifyOtherEnvironments);
        Assert.Equal(["dev"], result!.Environments.Select(e => e.Key));
    }

    [Fact]
    public async Task GetDiscardedEnvironmentsAsync_ReturnsCountsLastSeenAndFirstSeenCasing()
    {
        // Arrange
        var discarded = GetService<DiscardedEnvironmentService>();
        await discarded.RecordAsync(SampleDataService.TEST_PROJECT_ID, ["Prodution", "prodution", null]);

        // Act
        var result = await SendRequestAsAsync<ProjectDiscardedEnvironments>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath, "discarded")
            .StatusCodeShouldBeOk()
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Environments.Count);
        var typo = Assert.Single(result.Environments, e => e.Key == "prodution");
        Assert.Equal("Prodution", typo.Name);
        Assert.Equal(2, typo.Count);
        Assert.NotEqual(default, typo.LastSeenUtc);
        var missing = Assert.Single(result.Environments, e => e.Key is null);
        Assert.Equal(1, missing.Count);
    }

    [Fact]
    public async Task EnvironmentEndpoints_ForAnotherOrganizationsProject_ReturnNotFound()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act & Assert
        await SendRequestAsync(r => r.AsFreeOrganizationUser().Post().AppendPaths("projects", projectId, EnvironmentsPath).Content(new NewProjectEnvironment { Name = "x" }).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsFreeOrganizationUser().Patch().AppendPaths("projects", projectId, EnvironmentsPath).QueryString("name", "x").Content(new UpdateProjectEnvironment { Notify = false }).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsFreeOrganizationUser().Delete().AppendPaths("projects", projectId, EnvironmentsPath).QueryString("name", "x").StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsFreeOrganizationUser().Put().AppendPaths("projects", projectId, EnvironmentsPath, "settings").Content(new UpdateProjectEnvironmentSettings { DiscardUnlistedEnvironments = true }).StatusCodeShouldBeNotFound());
        await SendRequestAsync(r => r.AsFreeOrganizationUser().AppendPaths("projects", projectId, EnvironmentsPath, "discarded").StatusCodeShouldBeNotFound());
        var project = await _projectRepository.GetByIdAsync(projectId);
        Assert.Empty(project!.Environments);
        Assert.False(project.DiscardUnlistedEnvironments);
    }

    [Fact]
    public async Task EnvironmentEndpoints_WithClientTokenOrAnonymous_AreRejected()
    {
        // Arrange
        string projectId = SampleDataService.TEST_PROJECT_ID;

        // Act & Assert
        await SendRequestAsync(r => r.AsTestOrganizationClientUser().Post().AppendPaths("projects", projectId, EnvironmentsPath).Content(new NewProjectEnvironment { Name = "x" }).StatusCodeShouldBeForbidden());
        await SendRequestAsync(r => r.AsTestOrganizationClientUser().AppendPaths("projects", projectId, EnvironmentsPath, "discarded").StatusCodeShouldBeForbidden());
        await SendRequestAsync(r => r.AsAnonymousUser().Post().AppendPaths("projects", projectId, EnvironmentsPath).Content(new NewProjectEnvironment { Name = "x" }).StatusCodeShouldBeUnauthorized());
        await SendRequestAsync(r => r.AsAnonymousUser().AppendPaths("projects", projectId, EnvironmentsPath, "discarded").StatusCodeShouldBeUnauthorized());
        var project = await _projectRepository.GetByIdAsync(projectId);
        Assert.Empty(project!.Environments);
    }

    private Task AddEnvironmentAsync(string name)
    {
        return SendRequestAsync(r => r
            .AsTestOrganizationUser()
            .Post()
            .AppendPaths("projects", SampleDataService.TEST_PROJECT_ID, EnvironmentsPath)
            .Content(new NewProjectEnvironment { Name = name })
            .StatusCodeShouldBeOk()
        );
    }

    private Task<PersistentEvent> AddEventAsync(string environment)
    {
        var ev = GetService<EventData>().GenerateEvent(organizationId: SampleDataService.TEST_ORG_ID, projectId: SampleDataService.TEST_PROJECT_ID, stackId: Foundatio.Repositories.Utility.ObjectId.GenerateNewId().ToString(), generateData: false, generateTags: false);
        ev.Environment = environment;
        return _eventRepository.AddAsync(ev, o => o.ImmediateConsistency());
    }
}
