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
            Name = "Disintegrating Pistol",
            OrganizationId = "537650f3b77efe23a47914f3"
        };

        // Act
        var result = _mapper.MapToProject(source);

        // Assert
        Assert.Equal("Disintegrating Pistol", result.Name);
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
    }

    [Fact]
    public void MapToViewProject_WithValidProject_MapsAllProperties()
    {
        // Arrange
        var source = new Project
        {
            Id = "537650f3b77efe23a47914f4",
            Name = "Disintegrating Pistol",
            OrganizationId = "537650f3b77efe23a47914f3",
            DeleteBotDataEnabled = true
        };

        // Act
        var result = _mapper.MapToViewProject(source);

        // Assert
        Assert.Equal("537650f3b77efe23a47914f4", result.Id);
        Assert.Equal("Disintegrating Pistol", result.Name);
        Assert.Equal("537650f3b77efe23a47914f3", result.OrganizationId);
        Assert.True(result.DeleteBotDataEnabled);
    }

    [Fact]
    public void MapToViewProject_WithSlackToken_SetsHasSlackIntegration()
    {
        // Arrange
        var source = new Project
        {
            Id = "537650f3b77efe23a47914f4",
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
            Id = "537650f3b77efe23a47914f4",
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
            new() { Id = "537650f3b77efe23a47914f4", Name = "Project 1" },
            new() { Id = "1ecd0826e447ad1e78877a66", Name = "Project 2" }
        };

        // Act
        var result = _mapper.MapToViewProjects(projects);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal("537650f3b77efe23a47914f4", result[0].Id);
        Assert.Equal("1ecd0826e447ad1e78877a66", result[1].Id);
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
