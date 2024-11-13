using Exceptionless.Core.Jobs;
using Exceptionless.Core.Models;
using Exceptionless.Core.Utility;
using Exceptionless.Tests.Extensions;
using Exceptionless.Web.Controllers;
using Exceptionless.Web.Models;
using FluentRest;
using Foundatio.Jobs;
using Xunit;
using Xunit.Abstractions;

namespace Exceptionless.Tests.Controllers;

public sealed class ProjectControllerTests : IntegrationTestsBase
{
    public ProjectControllerTests(ITestOutputHelper output, AspireWebHostFactory factory) : base(output, factory)
    {
    }

    protected override async Task ResetDataAsync()
    {
        await base.ResetDataAsync();
        var service = GetService<SampleDataService>();
        await service.CreateDataAsync();
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

        project.Name = "Updated";
        var updatedProject = await SendRequestAsAsync<ViewProject>(r => r
            .AsTestOrganizationUser()
            .Patch()
            .AppendPaths("projects", project.Id)
            .Content(project)
            .StatusCodeShouldBeOk()
        );

        Assert.Equal("Updated", updatedProject.Name);
    }

    [Fact]
    public async Task CanGetProjectConfiguration()
    {
        var response = await SendRequestAsync(r => r
           .AsFreeOrganizationClientUser()
           .AppendPath("projects/config")
           .StatusCodeShouldBeOk()
        );

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.True(response.Content.Headers.ContentLength.HasValue);
        Assert.True(response.Content.Headers.ContentLength > 0);

        var config = await response.DeserializeAsync<ClientConfiguration>();
        Assert.True(config.Settings.GetBoolean("IncludeConditionalData"));
        Assert.Equal(0, config.Version);
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

        Assert.Equal(2, projects.Count);
        var project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(0, project.StackCount);
        Assert.Equal(0, project.EventCount);

        (var stacks, var events) = await CreateDataAsync(d =>
        {
            d.Event().Message("test");
        });

        projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(stacks.Count, project.StackCount);
        Assert.Equal(events.Count, project.EventCount);

        // Reset Project data and ensure soft deleted counts don't show up
        var workItems = await SendRequestAsAsync<WorkInProgressResult>(r => r
            .AsTestOrganizationUser()
            .AppendPaths("projects", project.Id, "reset-data")
            .StatusCodeShouldBeAccepted()
        );

        Assert.Single(workItems.Workers);
        var workItemJob = GetService<WorkItemJob>();
        await workItemJob.RunUntilEmptyAsync();
        await RefreshDataAsync();

        projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        // Stacks and event counts include soft deleted (performance reasons)
        Assert.Equal(stacks.Count, project.StackCount);
        Assert.Equal(events.Count, project.EventCount);

        var cleanupJob = GetService<CleanupDataJob>();
        await cleanupJob.RunAsync();

        projects = await SendRequestAsAsync<List<ViewProject>>(r => r
            .AsTestOrganizationUser()
            .AppendPath("projects")
            .QueryString("mode", "stats")
            .StatusCodeShouldBeOk()
        );

        project = projects.Single(p => String.Equals(p.Id, SampleDataService.TEST_PROJECT_ID));
        Assert.Equal(0, project.StackCount);
        Assert.Equal(0, project.EventCount);
    }
}
