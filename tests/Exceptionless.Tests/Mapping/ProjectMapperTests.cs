using Exceptionless.Core.Models;
using Exceptionless.Web.Mapping;
using Exceptionless.Web.Models;
using Xunit;

namespace Exceptionless.Tests.Mapping;

public sealed class ProjectMapperTests
{
    private readonly ProjectMapper _mapper;

    public ProjectMapperTests()
    {
        _mapper = new ProjectMapper();
    }

    [Fact]
    public void MapToProject_WithValidNewProject_MapsNameAndOrganizationId()
    {
        // Arrange
        var source = new NewProject
        {
            Name = "Test Project",
            OrganizationId = "org123"
        };

        // Act
        var result = _mapper.MapToProject(source);

        // Assert
        Assert.Equal("Test Project", result.Name);
        Assert.Equal("org123", result.OrganizationId);
    }

    [Fact]
    public void MapToViewProject_WithValidProject_MapsAllProperties()
    {
        // Arrange
        var source = new Project
        {
            Id = "proj123",
            Name = "Test Project",
            OrganizationId = "org123",
            DeleteBotDataEnabled = true
        };

        // Act
        var result = _mapper.MapToViewProject(source);

        // Assert
        Assert.Equal("proj123", result.Id);
        Assert.Equal("Test Project", result.Name);
        Assert.Equal("org123", result.OrganizationId);
        Assert.True(result.DeleteBotDataEnabled);
    }

    [Fact]
    public void MapToViewProject_WithSlackToken_SetsHasSlackIntegration()
    {
        // Arrange
        var source = new Project
        {
            Id = "proj123",
            Name = "Project with Slack",
            Data = new DataDictionary { { Project.KnownDataKeys.SlackToken, "test-token" } }
        };

        // Act
        var result = _mapper.MapToViewProject(source);

        // Assert
        Assert.True(result.HasSlackIntegration);
    }

    [Fact]
    public void MapToViewProject_WithoutSlackToken_HasSlackIntegrationIsFalse()
    {
        // Arrange
        var source = new Project
        {
            Id = "proj123",
            Name = "Project without Slack"
        };

        // Act
        var result = _mapper.MapToViewProject(source);

        // Assert
        Assert.False(result.HasSlackIntegration);
    }

    [Fact]
    public void MapToViewProjects_WithMultipleProjects_MapsAll()
    {
        // Arrange
        var projects = new List<Project>
        {
            new() { Id = "proj1", Name = "Project 1" },
            new() { Id = "proj2", Name = "Project 2" }
        };

        // Act
        var result = _mapper.MapToViewProjects(projects);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("proj1", result[0].Id);
        Assert.Equal("proj2", result[1].Id);
    }

    [Fact]
    public void MapToViewProjects_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var projects = new List<Project>();

        // Act
        var result = _mapper.MapToViewProjects(projects);

        // Assert
        Assert.Empty(result);
    }
}
